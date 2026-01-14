param(
  [Parameter(Mandatory = $true)][string]$HighlightOutput,
  [Parameter(Mandatory = $true)][string]$MagicCircleOutput,
  [int]$Size = 1024,
  [int]$HighlightSeed = 20260120,
  [int]$MagicSeed = 20260121
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-OverlayBitmap([int]$side) {
  $bitmap = New-Object System.Drawing.Bitmap $side, $side, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $graphics.Clear([System.Drawing.Color]::Black)
  return @($bitmap, $graphics)
}

function Draw-Starfield([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$side, [int]$count) {
  for ($i = 0; $i -lt $count; $i++) {
    $x = [int]($rand.NextDouble() * $side)
    $y = [int]($rand.NextDouble() * $side)
    $size = if ($rand.NextDouble() -lt 0.2) { 2 } else { 1 }
    $alpha = 40 + $rand.Next(160)
    $color = [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255)
    $brush = New-Object System.Drawing.SolidBrush($color)
    $g.FillEllipse($brush, $x, $y, $size, $size)
    $brush.Dispose()
  }
}

function Draw-Rings([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$count, [int]$minR, [int]$maxR, [int]$minAlpha, [int]$maxAlpha) {
  for ($i = 0; $i -lt $count; $i++) {
    $r = $minR + $rand.Next($maxR - $minR)
    $alpha = $minAlpha + $rand.Next($maxAlpha - $minAlpha)
    $width = 1 + $rand.Next(2)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255), $width)
    $g.DrawEllipse($pen, $cx - $r, $cy - $r, $r * 2, $r * 2)
    $pen.Dispose()
  }
}

function Draw-Arcs([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$count, [int]$minR, [int]$maxR) {
  for ($i = 0; $i -lt $count; $i++) {
    $r = $minR + $rand.Next($maxR - $minR)
    $start = $rand.Next(0, 360)
    $sweep = 40 + $rand.Next(180)
    $alpha = 30 + $rand.Next(120)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255), 1)
    $g.DrawArc($pen, $cx - $r, $cy - $r, $r * 2, $r * 2, $start, $sweep)
    $pen.Dispose()
  }
}

function Draw-RuneTicks([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$radius, [int]$count) {
  for ($i = 0; $i -lt $count; $i++) {
    $angle = $rand.NextDouble() * 6.28318530718
    $inner = $radius - 8 - $rand.Next(16)
    $outer = $radius + 6 + $rand.Next(12)
    $x1 = $cx + [Math]::Cos($angle) * $inner
    $y1 = $cy + [Math]::Sin($angle) * $inner
    $x2 = $cx + [Math]::Cos($angle) * $outer
    $y2 = $cy + [Math]::Sin($angle) * $outer
    $alpha = 60 + $rand.Next(140)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255), 1)
    $g.DrawLine($pen, $x1, $y1, $x2, $y2)
    $pen.Dispose()
  }
}

function Draw-RadialLines([System.Drawing.Graphics]$g, [System.Random]$rand, [int]$cx, [int]$cy, [int]$count, [int]$minLen, [int]$maxLen) {
  for ($i = 0; $i -lt $count; $i++) {
    $angle = $rand.NextDouble() * 6.28318530718
    $len = $minLen + $rand.Next($maxLen - $minLen)
    $alpha = 40 + $rand.Next(120)
    $x2 = $cx + [Math]::Cos($angle) * $len
    $y2 = $cy + [Math]::Sin($angle) * $len
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255), 1)
    $g.DrawLine($pen, $cx, $cy, $x2, $y2)
    $pen.Dispose()
  }
}

function Write-Overlay([string]$outputPath, [int]$side, [int]$seed, [string]$mode) {
  $dir = Split-Path -Parent $outputPath
  if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
  }

  $rand = New-Object System.Random $seed
  $center = [int]($side * 0.5)
  $minRadius = [int]($side * 0.18)
  $maxRadius = [int]($side * 0.48)

  $bitmap, $graphics = New-OverlayBitmap -side $side

  if ($mode -eq "Highlight") {
    Draw-Starfield -g $graphics -rand $rand -side $side -count ([int]($side * $side / 900))
    Draw-Rings -g $graphics -rand $rand -cx $center -cy $center -count 8 -minR $minRadius -maxR $maxRadius -minAlpha 30 -maxAlpha 140
    Draw-Arcs -g $graphics -rand $rand -cx $center -cy $center -count 24 -minR $minRadius -maxR $maxRadius
    Draw-RadialLines -g $graphics -rand $rand -cx $center -cy $center -count 36 -minLen $minRadius -maxLen $maxRadius
  } elseif ($mode -eq "MagicCircle") {
    Draw-Rings -g $graphics -rand $rand -cx $center -cy $center -count 6 -minR $minRadius -maxR $maxRadius -minAlpha 60 -maxAlpha 180
    Draw-RuneTicks -g $graphics -rand $rand -cx $center -cy $center -radius ([int]($side * 0.38)) -count 140
    Draw-Arcs -g $graphics -rand $rand -cx $center -cy $center -count 12 -minR ([int]($side * 0.25)) -maxR ([int]($side * 0.45))
  }

  $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Jpeg)
  $graphics.Dispose()
  $bitmap.Dispose()

  Write-Output ("generated: {0}" -f $outputPath)
}

Write-Overlay -outputPath $HighlightOutput -side $Size -seed $HighlightSeed -mode "Highlight"
Write-Overlay -outputPath $MagicCircleOutput -side $Size -seed $MagicSeed -mode "MagicCircle"
