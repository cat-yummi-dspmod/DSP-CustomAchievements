# 生成 res\default_icon.png：没有提供图标的成就使用的白色五角星线稿

Add-Type -AssemblyName System.Drawing

$Size = 256
$OutDir = Join-Path $PSScriptRoot "res"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$bmp = New-Object System.Drawing.Bitmap $Size, $Size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 10
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawEllipse($pen, 14, 14, 228, 228)

$points = New-Object 'System.Drawing.PointF[]' 10
for ($i = 0; $i -lt 10; $i++) {
    $r = if ($i % 2 -eq 0) { 82 } else { 34 }
    $a = -[Math]::PI / 2 + $i * [Math]::PI / 5
    $points[$i] = New-Object System.Drawing.PointF ([float](128 + $r * [Math]::Cos($a))), ([float](134 + $r * [Math]::Sin($a)))
}
$g.DrawPolygon($pen, $points)

$out = Join-Path $OutDir "default_icon.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$pen.Dispose(); $g.Dispose(); $bmp.Dispose()
Write-Host "Icon: $out"
