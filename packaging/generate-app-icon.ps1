<#
.SYNOPSIS
    Renders the Codex Reset Tray brand application icon (Assets/app.ico).

.DESCRIPTION
    Reproducibly draws a stable brand "gauge" mark - the same circular ring
    motif the live tray icon uses - and assembles a multi-resolution .ico
    (16/20/24/32/48/64/256) by writing the ICONDIR / ICONDIRENTRY headers and
    PNG-encoded frames by hand. No external tooling required; only
    System.Drawing (GDI+).

    Run from anywhere:
        pwsh -NoProfile -File packaging/generate-app-icon.ps1

    The icon is the brand mark, NOT a live usage gauge: a fixed emerald ring
    (~72% swept) on a rounded dark surface with a clean needle + hub, so the
    product feels cohesive with the dynamic tray icon while staying stable.
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

# ---- Shared design-brief palette ------------------------------------------------
$colSurface   = [System.Drawing.Color]::FromArgb(255, 22, 27, 34)   # #161B22 surface
$colSurfaceHi = [System.Drawing.Color]::FromArgb(255, 28, 34, 48)   # #1C2230 raised
$colStroke    = [System.Drawing.Color]::FromArgb(255, 42, 50, 60)   # #2A323C
$colTrack     = [System.Drawing.Color]::FromArgb(255, 52, 62, 74)   # ring track
$colBrand     = [System.Drawing.Color]::FromArgb(255, 54, 211, 153) # #36D399 emerald
$colBrandHi   = [System.Drawing.Color]::FromArgb(255, 94, 227, 176) # #5EE3B0 hover
$colInk       = [System.Drawing.Color]::FromArgb(255, 232, 237, 242)# #E8EDF2 text
$colHalo      = [System.Drawing.Color]::FromArgb(235, 9, 12, 16)    # dark halo

$startAngle = 135.0
$sweepAngle = 270.0
$brandFill  = 0.72   # stable, recognisable sweep

function New-IconFrame {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        # Rounded-square brand surface (only at sizes where it reads as a tile;
        # tiny frames stay as a bare ring so the gauge silhouette survives 16px).
        if ($Size -ge 24) {
            $margin = [Math]::Max(1.0, $Size * 0.06)
            $radius = [Math]::Max(3.0, $Size * 0.22)
            $rect = New-Object System.Drawing.RectangleF($margin, $margin, ($Size - 2 * $margin), ($Size - 2 * $margin))
            $path = New-Object System.Drawing.Drawing2D.GraphicsPath
            $d = $radius * 2.0
            $path.AddArc($rect.Left, $rect.Top, $d, $d, 180, 90)
            $path.AddArc($rect.Right - $d, $rect.Top, $d, $d, 270, 90)
            $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
            $path.AddArc($rect.Left, $rect.Bottom - $d, $d, $d, 90, 90)
            $path.CloseFigure()

            $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $colSurfaceHi, $colSurface, 90.0)
            $g.FillPath($brush, $path)
            $brush.Dispose()

            $strokePen = New-Object System.Drawing.Pen($colStroke, [Math]::Max(1.0, $Size * 0.02))
            $g.DrawPath($strokePen, $path)
            $strokePen.Dispose()
            $path.Dispose()
        }

        # Gauge ring geometry (fractions of the frame -> crisp at every size).
        $ringStroke = [Math]::Max(2.4, $Size * 0.115)
        $haloStroke = $ringStroke + [Math]::Max(1.2, $Size * 0.05)
        $inset = ($haloStroke / 2.0) + [Math]::Max(1.2, $Size * 0.2)
        $ring = New-Object System.Drawing.RectangleF($inset, $inset, ($Size - 2 * $inset), ($Size - 2 * $inset))

        # Dark halo so the ring reads on any backdrop.
        $halo = New-Object System.Drawing.Pen($colHalo, $haloStroke)
        $halo.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $halo.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($halo, $ring, $startAngle, $sweepAngle)
        $halo.Dispose()

        # Neutral track.
        $track = New-Object System.Drawing.Pen($colTrack, $ringStroke)
        $track.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $track.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($track, $ring, $startAngle, $sweepAngle)
        $track.Dispose()

        # Brand sweep (emerald), with a soft gradient at larger sizes.
        if ($Size -ge 32) {
            $gp = New-Object System.Drawing.Drawing2D.LinearGradientBrush($ring, $colBrand, $colBrandHi, 60.0)
            $progress = New-Object System.Drawing.Pen($gp, $ringStroke)
        } else {
            $progress = New-Object System.Drawing.Pen($colBrand, $ringStroke)
        }
        $progress.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $progress.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($progress, $ring, $startAngle, ($sweepAngle * $brandFill))
        if ($progress.Brush -is [System.Drawing.Drawing2D.LinearGradientBrush]) { $progress.Brush.Dispose() }
        $progress.Dispose()

        $cx = $Size / 2.0
        $cy = $Size / 2.0

        # Needle (gauge pointer) at sizes where it stays crisp.
        if ($Size -ge 24) {
            $needleLen = [Math]::Max(3.0, $Size * 0.2)
            $needleHalo = New-Object System.Drawing.Pen($colHalo, [Math]::Max(2.4, $Size * 0.1))
            $needleHalo.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $needleHalo.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $g.DrawLine($needleHalo, $cx, $cy, $cx, ($cy - $needleLen))
            $needleHalo.Dispose()

            $needle = New-Object System.Drawing.Pen($colInk, [Math]::Max(1.4, $Size * 0.05))
            $needle.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $needle.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            $g.DrawLine($needle, $cx, $cy, $cx, ($cy - $needleLen))
            $needle.Dispose()
        }

        # Hub dot.
        $hubR = [Math]::Max(1.4, $Size * 0.09)
        $hubHaloR = $hubR + [Math]::Max(0.8, $Size * 0.045)
        $hubHalo = New-Object System.Drawing.SolidBrush($colHalo)
        $g.FillEllipse($hubHalo, ($cx - $hubHaloR), ($cy - $hubHaloR), ($hubHaloR * 2.0), ($hubHaloR * 2.0))
        $hubHalo.Dispose()

        $hub = New-Object System.Drawing.SolidBrush($colBrand)
        $g.FillEllipse($hub, ($cx - $hubR), ($cy - $hubR), ($hubR * 2.0), ($hubR * 2.0))
        $hub.Dispose()
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
