param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\CodexResetTray.App\CodexResetTray.App.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$releaseDir = Join-Path $repoRoot "artifacts\release"
$zipPath = Join-Path $releaseDir "CodexResetTray-$Runtime.zip"

New-Item -ItemType Directory -Force -Path $publishDir, $releaseDir | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Published $zipPath"
