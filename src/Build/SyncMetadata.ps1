param(
    [Parameter(Mandatory = $true)]
    [string]$MetadataPath,
    [string]$ModInfoPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ModInfoPath)) {
    $ModInfoPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\SephiriaExpanded\ModInfo.cs"
}

if (-not (Test-Path -LiteralPath $ModInfoPath)) {
    throw "ModInfo.cs를 찾을 수 없습니다: $ModInfoPath"
}

if (-not (Test-Path -LiteralPath $MetadataPath)) {
    throw "metadata.json을 찾을 수 없습니다: $MetadataPath"
}

$modInfo = Get-Content -LiteralPath $ModInfoPath -Raw
$match = [regex]::Match($modInfo, 'Version\s*=\s*"([^"]+)"')
if (-not $match.Success) {
    throw "ModInfo.cs에서 ModInfo.Version을 찾을 수 없습니다."
}

$version = $match.Groups[1].Value
$metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
$metadata.modVersion = $version
$json = $metadata | ConvertTo-Json -Depth 16
Set-Content -LiteralPath $MetadataPath -Value $json -Encoding UTF8
Write-Output ("metadata.json modVersion을 {0}으로 변경했습니다." -f $version)
