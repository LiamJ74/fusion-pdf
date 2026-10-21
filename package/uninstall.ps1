# ============================================================
# Uninstall Intune Win32 App - Fusion PDF
#
# Retire l'exe, le raccourci du menu Demarrer public et les deux cles de
# clic droit, puis RELIT : exit 0 seulement si plus rien ne reste.
#
# qpdf n'est PAS desinstalle : c'est une application Intune distincte,
# qui peut servir a autre chose.
#
# Une fenetre Fusion PDF encore ouverte est fermee (attente bornee a
# 15 s), sinon l'exe ne pourrait pas etre supprime.
#
# Journal : C:\ProgramData\FusionPDF\FusionPDF-uninstall.log (%LOCALAPPDATA%\FusionPDF sans droits admin)
# PowerShell 5.1, ASCII strict, contexte SYSTEME.
# ============================================================

[CmdletBinding()]
param()

$NomCle  = 'FusionPDF'
$LogDir  = $(if (([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { 'C:\ProgramData\FusionPDF' } else { Join-Path $env:LOCALAPPDATA 'FusionPDF' })
$Journal = Join-Path $LogDir 'FusionPDF-uninstall.log'

$racinePF = $env:ProgramW6432
if (-not $racinePF) { $racinePF = $env:ProgramFiles }
$Dossier = Join-Path $racinePF 'Fusion PDF'
$Lnk     = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Fusion PDF.lnk'
$Cles    = @("SOFTWARE\Classes\Directory\shell\$NomCle", "SOFTWARE\Classes\Directory\Background\shell\$NomCle",
             "SOFTWARE\Classes\Directory\shell\FusionPDFSousDossiers", "SOFTWARE\Classes\Directory\Background\shell\FusionPDFSousDossiers")

if (-not (Test-Path -LiteralPath $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
function Write-Log([string]$Ligne) {
    $l = "{0:yyyy-MM-dd HH:mm:ss} - {1}" -f (Get-Date), $Ligne
    Write-Output $l
    try { Add-Content -LiteralPath $Journal -Value $l -Encoding ASCII } catch { }
}

Write-Log '=== DEBUT DESINSTALLATION : Fusion PDF ==='

# --- fenetres ouvertes ------------------------------------------------
$procs = @(Get-Process -Name 'FusionPDF' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($Dossier, [StringComparison]::OrdinalIgnoreCase) })
foreach ($p in $procs) {
    Write-Log "fermeture de FusionPDF.exe (pid $($p.Id))"
    try { [void]$p.CloseMainWindow() } catch { }
    if (-not $p.WaitForExit(15000)) { try { $p.Kill(); [void]$p.WaitForExit(5000) } catch { } }
}

# --- cles de clic droit ----------------------------------------------
$hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
foreach ($c in $Cles) {
    $k = $hklm.OpenSubKey($c)
    if (-not $k) { Write-Log "cle deja absente : HKLM\$c"; continue }
    $k.Close()
    try { $hklm.DeleteSubKeyTree($c, $false); Write-Log "cle retiree : HKLM\$c" }
    catch { Write-Log "ERREUR cle $c : $($_.Exception.Message)" }
}
$hklm.Close()

# --- raccourci et dossier --------------------------------------------
if (Test-Path -LiteralPath $Lnk) {
    try { Remove-Item -LiteralPath $Lnk -Force -ErrorAction Stop; Write-Log "raccourci retire : $Lnk" }
    catch { Write-Log "ERREUR raccourci : $($_.Exception.Message)" }
}
if (Test-Path -LiteralPath $Dossier) {
    try { Remove-Item -LiteralPath $Dossier -Recurse -Force -ErrorAction Stop; Write-Log "dossier retire : $Dossier" }
    catch { Write-Log "ERREUR dossier : $($_.Exception.Message)" }
}

# --- conclusion sur l'etat relu -------------------------------------
$reste = @()
if (Test-Path -LiteralPath (Join-Path $Dossier 'FusionPDF.exe')) { $reste += 'exe' }
if (Test-Path -LiteralPath $Lnk) { $reste += 'raccourci' }
$hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
foreach ($c in $Cles) { $k = $hklm.OpenSubKey($c); if ($k) { $k.Close(); $reste += "HKLM\$c" } }
$hklm.Close()

if ($reste.Count) {
    Write-Log ('ECHEC, relu : il reste ' + ($reste -join ', '))
    exit 1
}
Write-Log 'OK, relu : exe, raccourci et clic droit retires'
Write-Log '=== FIN DESINSTALLATION : Fusion PDF ==='
exit 0
