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
$mults  = @('1x','2x','5x','0.5x','2x','5x','10x','20x')
$pcols  = @('#4a4a5e','#3f5f7a','#4a7a4a','#8a2f2f','#3f5f7a','#4a7a4a','#a8842a','#e0b52e')
$px0 = 148.0; $pw = 104.0; $py = 896.0; $ph = 86.0
for ($i = 0; $i -lt 8; $i++) {
    $x = $px0 + $pw * $i
    FillR $pcols[$i] $x $py ($pw - 4) $ph
    FillR '#0b0b14' $x $py ($pw - 4) 5
    $tc = '#ffffff'
    if ($i -eq 7) { $tc = '#1a1200' }
    Txt $mults[$i] ($x + $pw/2 - 2) ($py + 26) 26 $tc 'c'
}

# ---------- 發射器 ----------
Circ '#101018' 84 938 60
Circ '#e05a5a' 84 938 54
Circ '#ffd0d0' 84 938 20
Txt '發射器' 152 918 19 '#ff9a9a' 'l'

# ---------- 瞄準圓錐 ----------
$penCone = New-Object System.Drawing.Pen -ArgumentList (Col '#e05a5a'), 2
$penCone.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawLine($penCone, 84, 882, 40, 640)
$g.DrawLine($penCone, 84, 882, 330, 640)
$g.DrawArc($penCone, 84 - 250, 882 - 250, 500, 500, 230, 88)
$penCone.Dispose()

# ---------- 分界擋板（中間有缺口） ----------
$div = '#6d6d9c'
FillR $div 486 150 15 410
FillR $div 486 690 15 172
# 缺口標示
$penGap = New-Object System.Drawing.Pen -ArgumentList (Col '#ffcc44'), 2
$penGap.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dot
$g.DrawRectangle($penGap, 470, 560, 47, 130)
$penGap.Dispose()
Txt '缺口' 494 608 17 '#ffcc44' 'c'

# ---------- 釘子 ----------
# 左區：密集叢集
for ($ry = 92.0; $ry -le 856.0; $ry += 58.0) {
    $odd = [int](($ry - 92.0) / 58.0) % 2
    for ($x = 38.0 + $odd * 29.0; $x -le 470.0; $x += 58.0) {
        $dx = $x - 84.0; $dy = $ry - 878.0
        if (($dx*$dx + $dy*$dy) -lt 7000) { continue }
        Circ '#d8d8e8' $x $ry 9
    }
}
# 右區：空曠快速區
for ($ry = 96.0; $ry -le 856.0; $ry += 122.0) {
    $odd = [int](($ry - 96.0) / 122.0) % 2
    for ($x = 524.0 + $odd * 61.0; $x -le 966.0; $x += 122.0) {
        Circ '#d8d8e8' $x $ry 15
    }
}

# ---------- 範例球路 ----------
$penT1 = New-Object System.Drawing.Pen -ArgumentList (Col '#5ac8ff'), 4
$penT1.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$p1 = @(
    (New-Object System.Drawing.PointF -ArgumentList 84.0, 878.0),
    (New-Object System.Drawing.PointF -ArgumentList 52.0, 700.0),
    (New-Object System.Drawing.PointF -ArgumentList 78.0, 480.0),
    (New-Object System.Drawing.PointF -ArgumentList 165.0, 360.0),
    (New-Object System.Drawing.PointF -ArgumentList 262.0, 470.0),
    (New-Object System.Drawing.PointF -ArgumentList 285.0, 700.0),
    (New-Object System.Drawing.PointF -ArgumentList 240.0, 878.0)
)
$g.DrawCurve($penT1, $p1)
$penT1.Dispose()

$penT2 = New-Object System.Drawing.Pen -ArgumentList (Col '#ffd24a'), 4
$penT2.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$p2 = @(
    (New-Object System.Drawing.PointF -ArgumentList 84.0, 878.0),
    (New-Object System.Drawing.PointF -ArgumentList 230.0, 760.0),
    (New-Object System.Drawing.PointF -ArgumentList 390.0, 620.0),
    (New-Object System.Drawing.PointF -ArgumentList 493.0, 600.0),
    (New-Object System.Drawing.PointF -ArgumentList 640.0, 480.0),
    (New-Object System.Drawing.PointF -ArgumentList 820.0, 540.0),
    (New-Object System.Drawing.PointF -ArgumentList 905.0, 878.0)
)
$g.DrawCurve($penT2, $p2)
$penT2.Dispose()

# ---------- 區域標籤 ----------
Txt '左半：密集叢集' 250 40 24 '#8fd0ff' 'c'
Txt '球彈很久 → 累積多' 250 76 19 '#8fd0ff' 'c'
Txt '但出口是低倍率袋' 250 102 19 '#8fd0ff' 'c'

Txt '右半：空曠快速區' 730 40 24 '#ffd24a' 'c'
Txt '球直接衝下去 → 累積少' 730 76 19 '#ffd24a' 'c'
Txt '但出口是高倍率袋' 730 102 19 '#ffd24a' 'c'

# ---------- 圖例 ----------
FillR '#0b0b14' 20 820 180 62
Circ '#d8d8e8' 38 836 9
Txt '釘子' 56 826 17 '#c0c0d0' 'l'
$penL = New-Object System.Drawing.Pen -ArgumentList (Col '#5ac8ff'), 3
$penL.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawLine($penL, 24, 866, 52, 866); $penL.Dispose()
Txt '陡射→左→1x' 56 856 17 '#8fd0ff' 'l'
$penL2 = New-Object System.Drawing.Pen -ArgumentList (Col '#ffd24a'), 3
$penL2.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawLine($penL2, 118, 866, 146, 866); $penL2.Dispose()
Txt '平射→右→20x' 150 856 17 '#ffd24a' 'l'

$g.Dispose()
$out = Join-Path (Get-Location) 'Docs\board-layout-01.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved: $out"
