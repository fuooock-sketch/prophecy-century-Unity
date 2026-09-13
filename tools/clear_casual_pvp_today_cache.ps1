param(
    [string]$UtcDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
)

$ErrorActionPreference = 'Stop'
$targetDate = [DateTime]::ParseExact($UtcDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).Date
$cachePath = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\prophecy_century\casual_pvp_mirror_pool.json'
if (-not (Test-Path -LiteralPath $cachePath)) {
    Write-Host "No local Casual PVP cache exists: $cachePath"
    exit 0
}

$backupPath = "$cachePath.before-$UtcDate-cleanup.bak"
Copy-Item -LiteralPath $cachePath -Destination $backupPath -Force
$store = Get-Content -Raw -Encoding UTF8 -LiteralPath $cachePath | ConvertFrom-Json
$snapshots = @($store.snapshots)
$store.snapshots = @($snapshots | Where-Object {
    $capturedAt = $_.capturedAtUtc
    $null -eq $capturedAt -or ([DateTime]$capturedAt).ToUniversalTime().Date -ne $targetDate
})
$store | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $cachePath -Encoding UTF8
Write-Host "Removed $($snapshots.Count - $store.snapshots.Count) snapshots captured on $UtcDate."
Write-Host "Backup: $backupPath"
