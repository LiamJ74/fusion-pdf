# Fusion PDF - DETECTION Intune
#
# Exit 0 + une ligne = installe | Exit 1 = absent ou incomplet
#
# Installe = les DEUX :
#   - C:\Program Files\Fusion PDF\FusionPDF.exe, en version
#     $VersionAttendue ou plus recente ;
#   - le clic droit sur un dossier ET sur le fond d'un dossier, qui pointent
#     vers cet exe.
# L'exe seul ne suffit pas : sans le clic droit, la moitie de l'usage manque.
#
# $VersionAttendue EST LA SOURCE DE LA VERSION : Build-FusionPDF.ps1 la lit
# et l'inscrit dans l'exe. Monter de version = changer cette ligne, puis
# recompiler et regenerer le paquet.
#
# UNE SEULE LIGNE DE SORTIE (Intune ne garde que la derniere).
#
# Registre lu en vue 64 bits explicite : correct quel que soit le
# PowerShell qui execute ce script.
#
# PowerShell 5.1, ASCII strict, contexte SYSTEME.

$VersionAttendue = '1.2.0.0'

$racinePF = $env:ProgramW6432
if (-not $racinePF) { $racinePF = $env:ProgramFiles }
$Exe = Join-Path $racinePF 'Fusion PDF\FusionPDF.exe'

$manque = @()
$version = ''

if (-not (Test-Path -LiteralPath $Exe)) {
    $manque += 'exe absent'
} else {
    $version = "$((Get-Item -LiteralPath $Exe).VersionInfo.FileVersion)".Trim()
    $ok = $false
    try { $ok = ([version]$version -ge [version]$VersionAttendue) } catch { }
    if (-not $ok) { $manque += "version $version < $VersionAttendue" }
}

$hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
foreach ($cle in @('SOFTWARE\Classes\Directory\shell\FusionPDF\command',
                   'SOFTWARE\Classes\Directory\Background\shell\FusionPDF\command')) {
    $k = $hklm.OpenSubKey($cle)
    $val = $null
    if ($k) { $val = $k.GetValue(''); $k.Close() }
    if (-not $val) { $manque += "cle absente $($cle -replace '^SOFTWARE\\Classes\\','' -replace '\\command$','')" }
    elseif ($val.IndexOf($Exe, [StringComparison]::OrdinalIgnoreCase) -lt 0) { $manque += "cle $($cle.Split('\')[3]) vers un autre exe" }
}
$hklm.Close()

if ($manque.Count) {
    Write-Output ('Fusion PDF incomplet : ' + ($manque -join ' ## '))
    exit 1
}
Write-Output "Fusion PDF $version installe ## clic droit dossier et fond de dossier OK"
exit 0
