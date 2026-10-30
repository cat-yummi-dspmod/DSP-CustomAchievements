# 生成 256x256 的 icon.png：深蓝底上一枚金色五角星奖章

Add-Type -AssemblyName System.Drawing

$Size = 256
$Output = Join-Path $PSScriptRoot "icon.png"

$bmp = New-Object System.Drawing.Bitmap $Size, $Size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$rect = New-Object System.Drawing.Rectangle 8, 8, 240, 240
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ($rect,
    [System.Drawing.Color]::FromArgb(255, 30, 60, 110),
    [System.Drawing.Color]::FromArgb(255, 8, 16, 40), 90)
$g.FillEllipse($bg, $rect)
$ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 200, 80)), 10
$g.DrawEllipse($ring, 13, 13, 230, 230)

$points = New-Object 'System.Drawing.PointF[]' 10
for ($i = 0; $i -lt 10; $i++) {
    $r = if ($i % 2 -eq 0) { 92 } else { 38 }
    $a = -[Math]::PI / 2 + $i * [Math]::PI / 5
    $points[$i] = New-Object System.Drawing.PointF ([float](128 + $r * [Math]::Cos($a))), ([float](134 + $r * [Math]::Sin($a)))
}
$star = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.PointF 0, 40), (New-Object System.Drawing.PointF 0, 220),
    [System.Drawing.Color]::FromArgb(255, 255, 240, 150),
    [System.Drawing.Color]::FromArgb(255, 255, 150, 20))
$g.FillPolygon($star, $points)
$edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 90, 50, 0)), 4
$edge.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawPolygon($edge, $points)

$bmp.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
$edge.Dispose(); $star.Dispose(); $ring.Dispose(); $bg.Dispose(); $g.Dispose(); $bmp.Dispose()
Write-Host "Icon created: $Output" -ForegroundColor Green
