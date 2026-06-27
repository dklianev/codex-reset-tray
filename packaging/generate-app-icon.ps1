<#
.SYNOPSIS
    Renders the Codex Reset Tray brand application icon (Assets/app.ico).

.DESCRIPTION
    Reproducibly draws the brand mark - a bold ring (the same motif the live
    tray icon uses) on a rounded dark tile - and assembles a multi-resolution
    .ico (16/20/24/32/48/64/256) by writing the ICONDIR / ICONDIRENTRY headers
    and PNG-encoded frames by hand. Only System.Drawing (GDI+) is required.

    Run from anywhere:
        pwsh -NoProfile -File packaging/generate-app-icon.ps1

    The mark is stable (a fixed ~75% emerald->cyan sweep), so the exe/window
    icon stays cohesive with the dynamic tray gauge without implying a live value.
#>

[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $OutputPath = Join-Path $repoRoot "src\CodexResetTray.App\Assets\app.ico"
}

$assetsDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
}

# ---- Brand palette --------------------------------------------------------------
$colTileTop = [System.Drawing.Color]::FromArgb(255, 28, 34, 48)   # #1C2230
$colTileBot = [System.Drawing.Color]::FromArgb(255, 12, 16, 24)   # deep
$colStroke  = [System.Drawing.Color]::FromArgb(255, 42, 50, 60)   # #2A323C
$colTrack   = [System.Drawing.Color]::FromArgb(255, 30, 40, 52)
$colA       = [System.Drawing.Color]::FromArgb(255, 52, 211, 153) # #34D399 emerald
$colB       = [System.Drawing.Color]::FromArgb(255, 34, 211, 238) # #22D3EE cyan

$startAngle = -90.0   # 12 o'clock
$sweep      = 270.0   # stable brand fill

function New-IconFrame {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        # Rounded tile (only where it reads; tiny frames stay a bare ring).
        if ($Size -ge 24) {
            $margin = [Math]::Max(1.0, $Size * 0.055)
            $radius = [Math]::Max(3.0, $Size * 0.23)
            $rect = New-Object System.Drawing.RectangleF($margin, $margin, ($Size - 2 * $margin), ($Size - 2 * $margin))
            $path = New-Object System.Drawing.Drawing2D.GraphicsPath
            $d = $radius * 2.0
            $path.AddArc($rect.Left, $rect.Top, $d, $d, 180, 90)
            $path.AddArc($rect.Right - $d, $rect.Top, $d, $d, 270, 90)
            $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
            $path.AddArc($rect.Left, $rect.Bottom - $d, $d, $d, 90, 90)
            $path.CloseFigure()

            $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $colTileTop, $colTileBot, 90.0)
            $g.FillPath($brush, $path)
            $brush.Dispose()

            $strokePen = New-Object System.Drawing.Pen($colStroke, [Math]::Max(1.0, $Size * 0.02))
            $g.DrawPath($strokePen, $path)
            $strokePen.Dispose()
            $path.Dispose()
        }

        # Bold brand ring.
        $stroke = [Math]::Max(2.6, $Size * 0.135)
        $inset = ($stroke / 2.0) + [Math]::Max(1.4, $Size * 0.2)
        $ring = New-Object System.Drawing.RectangleF($inset, $inset, ($Size - 2 * $inset), ($Size - 2 * $inset))

        $trackPen = New-Object System.Drawing.Pen($colTrack, $stroke)
        $g.DrawArc($trackPen, $ring, 0, 360)
        $trackPen.Dispose()

        $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($ring, $colA, $colB, 45.0)
        $progress = New-Object System.Drawing.Pen($grad, $stroke)
        $progress.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $progress.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($progress, $ring, $startAngle, $sweep)
        $progress.Dispose()
        $grad.Dispose()
    }
    finally {
        $g.Dispose()
    }

    return $bmp
}

function Get-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)
    $ms = New-Object System.IO.MemoryStream
    $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
}

# ---- Assemble the multi-resolution .ico ----------------------------------------
$sizes = @(16, 20, 24, 32, 48, 64, 256)

$bitmaps = @()
$payloads = @()
foreach ($s in $sizes) {
    $bmp = New-IconFrame -Size $s
    $bitmaps += $bmp
    $payloads += ,(Get-PngBytes -Bitmap $bmp)
}

$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)

# ICONDIR
$writer.Write([int16]0)               # reserved
$writer.Write([int16]1)               # type = icon
$writer.Write([int16]$sizes.Count)    # image count

# ICONDIRENTRY records, then image data appended afterwards.
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = $sizes[$i]
    $byteDim = if ($dim -ge 256) { 0 } else { $dim }
    $writer.Write([byte]$byteDim)      # width  (0 => 256)
    $writer.Write([byte]$byteDim)      # height (0 => 256)
    $writer.Write([byte]0)             # palette count
    $writer.Write([byte]0)             # reserved
    $writer.Write([int16]1)            # colour planes
    $writer.Write([int16]32)           # bits per pixel
    $writer.Write([int32]$payloads[$i].Length)  # image data size
    $writer.Write([int32]$offset)               # offset to image data
    $offset += $payloads[$i].Length
}

foreach ($payload in $payloads) {
    $writer.Write($payload)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($OutputPath, $out.ToArray())

$writer.Dispose()
$out.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

$len = (Get-Item $OutputPath).Length
Write-Host "Wrote $OutputPath ($len bytes, $($sizes.Count) frames: $($sizes -join ', '))"
