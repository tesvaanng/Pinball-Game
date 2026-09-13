Add-Type -AssemblyName System.Drawing

$W = 1000; $H = 1000
$bmp = New-Object System.Drawing.Bitmap -ArgumentList $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

function Col([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function FillR([string]$hex, [float]$x, [float]$y, [float]$w, [float]$h) {
    $b = New-Object System.Drawing.SolidBrush -ArgumentList (Col $hex)
    $g.FillRectangle($b, $x, $y, $w, $h); $b.Dispose()
}
function Circ([string]$hex, [float]$cx, [float]$cy, [float]$r) {
    $b = New-Object System.Drawing.SolidBrush -ArgumentList (Col $hex)
    $g.FillEllipse($b, $cx - $r, $cy - $r, 2*$r, 2*$r); $b.Dispose()
}
function Txt([string]$s, [float]$x, [float]$y, [float]$size, [string]$hex, [string]$align) {
    $f = New-Object System.Drawing.Font -ArgumentList 'Microsoft JhengHei', $size
    $b = New-Object System.Drawing.SolidBrush -ArgumentList (Col $hex)
    $sf = New-Object System.Drawing.StringFormat
    if ($align -eq 'c') { $sf.Alignment = [System.Drawing.StringAlignment]::Center }
    if ($align -eq 'r') { $sf.Alignment = [System.Drawing.StringAlignment]::Far }
    $g.DrawString($s, $f, $b, $x, $y, $sf)
    $f.Dispose(); $b.Dispose(); $sf.Dispose()
}

# ---------- 背景 ----------
FillR '#0b0b14' 0 0 $W $H
FillR '#14141f' 18 18 964 964

# ---------- 牆 ----------
$wall = '#3d3d5c'
FillR $wall 0 0 18 1000
FillR $wall 982 0 18 1000
FillR $wall 18 0 964 18

# ---------- 底排袋口 ----------
$mults = @('20x','10x','5x','1x','1x','5x','10x','20x')
$pcols = @('#e0b52e','#a8842a','#4a7a4a','#4a4a5e','#4a4a5e','#4a7a4a','#a8842a','#e0b52e')
$px0 = 148.0; $pw = 104.0; $py = 896.0; $ph = 86.0
for ($i = 0; $i -lt 8; $i++) {
    $x = $px0 + $pw * $i
    FillR $pcols[$i] $x $py ($pw - 4) $ph
    FillR '#0b0b14' $x $py ($pw - 4) 5
    $tc = '#ffffff'; if ($i -eq 0 -or $i -eq 7) { $tc = '#1a1200' }
    Txt $mults[$i] ($x + $pw/2 - 2) ($py + 26) 26 $tc 'c'
}

# ---------- 頂部中央發射器 ----------
FillR '#101018' 440 40 120 78
FillR '#e05a5a' 448 48 104 66
Circ '#ffd0d0' 500 84 14
Txt '發射器' 500 128 19 '#ff9a9a' 'c'

# ---------- 飛行區 ----------
$penFly = New-Object System.Drawing.Pen -ArgumentList (Col '#3a3a55'), 2
$penFly.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawRectangle($penFly, 30, 165, 940, 165)
$penFly.Dispose()
Txt '空曠飛行區（無釘子）— 瞄準角度在此決定進場位置' 500 232 20 '#9a9ab8' 'c'

# ---------- 瞄準圓錐 ----------
$penCone = New-Object System.Drawing.Pen -ArgumentList (Col '#e05a5a'), 2
$penCone.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawLine($penCone, 500, 120, 190, 330)
$g.DrawLine($penCone, 500, 120, 810, 330)
$g.DrawArc($penCone, 500 - 400, 120 - 400, 800, 800, 55, 70)
$penCone.Dispose()

# ---------- 釘子 ----------
# 中央：密集叢集
for ($ry = 350.0; $ry -le 856.0; $ry += 54.0) {
    $odd = [int](($ry - 350.0) / 54.0) % 2
    for ($x = 336.0 + $odd * 27.0; $x -le 664.0; $x += 54.0) {
        Circ '#d8d8e8' $x $ry 9
    }
}
# 兩側：空曠快速區
for ($ry = 356.0; $ry -le 856.0; $ry += 124.0) {
    $odd = [int](($ry - 356.0) / 124.0) % 2
    for ($x = 62.0 + $odd * 62.0; $x -le 316.0; $x += 124.0) {
        Circ '#d8d8e8' $x $ry 15
    }
    for ($x = 684.0 + $odd * 62.0; $x -le 940.0; $x += 124.0) {
        Circ '#d8d8e8' $x $ry 15
    }
}

# ---------- 範例球路 ----------
$penT1 = New-Object System.Drawing.Pen -ArgumentList (Col '#5ac8ff'), 4
$penT1.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$p1 = @(
    (New-Object System.Drawing.PointF -ArgumentList 500.0, 120.0),
    (New-Object System.Drawing.PointF -ArgumentList 462.0, 250.0),
    (New-Object System.Drawing.PointF -ArgumentList 440.0, 380.0),
    (New-Object System.Drawing.PointF -ArgumentList 560.0, 520.0),
    (New-Object System.Drawing.PointF -ArgumentList 430.0, 660.0),
    (New-Object System.Drawing.PointF -ArgumentList 500.0, 780.0),
    (New-Object System.Drawing.PointF -ArgumentList 460.0, 878.0)
)
$g.DrawCurve($penT1, $p1)
$penT1.Dispose()

$penT2 = New-Object System.Drawing.Pen -ArgumentList (Col '#ffd24a'), 4
$penT2.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$p2 = @(
    (New-Object System.Drawing.PointF -ArgumentList 500.0, 120.0),
    (New-Object System.Drawing.PointF -ArgumentList 380.0, 220.0),
    (New-Object System.Drawing.PointF -ArgumentList 250.0, 320.0),
    (New-Object System.Drawing.PointF -ArgumentList 190.0, 480.0),
    (New-Object System.Drawing.PointF -ArgumentList 165.0, 640.0),
    (New-Object System.Drawing.PointF -ArgumentList 178.0, 780.0),
    (New-Object System.Drawing.PointF -ArgumentList 190.0, 878.0)
)
$g.DrawCurve($penT2, $p2)
$penT2.Dispose()

# ---------- 區域標籤 ----------
Txt '中央：密集叢集' 500 580 24 '#8fd0ff' 'c'
Txt '慢、累積多' 500 610 18 '#8fd0ff' 'c'
Txt '（但出口是中央低倍率袋）' 500 634 18 '#8fd0ff' 'c'

Txt '兩側：空曠快速區' 150 430 22 '#ffd24a' 'c'
Txt '快、累積少' 150 458 18 '#ffd24a' 'c'
Txt '（出口是高倍率袋）' 150 482 18 '#ffd24a' 'c'

Txt '兩側：空曠快速區' 850 430 22 '#ffd24a' 'c'
Txt '快、累積少' 850 458 18 '#ffd24a' 'c'
Txt '（出口是高倍率袋）' 850 482 18 '#ffd24a' 'c'

# ---------- 圖例 ----------
FillR '#0b0b14' 20 812 100 70
$penL = New-Object System.Drawing.Pen -ArgumentList (Col '#5ac8ff'), 3
$penL.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawLine($penL, 24, 838, 52, 838); $penL.Dispose()
Txt '陡射' 58 826 17 '#8fd0ff' 'l'
$penL2 = New-Object System.Drawing.Pen -ArgumentList (Col '#ffd24a'), 3
$penL2.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawLine($penL2, 24, 868, 52, 868); $penL2.Dispose()
Txt '斜射' 58 856 17 '#ffd24a' 'l'

$g.Dispose()
$out = Join-Path (Get-Location) 'Docs\board-layout-02.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved: $out"
