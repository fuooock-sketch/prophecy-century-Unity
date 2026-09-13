param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
Set-Location $workspace
# Outside Unity's Temp directory, which the main editor deletes on exit.
$validationRoot = Join-Path $PSScriptRoot 'obj/PvpRecoveryValidation'
$editorData = Join-Path (Split-Path $UnityEditor -Parent) 'Data'
$response = Get-ChildItem -Path Library/Bee/artifacts -Filter Assembly-CSharp.rsp -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$response) { throw 'Open the main Unity project and compile once to generate its compiler response file.' }
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Plugins", "$validationRoot/Assets/Editor", "$validationRoot/Assets/Resources/Data", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
$assembly = Join-Path $validationRoot 'Assets/Plugins/PvpCheck.dll'
& "$editorData/NetCoreRuntime/dotnet.exe" "$editorData/DotNetSdkRoslyn/csc.dll" "@$($response.FullName)" "-out:$assembly" "-refout:$validationRoot/PvpCheck.ref.dll"
if ($LASTEXITCODE -ne 0) { throw 'Runtime C# compilation failed.' }
Get-ChildItem Assets/Editor -Filter 'CasualPvp*SelfTest.cs' | Copy-Item -Destination "$validationRoot/Assets/Editor"
Get-ChildItem Assets/Resources/Data -Filter *.json | Copy-Item -Destination "$validationRoot/Assets/Resources/Data"
Copy-Item -LiteralPath ProjectSettings/ProjectVersion.txt -Destination "$validationRoot/ProjectSettings/ProjectVersion.txt"
$dependencies = @{}
(Get-Content Packages/manifest.json -Raw | ConvertFrom-Json).dependencies.PSObject.Properties |
    Where-Object Name -Like 'com.unity.modules.*' | ForEach-Object { $dependencies[$_.Name] = $_.Value }
$uiPackage = Get-ChildItem Library/PackageCache -Directory -Filter 'com.unity.ugui@*' | Select-Object -First 1
if (!$uiPackage) { throw 'The main project Unity UI package cache is missing.' }
$dependencies['com.unity.ugui'] = 'file:' + ($uiPackage.FullName -replace '\\','/')
@{dependencies=$dependencies} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 "$validationRoot/Packages/manifest.json"
# A new filename prevents an old success marker from satisfying this run.
$logPath = Join-Path $validationRoot ("test-" + [Guid]::NewGuid().ToString('N') + '.log')
$fixture = $null
try {
    $testId = [Guid]::NewGuid().ToString('N')
    $readyPath = Join-Path $validationRoot "fixture-$testId.json"
    $databasePath = Join-Path $validationRoot "fixture-$testId.db"
    $fixtureArguments = "`"$PSScriptRoot/casual_pvp_server/integration_fixture.py`" --ready-file `"$readyPath`" --database `"$databasePath`""
    $fixture = Start-Process -FilePath (Get-Command python).Source -ArgumentList $fixtureArguments -WindowStyle Hidden -PassThru
    $readyDeadline = [DateTime]::UtcNow.AddSeconds(15)
    while (!(Test-Path -LiteralPath $readyPath)) {
        if ($fixture.HasExited -or [DateTime]::UtcNow -gt $readyDeadline) { throw 'Integration fixture failed to start.' }
        Start-Sleep -Milliseconds 100
    }
    $endpoint = (Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json).endpoint
    $arguments = "-batchmode -nographics -projectPath `"$validationRoot`" -executeMethod CasualPvpSelfTest.RunBatch -pvpTestEndpoint $endpoint -logFile `"$logPath`""
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(180000)) { Stop-Process -Id $process.Id; throw 'Unity integration tests timed out.' }
}
finally { if ($fixture -and !$fixture.HasExited) { Stop-Process -Id $fixture.Id } }
$logText = if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Raw } else { '' }
if ($logText -notmatch 'Casual PVP self test passed\.' -or $logText -notmatch 'Casual PVP network integration passed\.' -or $logText -match '(?m)^.*(?:error CS\d+|Exception:)') {
    Write-Output "Unity test log: $logPath"
    throw 'Casual PVP Unity regression checks failed. Inspect the test log.'
}
Write-Output "PASS: runtime compilation, Unity recovery and real HTTP integration tests. Log: $logPath"
