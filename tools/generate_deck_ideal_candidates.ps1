param(
  [Parameter(Mandatory = $true)][string]$OutputPath,
  [Parameter(Mandatory = $true)][int]$Width,
  [Parameter(Mandatory = $true)][int]$Height,
  [Parameter(Mandatory = $true)][int]$Seed,
  [ValidateSet("CardBack", "CardFace", "Board")][string]$Style = "CardBack"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-Color([int]$a, [int]$r, [int]$g, [int]$b) {
  return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function Fill-Gradient([System.Drawing.Graphics]$g, [System.Drawing.Rectangle]$rect, [System.Drawing.Color]$top, [System.Drawing.Color]$bottom) {
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $top, $bottom, [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.FillRectangle($brush, $rect)
  $brush.Dispose()
}

function Add-RadialGlow([System.Drawing.Graphics]$g, [int]$cx, [int]$cy, [int]$radius, [System.Drawing.Color]$centerColor) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddEllipse($cx - $radius, $cy - $radius, $radius * 2, $radius * 2)
  $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
  $brush.CenterColor = $centerColor
  $brush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $centerColor.R, $centerColor.G, $centerColor.B))
  $g.FillPath($brush, $path)
  $brush.Dispose()
  $path.Dispose()
}

function Add-Starfield([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$width, [int]$height, [int]$count, [System.Drawing.Color]$baseColor) {
  for ($i = 0; $i -lt $count; $i++) {
    $x = [int]($rand.NextDouble() * $width)
    $y = [int]($rand.NextDouble() * $height)
    $size = if ($rand.NextDouble() -lt 0.15) { 2 } else { 1 }
    $alpha = 40 + $rand.Next(160)
    $color = [System.Drawing.Color]::FromArgb($alpha, $baseColor.R, $baseColor.G, $baseColor.B)
    $brush = New-Object System.Drawing.SolidBrush($color)
    $g.FillEllipse($brush, $x, $y, $size, $size)
    $brush.Dispose()
  }
}

function Add-SandStreaks([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$width, [int]$height, [int]$count, [System.Drawing.Color]$baseColor) {
  for ($i = 0; $i -lt $count; $i++) {
    $x = $rand.NextDouble() * $width
    $y = $rand.NextDouble() * $height
    $len = 4 + $rand.Next(12)
    $angle = $rand.NextDouble() * 6.28318530718
    $dx = [Math]::Cos($angle) * $len
    $dy = [Math]::Sin($angle) * $len
    $alpha = 30 + $rand.Next(140)
    $color = [System.Drawing.Color]::FromArgb($alpha, $baseColor.R, $baseColor.G, $baseColor.B)
    $pen = New-Object System.Drawing.Pen($color, 1)
    $g.DrawLine($pen, $x, $y, $x + $dx, $y + $dy)
    $pen.Dispose()
  }
}

function Add-MagicRings([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$radius, [int]$count, [System.Drawing.Color]$baseColor) {
  for ($i = 0; $i -lt $count; $i++) {
    $factor = 0.45 + ($i / [Math]::Max(1, $count)) * 0.5
    $r = [int]($radius * $factor)
    $alpha = 60 + $rand.Next(120)
    $color = [System.Drawing.Color]::FromArgb($alpha, $baseColor.R, $baseColor.G, $baseColor.B)
    $pen = New-Object System.Drawing.Pen($color, 2)
    $g.DrawEllipse($pen, $cx - $r, $cy - $r, $r * 2, $r * 2)
    $pen.Dispose()
  }
}

function Add-SwirlArcs([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$radius, [int]$count, [System.Drawing.Color]$baseColor) {
  for ($i = 0; $i -lt $count; $i++) {
    $r = [int]($radius * (0.35 + $rand.NextDouble() * 0.6))
    $start = $rand.Next(0, 360)
    $sweep = 40 + $rand.Next(160)
    $alpha = 30 + $rand.Next(120)
    $color = [System.Drawing.Color]::FromArgb($alpha, $baseColor.R, $baseColor.G, $baseColor.B)
    $pen = New-Object System.Drawing.Pen($color, 1)
    $g.DrawArc($pen, $cx - $r, $cy - $r, $r * 2, $r * 2, $start, $sweep)
    $pen.Dispose()
  }
}

function Add-Frame([System.Drawing.Graphics]$g, [int]$width, [int]$height, [System.Drawing.Color]$baseColor) {
  $outer = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(190, $baseColor.R, $baseColor.G, $baseColor.B), 3)
  $inner = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140, $baseColor.R, $baseColor.G, $baseColor.B), 1)
  $g.DrawRectangle($outer, 10, 10, $width - 21, $height - 21)
  $g.DrawRectangle($inner, 18, 18, $width - 37, $height - 37)
  $g.DrawRectangle($inner, 28, 28, $width - 57, $height - 57)
  $outer.Dispose()
  $inner.Dispose()
}

function Add-OrnateCorners([System.Drawing.Graphics]$g, [int]$width, [int]$height, [System.Drawing.Color]$baseColor) {
  $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, $baseColor.R, $baseColor.G, $baseColor.B), 2)
  $decor = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, $baseColor.R, $baseColor.G, $baseColor.B), 1)
  $offset = 28
  $size = 70
  $corners = @(
    @{ X = $offset; Y = $offset },
    @{ X = $width - $offset - $size; Y = $offset },
    @{ X = $offset; Y = $height - $offset - $size },
    @{ X = $width - $offset - $size; Y = $height - $offset - $size }
  )
  foreach ($corner in $corners) {
    $x = $corner.X
    $y = $corner.Y
    $g.DrawArc($pen, $x, $y, $size, $size, 180, 90)
    $g.DrawArc($pen, $x + 10, $y + 10, $size - 20, $size - 20, 180, 90)
    $g.DrawLine($decor, $x + 8, $y + $size - 6, $x + $size - 6, $y + 8)
  }
  $pen.Dispose()
  $decor.Dispose()
}

function Add-RuneTicks([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$radius, [int]$count, [System.Drawing.Color]$baseColor) {
  for ($i = 0; $i -lt $count; $i++) {
    $angle = $rand.NextDouble() * 6.28318530718
    $inner = $radius - 14 - $rand.Next(8)
    $outer = $radius + 6 + $rand.Next(6)
    $x1 = $cx + [Math]::Cos($angle) * $inner
    $y1 = $cy + [Math]::Sin($angle) * $inner
    $x2 = $cx + [Math]::Cos($angle) * $outer
    $y2 = $cy + [Math]::Sin($angle) * $outer
    $alpha = 60 + $rand.Next(120)
    $color = [System.Drawing.Color]::FromArgb($alpha, $baseColor.R, $baseColor.G, $baseColor.B)
    $pen = New-Object System.Drawing.Pen($color, 1)
    $g.DrawLine($pen, $x1, $y1, $x2, $y2)
    $pen.Dispose()
  }
}

function Add-CelestialLines([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$width, [int]$height, [int]$count, [System.Drawing.Color]$baseColor) {
  for ($i = 0; $i -lt $count; $i++) {
    $x = $rand.NextDouble() * $width
    $y = $rand.NextDouble() * $height
    $length = 40 + $rand.Next(120)
    $angle = ($rand.NextDouble() * 0.6 + 0.2) * 3.1415926535
    $dx = [Math]::Cos($angle) * $length
    $dy = [Math]::Sin($angle) * $length
    $alpha = 25 + $rand.Next(60)
    $color = [System.Drawing.Color]::FromArgb($alpha, $baseColor.R, $baseColor.G, $baseColor.B)
    $pen = New-Object System.Drawing.Pen($color, 1)
    $g.DrawLine($pen, $x, $y, $x + $dx, $y + $dy)
    $pen.Dispose()
  }
}

function Add-EdgeGlow([System.Drawing.Graphics]$g, [int]$width, [int]$height, [System.Drawing.Color]$baseColor) {
  $rect = New-Object System.Drawing.Rectangle 0, 0, $width, $height
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(30, $baseColor.R, $baseColor.G, $baseColor.B), [System.Drawing.Color]::FromArgb(0, $baseColor.R, $baseColor.G, $baseColor.B), [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
  $g.FillRectangle($brush, $rect)
  $brush.Dispose()
}

$rand = New-Object System.Random $Seed
$bmp = New-Object System.Drawing.Bitmap $Width, $Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rect = New-Object System.Drawing.Rectangle 0, 0, $Width, $Height

switch ($Style) {
  "CardBack" {
    $top = New-Color 255 12 16 34
    $bottom = New-Color 255 6 8 20
    $glow = New-Color 160 80 120 200
    $spark = New-Color 255 210 190 120
    $magic = New-Color 255 140 190 255
    $starCount = [int](($Width * $Height) / 1200)
    $sandCount = [int](($Width * $Height) / 1400)
    $ringCount = 4 + $rand.Next(2)
    $arcCount = 20 + $rand.Next(10)
    $frameColor = New-Color 255 190 150 80
    $runeCount = 80 + $rand.Next(40)
    $lineCount = 16 + $rand.Next(10)
    $edgeGlow = New-Color 255 120 90 40
  }
  "CardFace" {
    $top = New-Color 255 14 18 40
    $bottom = New-Color 255 8 10 24
    $glow = New-Color 140 70 110 190
    $spark = New-Color 255 180 170 130
    $magic = New-Color 255 120 170 235
    $starCount = [int](($Width * $Height) / 1500)
    $sandCount = [int](($Width * $Height) / 1800)
    $ringCount = 3 + $rand.Next(2)
    $arcCount = 18 + $rand.Next(8)
    $frameColor = New-Color 255 150 120 70
    $runeCount = 90 + $rand.Next(30)
    $lineCount = 22 + $rand.Next(10)
    $edgeGlow = New-Color 255 100 80 40
  }
  "Board" {
    $top = New-Color 255 8 12 28
    $bottom = New-Color 255 4 6 18
    $glow = New-Color 170 90 130 220
    $spark = New-Color 255 200 180 120
    $magic = New-Color 255 120 200 255
    $starCount = [int](($Width * $Height) / 1000)
    $sandCount = [int](($Width * $Height) / 1200)
    $ringCount = 5 + $rand.Next(3)
    $arcCount = 28 + $rand.Next(10)
    $frameColor = New-Color 255 170 130 70
    $runeCount = 120 + $rand.Next(40)
    $lineCount = 26 + $rand.Next(14)
    $edgeGlow = New-Color 255 120 90 50
  }
}

Fill-Gradient -g $g -rect $rect -top $top -bottom $bottom

$centerX = [int]($Width * (0.48 + $rand.NextDouble() * 0.04))
$centerY = [int]($Height * (0.48 + $rand.NextDouble() * 0.04))
$radius = [int]([Math]::Min($Width, $Height) * 0.45)

Add-RadialGlow -g $g -cx $centerX -cy $centerY -radius $radius -centerColor $glow
Add-Starfield -g $g -rand $rand -width $Width -height $Height -count $starCount -baseColor $spark
Add-SandStreaks -g $g -rand $rand -width $Width -height $Height -count $sandCount -baseColor $spark
Add-MagicRings -g $g -rand $rand -cx $centerX -cy $centerY -radius $radius -count $ringCount -baseColor $magic
Add-SwirlArcs -g $g -rand $rand -cx $centerX -cy $centerY -radius $radius -count $arcCount -baseColor $magic
Add-RuneTicks -g $g -rand $rand -cx $centerX -cy $centerY -radius $radius -count $runeCount -baseColor $spark
Add-CelestialLines -g $g -rand $rand -width $Width -height $Height -count $lineCount -baseColor $magic
Add-EdgeGlow -g $g -width $Width -height $Height -baseColor $edgeGlow

if ($Style -eq "CardBack") {
  Add-Frame -g $g -width $Width -height $Height -baseColor $frameColor
  Add-OrnateCorners -g $g -width $Width -height $Height -baseColor $frameColor
}

$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path $dir)) {
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

Write-Output ("generated: {0}" -f $OutputPath)
