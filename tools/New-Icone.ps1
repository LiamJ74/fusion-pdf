# Genere l'icone (FusionPDF.ico, 9 tailles) et les tuiles 128 et 512 px.
#
#   -Logo <png>  : le logo, en blanc, sur une tuile primaire #3b82f6 -> #1d4ed8
#   sans -Logo   : icone NEUTRE (version anonymisee) : deux pages blanches
#                  superposees, la seconde avec des lignes de texte.
#
# PowerShell 5.1 ou 7, ASCII.
param([string]$Logo = '', [Parameter(Mandatory)][string]$Dest)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $Dest | Out-Null
$src = $null
if ($Logo) { $src = [Drawing.Bitmap]::FromFile($Logo) }

function New-Arrondi([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $d = 2 * $r
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90); $p.CloseFigure()
    return $p
}

function New-Tuile([int]$t) {
    $bmp = New-Object Drawing.Bitmap $t, $t, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $fond = New-Arrondi 0 0 ($t - 1) ($t - 1) ([math]::Max(2, $t * 0.22))
    $br = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Point 0,0), (New-Object Drawing.Point $t,$t), ([Drawing.Color]::FromArgb(255,0x3B,0x82,0xF6)), ([Drawing.Color]::FromArgb(255,0x1D,0x4E,0xD8))
    $g.FillPath($br, $fond)

    if ($src) {
        # logo en blanc : matrice de couleur qui garde l'alpha et force RVB a 1
        $cm = New-Object Drawing.Imaging.ColorMatrix
        $cm.Matrix00 = 0; $cm.Matrix11 = 0; $cm.Matrix22 = 0; $cm.Matrix40 = 1; $cm.Matrix41 = 1; $cm.Matrix42 = 1
        $ia = New-Object Drawing.Imaging.ImageAttributes; $ia.SetColorMatrix($cm)
        $m = [int]($t * 0.2); $w = $t - 2 * $m
        $g.DrawImage($src, (New-Object Drawing.Rectangle $m, $m, $w, $w), 0, 0, $src.Width, $src.Height, 'Pixel', $ia)
    } else {
        # deux pages : celle de derriere en blanc translucide, decalee
        $pw = $t * 0.42; $ph = $t * 0.52; $rr = [math]::Max(1, $t * 0.05)
        $arriere = New-Arrondi ($t * 0.22) ($t * 0.17) $pw $ph $rr
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(115, 255, 255, 255))), $arriere)
        $avant = New-Arrondi ($t * 0.36) ($t * 0.31) $pw $ph $rr
        $g.FillPath([Drawing.Brushes]::White, $avant)
        if ($t -ge 24) {
            $ligne = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 0x93, 0xC5, 0xFD))
            $lh = [math]::Max(1, $t * 0.035)
            foreach ($k in 0..3) {
                $lw = if ($k -eq 3) { $pw * 0.4 } else { $pw * 0.62 }
                $g.FillRectangle($ligne, [float]($t * 0.36 + $pw * 0.19), [float]($t * 0.31 + $ph * (0.24 + 0.17 * $k)), [float]$lw, [float]$lh)
            }
        }
    }
    $g.Dispose(); return $bmp
}

$tailles = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($t in $tailles) { $b = New-Tuile $t; $ms = New-Object IO.MemoryStream; $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); ,$ms.ToArray() }
$ico = New-Object IO.MemoryStream; $bw = New-Object IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$tailles.Count)
$off = 6 + 16 * $tailles.Count
for ($i = 0; $i -lt $tailles.Count; $i++) {
    $t = $tailles[$i]; $v = if ($t -ge 256) { 0 } else { $t }
    $bw.Write([byte]$v); $bw.Write([byte]$v); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$off); $off += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $Dest 'FusionPDF.ico'), $ico.ToArray())
foreach ($t in 128, 512) { $b = New-Tuile $t; $b.Save((Join-Path $Dest "tuile-$t.png"), [Drawing.Imaging.ImageFormat]::Png); $b.Dispose() }
if ($src) { $src.Dispose() }
Get-ChildItem -LiteralPath $Dest | Select-Object Name, Length
