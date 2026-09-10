Add-Type -AssemblyName System.Drawing
$path = "D:\study_and_work\unity-MK-test\MageKnight_from_zero\vibe_coding\codex\_tmp_test_draw.png"
$bmp = New-Object System.Drawing.Bitmap(64, 64)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 30, 30, 40))
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 200, 200, 255), 2)
$g.DrawEllipse($pen, 8, 8, 48, 48)
$pen.Dispose()
$g.Dispose()
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
