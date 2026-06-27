param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "CodexResetTray.slnx"
$publishScript = Join-Path $repoRoot "packaging\publish.ps1"
$publishFailureTest = Join-Path $repoRoot "tests\packaging\Verify-PublishNativeFailure.ps1"

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

Invoke-CheckedNativeCommand -FilePath "dotnet" -ArgumentList @(
    "build",
    $solution,
    "-c",
    $Configuration
)

Invoke-CheckedNativeCommand -FilePath "dotnet" -ArgumentList @(
    "test",
    $solution,
    "-c",
    $Configuration,
    "--no-build"
)

& $publishFailureTest
& $publishScript -Configuration $Configuration -Runtime $Runtime
