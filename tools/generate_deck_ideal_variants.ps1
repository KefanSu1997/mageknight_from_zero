param(
    [string]$OutputRoot = "Assets/UI/Images/DeckTheme/Ideal/Candidates",
    [int]$SeedBase = 1200
)

Add-Type -AssemblyName System.Drawing

function Convert-HexToColor {
    param([string]$Hex, [int]$Alpha = 255)
    $clean = $Hex.TrimStart("#")
    $r = [Convert]::ToInt32($clean.Substring(0, 2), 16)
    $g = [Convert]::ToInt32($clean.Substring(2, 2), 16)
    $b = [Convert]::ToInt32($clean.Substring(4, 2), 16)
    return [System.Drawing.Color]::FromArgb($Alpha, $r, $g, $b)
}

function New-RadialGlow {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Rectangle]$Rect,
        [System.Drawing.Color]$Inner,
        [System.Drawing.Color]$Outer,
        [float]$Shrink = 0.1
    )
    $insetX = [int]($Rect.Width * $Shrink)
    $insetY = [int]($Rect.Height * $Shrink)
    $ellipseRect = [System.Drawing.Rectangle]::new(
        [int]($Rect.X + $insetX),
        [int]($Rect.Y + $insetY),
        [int]($Rect.Width - 2 * $insetX),
        [int]($Rect.Height - 2 * $insetY)
    )
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($ellipseRect)
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $brush.CenterColor = $Inner
    $brush.SurroundColors = @($Outer)
    $Graphics.FillEllipse($brush, $ellipseRect)
    $brush.Dispose()
    $path.Dispose()
}

function Add-StarField {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Random]$Random,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$BaseColor,
        [int]$Count = 600
    )
    for ($i = 0; $i -lt $Count; $i++) {
        $x = $Random.Next(0, $Width)
        $y = $Random.Next(0, $Height)
        $size = $Random.Next(1, 3)
        $alpha = $Random.Next(120, 230)
        $starColor = [System.Drawing.Color]::FromArgb($alpha, $BaseColor.R, $BaseColor.G, $BaseColor.B)
        $brush = New-Object System.Drawing.SolidBrush($starColor)
        $Graphics.FillEllipse($brush, $x, $y, $size, $size)
        $brush.Dispose()
    }
}

function Add-SwirlLines {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Random]$Random,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$LineColor,
        [int]$Count = 6
    )
    for ($i = 0; $i -lt $Count; $i++) {
        $alpha = $Random.Next(40, 90)
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($alpha, $LineColor.R, $LineColor.G, $LineColor.B), ($Random.NextDouble() * 2 + 1))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $p1 = New-Object System.Drawing.Point([int]($Random.NextDouble() * $Width * 0.3), [int]($Random.NextDouble() * $Height))
        $p2 = New-Object System.Drawing.Point([int]($Width * (0.3 + $Random.NextDouble() * 0.2)), [int]($Height * (0.1 + $Random.NextDouble() * 0.3)))
        $p3 = New-Object System.Drawing.Point([int]($Width * (0.6 + $Random.NextDouble() * 0.2)), [int]($Height * (0.6 + $Random.NextDouble() * 0.3)))
        $p4 = New-Object System.Drawing.Point([int]($Width * (0.7 + $Random.NextDouble() * 0.3)), [int]($Random.NextDouble() * $Height))
        $Graphics.DrawBezier($pen, $p1, $p2, $p3, $p4)
        $pen.Dispose()
    }
}

function Add-OrnateRing {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Color
    )
    $radius = [Math]::Min($Width, $Height) * 0.32
    $centerX = $Width * 0.5
    $centerY = $Height * 0.5
    $rect = [System.Drawing.Rectangle]::new(
        [int]($centerX - $radius),
        [int]($centerY - $radius),
        [int]($radius * 2),
        [int]($radius * 2)
    )
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, $Color.R, $Color.G, $Color.B), 4)
    $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $Graphics.DrawEllipse($pen, $rect)
    $pen.Dispose()
}

function Add-Emblem {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Color
    )
    $centerX = $Width * 0.5
    $centerY = $Height * 0.5
    $outer = [Math]::Min($Width, $Height) * 0.34
    $inner = $outer * 0.55
    $outerRect = [System.Drawing.Rectangle]::new(
        [int]($centerX - $outer),
        [int]($centerY - $outer),
        [int]($outer * 2),
        [int]($outer * 2)
    )
    $innerRect = [System.Drawing.Rectangle]::new(
        [int]($centerX - $inner),
        [int]($centerY - $inner),
        [int]($inner * 2),
        [int]($inner * 2)
    )
    $penOuter = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(170, $Color.R, $Color.G, $Color.B), 6)
    $penInner = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140, $Color.R, $Color.G, $Color.B), 3)
    $Graphics.DrawEllipse($penOuter, $outerRect)
    $Graphics.DrawEllipse($penInner, $innerRect)
    $penOuter.Dispose()
    $penInner.Dispose()
}

function New-VariantImage {
    param(
        [int]$Width,
        [int]$Height,
        [hashtable]$Palette,
        [int]$Seed,
        [string]$OutputPath,
        [string]$Style
    )
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $rect = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $Palette.Base1, $Palette.Base2, 35)
    $graphics.FillRectangle($gradient, $rect)
    $gradient.Dispose()

    New-RadialGlow -Graphics $graphics -Rect $rect -Inner $Palette.Glow -Outer ([System.Drawing.Color]::FromArgb(10, $Palette.Glow.R, $Palette.Glow.G, $Palette.Glow.B)) -Shrink 0.15

    $rng = New-Object System.Random $Seed
    Add-StarField -Graphics $graphics -Random $rng -Width $Width -Height $Height -BaseColor $Palette.Star -Count $Palette.StarCount
    Add-SwirlLines -Graphics $graphics -Random $rng -Width $Width -Height $Height -LineColor $Palette.Accent -Count $Palette.SwirlCount

    switch ($Style) {
        "Board" {
            $overlayColor = [System.Drawing.Color]::FromArgb(60, $Palette.Accent.R, $Palette.Accent.G, $Palette.Accent.B)
            $pen = New-Object System.Drawing.Pen($overlayColor, 3)
            $graphics.DrawArc($pen, 80, 80, $Width - 160, $Height - 160, 210, 120)
            $graphics.DrawArc($pen, 120, 140, $Width - 240, $Height - 280, 20, 120)
            $pen.Dispose()
        }
        "CardBack" {
            Add-Emblem -Graphics $graphics -Width $Width -Height $Height -Color $Palette.Accent
            $borderRect = [System.Drawing.Rectangle]::new(10, 10, ($Width - 20), ($Height - 20))
            $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140, $Palette.Star.R, $Palette.Star.G, $Palette.Star.B), 4)
            $graphics.DrawRectangle($borderPen, $borderRect)
            $borderPen.Dispose()
        }
        "CardFace" {
            Add-OrnateRing -Graphics $graphics -Width $Width -Height $Height -Color $Palette.Accent
            $haloColor = [System.Drawing.Color]::FromArgb(80, $Palette.Glow.R, $Palette.Glow.G, $Palette.Glow.B)
            $haloPen = New-Object System.Drawing.Pen($haloColor, 2)
            $graphics.DrawEllipse($haloPen, 120, 120, $Width - 240, $Height - 240)
            $haloPen.Dispose()
        }
    }

    $directory = Split-Path -Parent $OutputPath
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

$variants = @(
    @{
        Name = "a"
        Base1 = Convert-HexToColor "#0B0F24"
        Base2 = Convert-HexToColor "#2A124A"
        Glow = Convert-HexToColor "#3C68B8" 180
        Accent = Convert-HexToColor "#F2C47E"
        Star = Convert-HexToColor "#CFE9FF"
        StarCount = 800
        SwirlCount = 7
    },
    @{
        Name = "b"
        Base1 = Convert-HexToColor "#0A1B1F"
        Base2 = Convert-HexToColor "#221B3A"
        Glow = Convert-HexToColor "#5A6BFF" 170
        Accent = Convert-HexToColor "#C88BFF"
        Star = Convert-HexToColor "#F4E5FF"
        StarCount = 720
        SwirlCount = 6
    },
    @{
        Name = "c"
        Base1 = Convert-HexToColor "#160B16"
        Base2 = Convert-HexToColor "#0B1020"
        Glow = Convert-HexToColor "#7FB8FF" 160
        Accent = Convert-HexToColor "#E9D6A5"
        Star = Convert-HexToColor "#FFEED6"
        StarCount = 680
        SwirlCount = 6
    }
)

foreach ($variant in $variants) {
    $suffix = $variant.Name
    New-VariantImage -Width 1378 -Height 1204 -Palette $variant -Seed ($SeedBase + 11) -OutputPath (Join-Path $OutputRoot ("Board/deck_ideal_board_variant_{0}.png" -f $suffix)) -Style "Board"
    New-VariantImage -Width 640 -Height 880 -Palette $variant -Seed ($SeedBase + 21) -OutputPath (Join-Path $OutputRoot ("CardBack/deck_ideal_card_back_variant_{0}.png" -f $suffix)) -Style "CardBack"
    New-VariantImage -Width 1024 -Height 1024 -Palette $variant -Seed ($SeedBase + 31) -OutputPath (Join-Path $OutputRoot ("CardFace/deck_ideal_card_face_variant_{0}.png" -f $suffix)) -Style "CardFace"
    $SeedBase += 5
}

Write-Output "Deck ideal variants generated under $OutputRoot"
