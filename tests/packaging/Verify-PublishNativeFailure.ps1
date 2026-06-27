$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceScript = Join-Path $repoRoot "packaging\publish.ps1"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "CodexResetTrayPublishTest-$([System.Guid]::NewGuid())"
$miniRepo = Join-Path $tempRoot "repo"
$fakeBin = Join-Path $tempRoot "fake-bin"
$packagingDir = Join-Path $miniRepo "packaging"
$projectDir = Join-Path $miniRepo "src\CodexResetTray.App"
$stalePublishDir = Join-Path $miniRepo "artifacts\publish\win-x64"
$releaseDir = Join-Path $miniRepo "artifacts\release"
$zipPath = Join-Path $releaseDir "CodexResetTray-win-x64.zip"
$stdoutPath = Join-Path $tempRoot "publish-output.log"
$stderrPath = Join-Path $tempRoot "publish-error.log"
$previousZipContent = "previous release archive"

try {
    New-Item -ItemType Directory -Force -Path $fakeBin, $packagingDir, $projectDir, $stalePublishDir, $releaseDir | Out-Null

    Copy-Item -LiteralPath $sourceScript -Destination (Join-Path $packagingDir "publish.ps1")
    Set-Content -LiteralPath (Join-Path $projectDir "CodexResetTray.App.csproj") -Value "<Project />"
    Set-Content -LiteralPath (Join-Path $stalePublishDir "stale.txt") -Value "stale publish output"
    Set-Content -LiteralPath $zipPath -Value $previousZipContent -NoNewline
    Set-Content -LiteralPath (Join-Path $fakeBin "dotnet.cmd") -Value @(
        "@echo off",
        "exit /b 42"
    )

    $previousPath = $env:PATH
    $env:PATH = "$fakeBin;$previousPath"
    $powerShell = (Get-Process -Id $PID).Path

    $process = Start-Process `
        -FilePath $powerShell `
        -ArgumentList @("-NoProfile", "-File", (Join-Path $packagingDir "publish.ps1")) `
        -NoNewWindow `
        -Wait `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    if ($process.ExitCode -eq 0) {
        throw "publish.ps1 succeeded even though dotnet publish returned exit code 42."
    }

    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "publish.ps1 deleted the previous release zip after dotnet publish failed."
    }

    $currentZipContent = Get-Content -LiteralPath $zipPath -Raw
    if ($currentZipContent -ne $previousZipContent) {
        throw "publish.ps1 changed the previous release zip after dotnet publish failed."
    }

    $stdout = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
    $stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
    $output = "$stdout`n$stderr"
    if ($output -match "Published ") {
        throw "publish.ps1 printed a success message after dotnet publish failed."
    }
}
finally {
    if ($null -ne $previousPath) {
        $env:PATH = $previousPath
    }

    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
