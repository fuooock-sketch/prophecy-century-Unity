$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root "Packages\manifest.json"
$assetsPath = Join-Path $root "Assets"

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$dependencies = $manifest.dependencies

$assetFiles = Get-ChildItem -LiteralPath $assetsPath -Recurse -File -Include *.cs,*.asmdef,*.asmref
$assetReferences = $assetFiles | Select-String -Pattern "UnityEngine\.Purchasing|IStoreListener|UnityPurchasing" -ErrorAction SilentlyContinue

if ($null -ne $dependencies."com.unity.purchasing" -and $null -eq $assetReferences) {
    Write-Error "com.unity.purchasing is installed but no Assets code references Unity Purchasing. Remove the package to avoid Purchasing/Analytics API incompatibility."
}

Write-Output "Unity package manifest validation passed."
