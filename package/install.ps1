# ============================================================
# Install Intune Win32 App - Fusion PDF (application maison)
#
# Pose, POUR TOUS LES UTILISATEURS du poste :
#   - C:\Program Files\Fusion PDF\FusionPDF.exe
#   - le raccourci du menu Demarrer public  "Fusion PDF"
#   - le clic droit "Fusionner les PDF de ce dossier", sur un dossier
#     (Directory\shell) et sur le fond d'un dossier ouvert
#     (Directory\Background\shell), sous HKLM\SOFTWARE\Classes.
# Rien dans HKCU : le script tourne en SYSTEM, qui n'a pas la ruche des
# utilisateurs, et une cle machine sert tout le monde.
#
# Le moteur de fusion, qpdf, n'est PAS embarque : c'est l'application
# Intune 'qpdf', posee en DEPENDANCE de celle-ci.
#
# PIEGE 32 BITS. Intune lance ce script en PowerShell 32 bits. On n'utilise
# donc ni $env:ProgramFiles (qui y vaut 'Program Files (x86)') ni le
# fournisseur HKLM: de PowerShell (redirige vers WOW6432Node), mais
# $env:ProgramW6432 et la vue de registre 64 bits explicite.
#
# EXE EN COURS D'UTILISATION. Une mise a jour pendant qu'un utilisateur a
# la fenetre ouverte : on ne peut pas ecraser l'exe, mais on peut le
# RENOMMER (NTFS l'autorise sur un exe charge). L'ancien part en
# FusionPDF.exe.old-<date>, supprime a l'installation suivante.
#
# Conclusion sur l'etat RELU : exe a la bonne version, raccourci, deux cles.
# Journal : C:\ProgramData\Autopilot-Setup\FusionPDF-install.log
# PowerShell 5.1, ASCII strict, contexte SYSTEME.
# ============================================================

[CmdletBinding()]
param()

# Lus par Upload-IntuneApps.ps1 pour le nom, l'editeur et la description.
$AppName     = 'Fusion PDF'
$Publisher   = 'Fusion PDF'
$Description = 'Assembler plusieurs PDF en un seul fichier, dans l ordre voulu. Menu Demarrer et clic droit sur un dossier. Moteur : qpdf (dependance).'

# Le clic droit est commun a tout le poste : sa langue suit celle de
# l'installation de Windows (l'application, elle, suit chaque utilisateur).
$Verbe    = 'Merge the PDFs in this folder'
if ([Globalization.CultureInfo]::InstalledUICulture.TwoLetterISOLanguageName -eq 'fr') { $Verbe = 'Fusionner les PDF de ce dossier' }
$NomCle   = 'FusionPDF'
$LogDir   = 'C:\ProgramData\Autopilot-Setup'
$Journal  = Join-Path $LogDir 'FusionPDF-install.log'

$racinePF = $env:ProgramW6432
if (-not $racinePF) { $racinePF = $env:ProgramFiles }
$Dossier  = Join-Path $racinePF 'Fusion PDF'
$Exe      = Join-Path $Dossier 'FusionPDF.exe'
$Lnk      = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Fusion PDF.lnk'

if (-not (Test-Path -LiteralPath $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
function Write-Log([string]$Ligne) {
    $l = "{0:yyyy-MM-dd HH:mm:ss} - {1}" -f (Get-Date), $Ligne
    Write-Output $l
    try { Add-Content -LiteralPath $Journal -Value $l -Encoding ASCII } catch { }
}

Write-Log "=== DEBUT INSTALLATION : $AppName ==="
Write-Log ("contexte : {0}, processus {1} bits" -f [Security.Principal.WindowsIdentity]::GetCurrent().Name, $(if ([Environment]::Is64BitProcess) { 64 } else { 32 }))

try {
    # --- l'exe du paquet ----------------------------------------------
    $Source = Join-Path $PSScriptRoot 'FusionPDF.exe'
    if (-not (Test-Path -LiteralPath $Source)) { throw "FusionPDF.exe absent du paquet ($PSScriptRoot)" }
    $verPaquet = "$((Get-Item -LiteralPath $Source).VersionInfo.FileVersion)".Trim()
    Write-Log "paquet : FusionPDF.exe $verPaquet"

    # --- copie ----------------------------------------------------------
    if (-not (Test-Path -LiteralPath $Dossier)) { New-Item -ItemType Directory -Path $Dossier -Force -ErrorAction Stop | Out-Null }
    foreach ($vieux in @(Get-ChildItem -LiteralPath $Dossier -Filter 'FusionPDF.exe.old-*' -File -ErrorAction SilentlyContinue)) {
        try { Remove-Item -LiteralPath $vieux.FullName -Force -ErrorAction Stop; Write-Log "ancien exe supprime : $($vieux.Name)" }
        catch { Write-Log "ancien exe encore utilise, garde : $($vieux.Name)" }
    }
    if (Test-Path -LiteralPath $Exe) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Exe -Force -ErrorAction Stop
        } catch {
            $mis = 'FusionPDF.exe.old-' + (Get-Date -Format 'yyyyMMddHHmmss')
            Write-Log "exe en cours d'utilisation, renomme en $mis"
            Rename-Item -LiteralPath $Exe -NewName $mis -ErrorAction Stop
            Copy-Item -LiteralPath $Source -Destination $Exe -Force -ErrorAction Stop
        }
    } else {
        Copy-Item -LiteralPath $Source -Destination $Exe -Force -ErrorAction Stop
    }
    Write-Log "copie : $Exe"

    # FAQ complete, ouverte par le bouton "Ouvrir la FAQ complete (PDF)" de
    # l'aide integree. Facultative : sans elle, le bouton est masque.
    $Faq = Join-Path $PSScriptRoot 'FAQ Fusion PDF.pdf'
    if (Test-Path -LiteralPath $Faq) {
        Copy-Item -LiteralPath $Faq -Destination (Join-Path $Dossier 'FAQ Fusion PDF.pdf') -Force -ErrorAction Stop
        Write-Log 'copie : FAQ Fusion PDF.pdf'
    } else {
        Write-Log 'FAQ absente du paquet : bouton FAQ masque dans l aide'
    }

    # --- raccourci du menu Demarrer public -----------------------------
    $sh = New-Object -ComObject WScript.Shell
    $s = $sh.CreateShortcut($Lnk)
    $s.TargetPath = $Exe
    $s.WorkingDirectory = $Dossier
    $s.IconLocation = "$Exe,0"
    $s.Description = 'Assembler plusieurs PDF en un seul fichier'
    $s.Save()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($sh)
    Write-Log "raccourci : $Lnk"

    # --- clic droit, vue 64 bits explicite -----------------------------
    # Directory\shell            : clic droit SUR un dossier      -> %1
    # Directory\Background\shell : clic droit DANS un dossier     -> %V
    $hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
    foreach ($v in @(@{ Cle = "SOFTWARE\Classes\Directory\shell\$NomCle"; Arg = '%1' },
                     @{ Cle = "SOFTWARE\Classes\Directory\Background\shell\$NomCle"; Arg = '%V' })) {
        $k = $hklm.CreateSubKey($v.Cle)
        $k.SetValue('MUIVerb', $Verbe, 'String')
        $k.SetValue('Icon', "`"$Exe`",0", 'String')
        $k.Close()
        $c = $hklm.CreateSubKey($v.Cle + '\command')
        $c.SetValue('', "`"$Exe`" `"$($v.Arg)`"", 'String')
        $c.Close()
        Write-Log "clic droit : HKLM\$($v.Cle)"
    }
    $hklm.Close()
} catch {
    Write-Log "ERREUR : $($_.Exception.Message)"
}

# --- conclusion sur l'etat relu -------------------------------------
$manque = @()
if (-not (Test-Path -LiteralPath $Exe)) { $manque += 'exe' }
elseif ("$((Get-Item -LiteralPath $Exe).VersionInfo.FileVersion)".Trim() -ne $verPaquet) { $manque += 'version' }
if (-not (Test-Path -LiteralPath $Lnk)) { $manque += 'raccourci' }
$hklm = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
foreach ($cle in @("SOFTWARE\Classes\Directory\shell\$NomCle\command", "SOFTWARE\Classes\Directory\Background\shell\$NomCle\command")) {
    $k = $hklm.OpenSubKey($cle); $val = $null
    if ($k) { $val = $k.GetValue(''); $k.Close() }
    if (-not $val -or $val.IndexOf($Exe, [StringComparison]::OrdinalIgnoreCase) -lt 0) { $manque += $cle }
}
$hklm.Close()

if ($manque.Count) {
    Write-Log ("ECHEC, relu : manque " + ($manque -join ', '))
    exit 1
}
Write-Log "OK, relu : exe $verPaquet, raccourci, clic droit dossier et fond de dossier"
Write-Log "=== FIN INSTALLATION : $AppName ==="
exit 0
