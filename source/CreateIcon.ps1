$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconSizes = @(16,32,48,128,256)
$iconImages = [Collections.Generic.List[byte[]]]::new()
foreach ($iconSize in $iconSizes) {
    $bitmap = [Drawing.Bitmap]::new($iconSize,$iconSize)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($iconSize / 256.0, $iconSize / 256.0)
    $graphics.Clear([Drawing.Color]::FromArgb(247,245,239))
    $cord = [Drawing.Pen]::new([Drawing.Color]::FromArgb(85,114,96),6)
    $graphics.DrawLine($cord,28,72,228,72)
    $shadow = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(216,222,208))
    $paper = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $photo = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(137,154,118))
    $peg = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(85,114,96))
    $graphics.TranslateTransform(128,82)
    $graphics.RotateTransform(-9)
    $graphics.FillRectangle($shadow,-57,12,126,142)
    $graphics.FillRectangle($paper,-63,6,126,142)
    $graphics.FillRectangle($photo,-49,22,98,86)
    $graphics.FillRectangle($peg,-6,-24,12,43)
    $memory = [IO.MemoryStream]::new()
    $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
    $iconImages.Add($memory.ToArray())
    $memory.Dispose(); $peg.Dispose(); $photo.Dispose(); $paper.Dispose(); $shadow.Dispose(); $cord.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$iconOutput = [IO.File]::Create((Join-Path $PSScriptRoot 'Airline.ico'))
$writer = [IO.BinaryWriter]::new($iconOutput)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconImages.Count)
$iconOffset = 6 + 16 * $iconImages.Count
for ($iconIndex=0; $iconIndex -lt $iconImages.Count; $iconIndex++) {
    $dimension = if ($iconSizes[$iconIndex] -eq 256) {0} else {$iconSizes[$iconIndex]}
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$iconImages[$iconIndex].Length); $writer.Write([uint32]$iconOffset)
    $iconOffset += $iconImages[$iconIndex].Length
}
foreach($iconBytes in $iconImages) {$writer.Write($iconBytes)}
$writer.Dispose(); $iconOutput.Dispose()
