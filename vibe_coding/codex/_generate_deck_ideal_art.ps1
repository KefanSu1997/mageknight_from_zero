Add-Type -AssemblyName System.Drawing

$root = "D:\study_and_work\unity-MK-test\MageKnight_from_zero"
$boardPath = Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\Board\deck_ideal_board_candidate_20260104_f.png"
$cardBackPath = Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\CardBack\deck_ideal_card_back_candidate_20260104_f.png"
$cardFacePaths = @(
  (Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\CardFace\deck_ideal_card_face_candidate_20260104_a.png"),
  (Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\CardFace\deck_ideal_card_face_candidate_20260104_b.png"),
  (Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\CardFace\deck_ideal_card_face_candidate_20260104_c.png"),
  (Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\CardFace\deck_ideal_card_face_candidate_20260104_d.png"),
  (Join-Path -Path $root -ChildPath "Assets\UI\Images\DeckTheme\Ideal\Candidates\CardFace\deck_ideal_card_face_candidate_20260104_e.png")
)

function New-Canvas {
  param(
    [int]$Width,
    [int]$Height,
    [System.Drawing.Color]$ColorA,
    [System.Drawing.Color]$ColorB
  )
  $bmp = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

  $rect = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
  $lg = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $ColorA, $ColorB, 90.0)
  $g.FillRectangle($lg, $rect)
  $lg.Dispose()

  $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $path.AddEllipse(-$Width * 0.2, -$Height * 0.2, $Width * 1.4, $Height * 1.4)
  $pg = [System.Drawing.Drawing2D.PathGradientBrush]::new($path)
  $pg.CenterColor = [System.Drawing.Color]::FromArgb(150, 90, 60, 130)
  $pg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 5, 4, 12))
  $g.FillRectangle($pg, $rect)
  $pg.Dispose()
  $path.Dispose()

  return @($bmp, $g)
}

function Add-Starfield {
  param(
    [System.Drawing.Graphics]$G,
    [System.Random]$Rng,
    [int]$Width,
    [int]$Height,
    [int]$Count,
    [System.Drawing.Color]$Color
  )
  $brush = [System.Drawing.SolidBrush]::new($Color)
  for ($i = 0; $i -lt $Count; $i++) {
    $x = $Rng.NextDouble() * $Width
    $y = $Rng.NextDouble() * $Height
    $r = 0.6 + $Rng.NextDouble() * 1.8
    $G.FillEllipse($brush, [float]$x, [float]$y, [float]$r, [float]$r)
  }
  $brush.Dispose()
}

function Add-Dust {
  param(
    [System.Drawing.Graphics]$G,
    [System.Random]$Rng,
    [int]$Width,
    [int]$Height,
    [int]$Count
  )
  for ($i = 0; $i -lt $Count; $i++) {
    $alpha = 12 + $Rng.Next(60)
    $color = [System.Drawing.Color]::FromArgb($alpha, 230, 210, 140)
    $brush = [System.Drawing.SolidBrush]::new($color)
    $x = $Rng.NextDouble() * $Width
    $y = $Rng.NextDouble() * $Height
    $r = 1.0 + $Rng.NextDouble() * 3.5
    $G.FillEllipse($brush, [float]$x, [float]$y, [float]$r, [float]$r)
    $brush.Dispose()
  }
}

function Add-SwirlArcs {
  param(
    [System.Drawing.Graphics]$G,
    [System.Random]$Rng,
    [int]$Width,
    [int]$Height,
    [int]$Count,
    [System.Drawing.Color]$Color
  )
  for ($i = 0; $i -lt $Count; $i++) {
    $radius = 40 + $Rng.Next(320)
    $thickness = 1.0 + $Rng.NextDouble() * 2.6
    $alpha = 20 + $Rng.Next(80)
    $penColor = [System.Drawing.Color]::FromArgb($alpha, $Color.R, $Color.G, $Color.B)
    $pen = [System.Drawing.Pen]::new($penColor, [float]$thickness)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $start = $Rng.Next(0, 360)
    $sweep = 80 + $Rng.Next(220)
    $x = ($Width / 2) - $radius
    $y = ($Height / 2) - $radius
    $G.DrawArc($pen, [float]$x, [float]$y, [float]($radius * 2), [float]($radius * 2), [float]$start, [float]$sweep)
    $pen.Dispose()
  }
}

function Draw-Frame {
  param(
    [System.Drawing.Graphics]$G,
    [int]$Width,
    [int]$Height,
    [int]$Inset
  )
  $outerRect = [System.Drawing.Rectangle]::new($Inset, $Inset, ($Width - (2 * $Inset)), ($Height - (2 * $Inset)))
  $frameBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($outerRect,
    [System.Drawing.Color]::FromArgb(255, 196, 150, 60),
    [System.Drawing.Color]::FromArgb(255, 90, 64, 18),
    90.0)
  $G.FillRectangle($frameBrush, $outerRect)
  $frameBrush.Dispose()

  $innerInset = $Inset + 24
  $innerRect = [System.Drawing.Rectangle]::new($innerInset, $innerInset, ($Width - (2 * $innerInset)), ($Height - (2 * $innerInset)))
  $innerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 18, 14, 30))
  $G.FillRectangle($innerBrush, $innerRect)
  $innerBrush.Dispose()

  $pen1 = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(200, 255, 226, 150), 3.5)
  $pen2 = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(160, 60, 40, 10), 6.0)
  $pen3 = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(190, 120, 80, 30), 2.0)
  $G.DrawRectangle($pen2, $outerRect)
  $G.DrawRectangle($pen1, $innerRect)
  $G.DrawRectangle($pen3, [System.Drawing.Rectangle]::new($innerInset + 10, $innerInset + 10, ($Width - (2 * ($innerInset + 10))), ($Height - (2 * ($innerInset + 10)))))
  $pen1.Dispose()
  $pen2.Dispose()
  $pen3.Dispose()
}

function Draw-RuneRing {
  param(
    [System.Drawing.Graphics]$G,
    [int]$Width,
    [int]$Height,
    [int]$Radius,
    [int]$Count,
    [System.Drawing.Color]$Color
  )
  $cx = $Width / 2.0
  $cy = $Height / 2.0
  $pen = [System.Drawing.Pen]::new($Color, 2.0)
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

  for ($i = 0; $i -lt $Count; $i++) {
    $angle = 2.0 * [Math]::PI * $i / $Count
    $x1 = $cx + [Math]::Cos($angle) * $Radius
    $y1 = $cy + [Math]::Sin($angle) * $Radius
    $x2 = $cx + [Math]::Cos($angle) * ($Radius - 14)
    $y2 = $cy + [Math]::Sin($angle) * ($Radius - 14)
    $G.DrawLine($pen, [float]$x1, [float]$y1, [float]$x2, [float]$y2)

    $x3 = $cx + [Math]::Cos($angle + 0.04) * ($Radius - 6)
    $y3 = $cy + [Math]::Sin($angle + 0.04) * ($Radius - 6)
    $x4 = $cx + [Math]::Cos($angle - 0.04) * ($Radius - 6)
    $y4 = $cy + [Math]::Sin($angle - 0.04) * ($Radius - 6)
    $G.DrawLine($pen, [float]$x3, [float]$y3, [float]$x4, [float]$y4)
  }

  $pen.Dispose()
}

function Draw-Corner-Ornaments {
  param(
    [System.Drawing.Graphics]$G,
    [int]$Width,
    [int]$Height
  )
  $gold = [System.Drawing.Color]::FromArgb(220, 230, 185, 90)
  $pen = [System.Drawing.Pen]::new($gold, 4.0)
  $size = 120
  $offset = 44

  $G.DrawArc($pen, $offset, $offset, $size, $size, 180, 90)
  $G.DrawArc($pen, $Width - $offset - $size, $offset, $size, $size, 270, 90)
  $G.DrawArc($pen, $offset, $Height - $offset - $size, $size, $size, 90, 90)
  $G.DrawArc($pen, $Width - $offset - $size, $Height - $offset - $size, $size, $size, 0, 90)

  $pen.Dispose()
}

function Draw-Edge-Runes {
  param(
    [System.Drawing.Graphics]$G,
    [int]$Width,
    [int]$Height,
    [int]$Inset,
    [System.Drawing.Color]$Color
  )
  $pen = [System.Drawing.Pen]::new($Color, 2.0)
  $step = 36
  $topY = $Inset + 8
  $bottomY = $Height - $Inset - 8
  for ($x = $Inset + 12; $x -le ($Width - $Inset - 12); $x += $step) {
    $G.DrawLine($pen, [float]$x, [float]$topY, [float]($x + 10), [float]($topY + 6))
    $G.DrawLine($pen, [float]$x, [float]$bottomY, [float]($x + 10), [float]($bottomY - 6))
  }
  $pen.Dispose()
}

function Draw-Center-Glow {
  param(
    [System.Drawing.Graphics]$G,
    [int]$Width,
    [int]$Height,
    [System.Drawing.Color]$Color
  )
  $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $path.AddEllipse($Width * 0.15, $Height * 0.18, $Width * 0.7, $Height * 0.64)
  $pg = [System.Drawing.Drawing2D.PathGradientBrush]::new($path)
  $pg.CenterColor = [System.Drawing.Color]::FromArgb(120, $Color.R, $Color.G, $Color.B)
  $pg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 10, 6, 20))
  $rect = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
  $G.FillRectangle($pg, $rect)
  $pg.Dispose()
  $path.Dispose()
}

function Draw-Emblem {
  param(
    [System.Drawing.Graphics]$G,
    [int]$Width,
    [int]$Height,
    [string]$Variant,
    [System.Drawing.Color]$Color
  )
  $cx = $Width / 2.0
  $cy = $Height / 2.0
  $pen = [System.Drawing.Pen]::new($Color, 5.0)
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(120, $Color.R, $Color.G, $Color.B))

  switch ($Variant) {
    'sword' {
      $G.DrawLine($pen, [float]$cx, [float]($cy - 150), [float]$cx, [float]($cy + 120))
      $G.DrawLine($pen, [float]($cx - 60), [float]$cy, [float]($cx + 60), [float]$cy)
      $G.FillEllipse($brush, [float]($cx - 24), [float]($cy + 110), 48, 48)
    }
    'crystal' {
      $points = @(
        [System.Drawing.PointF]::new([float]$cx, [float]($cy - 140)),
        [System.Drawing.PointF]::new([float]($cx + 90), [float]$cy),
        [System.Drawing.PointF]::new([float]$cx, [float]($cy + 140)),
        [System.Drawing.PointF]::new([float]($cx - 90), [float]$cy)
      )
      $G.DrawPolygon($pen, $points)
      $G.FillPolygon($brush, $points)
    }
    'eye' {
      $G.DrawEllipse($pen, [float]($cx - 140), [float]($cy - 80), 280, 160)
      $G.FillEllipse($brush, [float]($cx - 50), [float]($cy - 50), 100, 100)
      $G.FillEllipse([System.Drawing.Brushes]::Black, [float]($cx - 18), [float]($cy - 18), 36, 36)
    }
    'crown' {
      $points = @(
        [System.Drawing.PointF]::new([float]($cx - 120), [float]($cy + 80)),
        [System.Drawing.PointF]::new([float]($cx - 80), [float]($cy - 60)),
        [System.Drawing.PointF]::new([float]($cx - 20), [float]($cy + 20)),
        [System.Drawing.PointF]::new([float]$cx, [float]($cy - 90)),
        [System.Drawing.PointF]::new([float]($cx + 20), [float]($cy + 20)),
        [System.Drawing.PointF]::new([float]($cx + 80), [float]($cy - 60)),
        [System.Drawing.PointF]::new([float]($cx + 120), [float]($cy + 80))
      )
      $G.DrawLines($pen, $points)
      $G.FillPolygon($brush, $points)
    }
    default {
      $G.DrawArc($pen, [float]($cx - 140), [float]($cy - 140), 280, 280, 20, 300)
      $G.DrawEllipse($pen, [float]($cx - 80), [float]($cy - 80), 160, 160)
    }
  }

  $pen.Dispose()
  $brush.Dispose()
}

function Save-Png {
  param(
    [System.Drawing.Bitmap]$Bmp,
    [string]$Path
  )
  $Bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Build-Board {
  param([string]$Path)
  $seed = 4201
  $rng = [System.Random]::new($seed)
  $size = @(1378, 1204)
  $colors = @(
    [System.Drawing.Color]::FromArgb(255, 12, 8, 22),
    [System.Drawing.Color]::FromArgb(255, 45, 18, 60)
  )
  $canvas = New-Canvas -Width $size[0] -Height $size[1] -ColorA $colors[0] -ColorB $colors[1]
  $bmp = $canvas[0]
  $g = $canvas[1]

  Add-Starfield -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 900 -Color ([System.Drawing.Color]::FromArgb(120, 180, 200, 255))
  Add-Dust -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 420
  Add-SwirlArcs -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 18 -Color ([System.Drawing.Color]::FromArgb(220, 140, 220, 255))

  $ringColor = [System.Drawing.Color]::FromArgb(120, 200, 170, 90)
  Draw-RuneRing -G $g -Width $size[0] -Height $size[1] -Radius 380 -Count 54 -Color $ringColor
  Draw-RuneRing -G $g -Width $size[0] -Height $size[1] -Radius 520 -Count 80 -Color ([System.Drawing.Color]::FromArgb(90, 140, 200, 210))

  $glowRect = [System.Drawing.Rectangle]::new(0, 0, $size[0], $size[1])
  $glowBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($glowRect,
    [System.Drawing.Color]::FromArgb(120, 255, 190, 120),
    [System.Drawing.Color]::FromArgb(0, 20, 10, 30),
    0.0)
  $g.FillRectangle($glowBrush, $glowRect)
  $glowBrush.Dispose()

  Save-Png -Bmp $bmp -Path $Path
  $g.Dispose()
  $bmp.Dispose()
}

function Build-CardBack {
  param([string]$Path)
  $seed = 4202
  $rng = [System.Random]::new($seed)
  $size = @(640, 880)
  $canvas = New-Canvas -Width $size[0] -Height $size[1] -ColorA ([System.Drawing.Color]::FromArgb(255, 10, 10, 24)) -ColorB ([System.Drawing.Color]::FromArgb(255, 50, 20, 70))
  $bmp = $canvas[0]
  $g = $canvas[1]

  Draw-Frame -G $g -Width $size[0] -Height $size[1] -Inset 28
  Add-Starfield -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 320 -Color ([System.Drawing.Color]::FromArgb(150, 230, 210, 255))
  Add-Dust -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 200
  Draw-Edge-Runes -G $g -Width $size[0] -Height $size[1] -Inset 34 -Color ([System.Drawing.Color]::FromArgb(140, 240, 210, 140))
  Draw-RuneRing -G $g -Width $size[0] -Height $size[1] -Radius 170 -Count 36 -Color ([System.Drawing.Color]::FromArgb(160, 230, 200, 120))
  Draw-Center-Glow -G $g -Width $size[0] -Height $size[1] -Color ([System.Drawing.Color]::FromArgb(255, 200, 160, 90))
  Draw-Emblem -G $g -Width $size[0] -Height $size[1] -Variant 'crown' -Color ([System.Drawing.Color]::FromArgb(200, 255, 210, 140))
  Draw-Corner-Ornaments -G $g -Width $size[0] -Height $size[1]

  Save-Png -Bmp $bmp -Path $Path
  $g.Dispose()
  $bmp.Dispose()
}

function Build-CardFace {
  param(
    [string]$Path,
    [int]$Seed,
    [string]$Variant,
    [System.Drawing.Color]$Accent
  )
  $rng = [System.Random]::new($Seed)
  $size = @(1024, 1024)
  $canvas = New-Canvas -Width $size[0] -Height $size[1] -ColorA ([System.Drawing.Color]::FromArgb(255, 12, 9, 20)) -ColorB ([System.Drawing.Color]::FromArgb(255, 44, 22, 50))
  $bmp = $canvas[0]
  $g = $canvas[1]

  Draw-Frame -G $g -Width $size[0] -Height $size[1] -Inset 44
  Add-Starfield -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 520 -Color ([System.Drawing.Color]::FromArgb(160, 230, 240, 255))
  Add-Dust -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 340
  Add-SwirlArcs -G $g -Rng $rng -Width $size[0] -Height $size[1] -Count 18 -Color $Accent

  Draw-RuneRing -G $g -Width $size[0] -Height $size[1] -Radius 340 -Count 72 -Color ([System.Drawing.Color]::FromArgb(180, $Accent.R, $Accent.G, $Accent.B))
  Draw-Edge-Runes -G $g -Width $size[0] -Height $size[1] -Inset 56 -Color ([System.Drawing.Color]::FromArgb(140, 240, 210, 140))
  Draw-Center-Glow -G $g -Width $size[0] -Height $size[1] -Color $Accent
  Draw-Emblem -G $g -Width $size[0] -Height $size[1] -Variant $Variant -Color ([System.Drawing.Color]::FromArgb(230, $Accent.R, $Accent.G, $Accent.B))
  Draw-Corner-Ornaments -G $g -Width $size[0] -Height $size[1]

  Save-Png -Bmp $bmp -Path $Path
  $g.Dispose()
  $bmp.Dispose()
}

Build-Board -Path $boardPath
Build-CardBack -Path $cardBackPath

$variants = @(
  @{ path = $cardFacePaths[0]; seed = 4311; variant = 'sword'; accent = [System.Drawing.Color]::FromArgb(255, 220, 190, 110) },
  @{ path = $cardFacePaths[1]; seed = 4312; variant = 'crystal'; accent = [System.Drawing.Color]::FromArgb(255, 150, 220, 255) },
  @{ path = $cardFacePaths[2]; seed = 4313; variant = 'eye'; accent = [System.Drawing.Color]::FromArgb(255, 230, 140, 150) },
  @{ path = $cardFacePaths[3]; seed = 4314; variant = 'crown'; accent = [System.Drawing.Color]::FromArgb(255, 200, 160, 255) },
  @{ path = $cardFacePaths[4]; seed = 4315; variant = 'swirl'; accent = [System.Drawing.Color]::FromArgb(255, 170, 230, 190) }
)

foreach ($item in $variants) {
  Build-CardFace -Path $item.path -Seed $item.seed -Variant $item.variant -Accent $item.accent
}
