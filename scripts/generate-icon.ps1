$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetDirectory = Join-Path $repoRoot 'src\LocalPlay.App\Assets'
$iconPath = Join-Path $assetDirectory 'LocalPlay.ico'
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null

$size = 64
$bitmap = [Drawing.Bitmap]::new($size, $size)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.ColorTranslator]::FromHtml('#326BFF'))

$gradient = [Drawing.Drawing2D.LinearGradientBrush]::new(
    [Drawing.Point]::new(5, 5),
    [Drawing.Point]::new(59, 59),
    [Drawing.ColorTranslator]::FromHtml('#326BFF'),
    [Drawing.ColorTranslator]::FromHtml('#58A0FF'))
$graphics.FillRectangle($gradient, 0, 0, $size, $size)

$triangle = [Drawing.PointF[]]@(
    [Drawing.PointF]::new(14.5, 42.5),
    [Drawing.PointF]::new(32, 22),
    [Drawing.PointF]::new(49.5, 42.5))
$graphics.FillPolygon([Drawing.Brushes]::White, $triangle)

$pixelBytes = $size * $size * 4
$maskStride = [Math]::Ceiling($size / 32) * 4
$maskBytes = $maskStride * $size
$imageBytes = 40 + $pixelBytes + $maskBytes
$fileStream = [IO.File]::Create($iconPath)
$writer = [IO.BinaryWriter]::new($fileStream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([Byte]$size)
$writer.Write([Byte]$size)
$writer.Write([Byte]0)
$writer.Write([Byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$imageBytes)
$writer.Write([UInt32]22)

$writer.Write([UInt32]40)
$writer.Write([Int32]$size)
$writer.Write([Int32]($size * 2))
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]0)
$writer.Write([UInt32]$pixelBytes)
$writer.Write([Int32]0)
$writer.Write([Int32]0)
$writer.Write([UInt32]0)
$writer.Write([UInt32]0)

for ($y = $size - 1; $y -ge 0; $y--) {
    for ($x = 0; $x -lt $size; $x++) {
        $pixel = $bitmap.GetPixel($x, $y)
        $writer.Write([Byte]$pixel.B)
        $writer.Write([Byte]$pixel.G)
        $writer.Write([Byte]$pixel.R)
        $writer.Write([Byte]$pixel.A)
    }
}

$writer.Write([Byte[]]::new($maskBytes))
$writer.Dispose()
$gradient.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Generated $iconPath"
