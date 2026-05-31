$ErrorActionPreference = "Stop"

$sourcePath = Join-Path $PSScriptRoot "..\Assets\Scripts\UI\RuntimeUiBootstrap.cs"
$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8

$required = @(
    "CreateTitleAstrolabe",
    "CreateTitleSelectionPanel",
    "StyleTitleButton",
    "AstrolabeRoot",
    "CampaignSelectionPanel",
    "HeroSelectionPanel"
)

$missing = @()
foreach ($token in $required) {
    if (-not $source.Contains($token)) {
        $missing += $token
    }
}

if ($source.Contains('ApplySpriteFromProjectPath(titlePanel.GetComponent<Image>(), "Art/bg/loading_image.png")')) {
    $missing += "TitlePanel still binds loading_image.png"
}

if ($missing.Count -gt 0) {
    Write-Error ("Title astrolabe validation failed: " + ($missing -join ", "))
}

Write-Output "Title astrolabe validation passed."
