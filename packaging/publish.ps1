param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\CodexResetTray.App\CodexResetTray.App.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$publishParentDir = Split-Path -Parent $publishDir
$releaseDir = Join-Path $repoRoot "artifacts\release"
$zipPath = Join-Path $releaseDir "CodexResetTray-$Runtime.zip"
$tempZipPath = Join-Path $releaseDir "CodexResetTray-$Runtime.tmp.zip"
$publishedExePath = Join-Path $publishDir "CodexResetTray.exe"
$stagingParentDir = Join-Path $repoRoot "artifacts\publish-staging"
$stagingDir = Join-Path $stagingParentDir "$Runtime-$([System.Guid]::NewGuid().ToString('N'))"

function Invoke-CheckedNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList

    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

function Get-RunningPublishedApp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    if (-not (Test-Path -LiteralPath $ExecutablePath)) {
        return @()
    }

    $targetPath = [System.IO.Path]::GetFullPath($ExecutablePath)

    Get-CimInstance Win32_Process -Filter "Name = 'CodexResetTray.exe'" |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
            [string]::Equals(
                [System.IO.Path]::GetFullPath($_.ExecutablePath),
                $targetPath,
                [StringComparison]::OrdinalIgnoreCase)
        }
}

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

if (Test-Path $tempZipPath) {
    Remove-Item -LiteralPath $tempZipPath
}

$runningPublishedApps = @(Get-RunningPublishedApp -ExecutablePath $publishedExePath)
if ($runningPublishedApps.Count -gt 0) {
    $processList = ($runningPublishedApps | ForEach-Object { "$($_.ProcessName) PID $($_.ProcessId)" }) -join ", "
    throw "Cannot publish because the previous published app is running from $publishedExePath ($processList). Exit Codex Reset Tray and run this script again."
}

New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

$publishArguments = @(
    "publish",
    $project,
    "-c",
    $Configuration,
    "-r",
    $Runtime,
    "--self-contained",
    "true",
    "/p:PublishSingleFile=true",
    "/p:PublishTrimmed=false",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=none",
    "/p:DebugSymbols=false",
    "-o",
    $stagingDir
)

try {
    Invoke-CheckedNativeCommand -FilePath "dotnet" -ArgumentList $publishArguments

    $stagedExePath = Join-Path $stagingDir "CodexResetTray.exe"
    if (-not (Test-Path -LiteralPath $stagedExePath)) {
        throw "dotnet publish completed but did not produce $stagedExePath."
    }

    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $tempZipPath

    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $publishParentDir | Out-Null
    Move-Item -LiteralPath $stagingDir -Destination $publishDir
    Move-Item -LiteralPath $tempZipPath -Destination $zipPath -Force

    Write-Host "Published $zipPath"
}
finally {
    if (Test-Path -LiteralPath $tempZipPath) {
        Remove-Item -LiteralPath $tempZipPath -Force
    }

    if (Test-Path -LiteralPath $stagingDir) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }

    if (Test-Path -LiteralPath $stagingParentDir) {
        $remainingStagingItems = Get-ChildItem -LiteralPath $stagingParentDir -Force | Select-Object -First 1
        if ($null -eq $remainingStagingItems) {
            Remove-Item -LiteralPath $stagingParentDir -Force
        }
    }
}
