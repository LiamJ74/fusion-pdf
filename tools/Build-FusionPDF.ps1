<#
.SYNOPSIS
    Compile FusionPDF.exe (C# 5 / WPF / .NET Framework 4.8), le teste, puis
    le depose dans le paquet Intune 'package'.

.DESCRIPTION
    AUCUN SDK : le compilateur est celui livre avec Windows,
        C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
    Il ne connait que le C# 5 (pas de $"", ni ?., ni nameof).

    LA VERSION A UNE SEULE SOURCE : $VersionAttendue dans
    'package\detection.ps1'. Ce script la lit et l'inscrit dans
    l'exe. Monter de version = changer cette ligne, puis recompiler. Ainsi
    l'exe et sa regle de detection ne peuvent pas diverger.

    ETAPES
      1. controles : sources en UTF-8 AVEC BOM, scripts du paquet en ASCII
      2. compilation dans un dossier temporaire hors du dossier source
      3. FusionPDF.exe --autotest : XAML, tri, renommage et annulation sur de
         vrais fichiers, et si qpdf est la : vraie fusion, PDF protege et PDF
         endommage reconnus, apercu WinRT
      4. demarrage reel de la fenetre, controle qu'elle repond, fermeture
      5. copie dans le paquet, version relue sur le fichier copie

    Rien n'est copie dans le paquet si une etape echoue.

    PowerShell 5.1, ASCII strict.

.EXAMPLE
    .\Build-FusionPDF.ps1
#>
[CmdletBinding()]
param(
    # Defaut : '..\package'. Calcule plus bas : sous PowerShell 5.1,
    # $PSScriptRoot est vide dans ce bloc quand le chemin porte des crochets.
    [string]$Paquet = '',
    # Pour une session sans bureau : ne pas ouvrir la fenetre (etape 4).
    [switch]$SansFenetre
)

$ErrorActionPreference = 'Stop'
$Ici = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Paquet) { $Paquet = Join-Path (Split-Path $Ici -Parent) 'package' }
$src = Join-Path (Split-Path $Ici -Parent) 'src'
$res = Join-Path (Split-Path $Ici -Parent) 'resources'

function Etape([string]$t) { Write-Host ''; Write-Host "== $t" -ForegroundColor Cyan }
function Echec([string]$t) { Write-Host "ECHEC : $t" -ForegroundColor Red; exit 1 }

# --- 1. controles -----------------------------------------------------
Etape 'Controles'
$det = Join-Path $Paquet 'detection.ps1'
if (-not (Test-Path -LiteralPath $det)) { Echec "detection.ps1 introuvable : $det" }
$m = [regex]::Match((Get-Content -LiteralPath $det -Raw), "\`$VersionAttendue\s*=\s*'([0-9]+(\.[0-9]+){1,3})'")
if (-not $m.Success) { Echec "`$VersionAttendue illisible dans $det" }
$v = [version]$m.Groups[1].Value
$Version = '{0}.{1}.{2}.{3}' -f $v.Major, $v.Minor, [math]::Max(0, $v.Build), [math]::Max(0, $v.Revision)
Write-Host "  version : $Version (lue dans detection.ps1)"

$sources = @(Get-ChildItem -LiteralPath $src -File | Where-Object { $_.Extension -in '.cs', '.xaml' })
foreach ($f in $sources) {
    $b = [IO.File]::ReadAllBytes($f.FullName)
    if ($b.Length -lt 3 -or $b[0] -ne 0xEF -or $b[1] -ne 0xBB -or $b[2] -ne 0xBF) { Echec "$($f.Name) n'est pas en UTF-8 avec BOM" }
}
Write-Host "  $($sources.Count) sources en UTF-8 avec BOM"

foreach ($f in @(Get-ChildItem -LiteralPath $Paquet -File -Filter '*.ps1') + @(Get-Item -LiteralPath $PSCommandPath)) {
    $n = @([IO.File]::ReadAllBytes($f.FullName) | Where-Object { $_ -gt 127 }).Count
    if ($n) { Echec "$($f.Name) contient $n octet(s) non ASCII" }
    $err = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$null, [ref]$err)
    if ($err) { Echec "$($f.Name) ne passe pas le parseur : $($err[0].Message)" }
}
Write-Host '  scripts du paquet : ASCII strict, parseur OK'

# Chaque texte d'interface (L("...") dans le code, entre signes paragraphe
# dans le XAML) doit avoir sa traduction dans src\textes.en.txt.
$manquantes = @(& (Join-Path $Ici 'Get-TextesATraduire.ps1') -Src $src -Manquantes)
if ($manquantes.Count) {
    $manquantes | ForEach-Object { Write-Host "  sans traduction : $_" -ForegroundColor Red }
    Echec "$($manquantes.Count) texte(s) sans traduction anglaise dans src\textes.en.txt"
}
Write-Host '  traductions anglaises : completes'

# --- 2. compilation ---------------------------------------------------
Etape 'Compilation'
$fw  = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { Echec "csc.exe introuvable : $csc" }

# Assemblies de reference 4.8 si le pack est installe, sinon celles du runtime.
$ref48 = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
if (Test-Path -LiteralPath $ref48) { $wpf = $ref48; $base = $ref48; $facade = Join-Path $ref48 'Facades' }
else { $wpf = Join-Path $fw 'WPF'; $base = $fw; $facade = $fw }
$winmd = Join-Path $env:windir 'System32\WinMetadata'
$refs = @(
    (Join-Path $wpf 'PresentationFramework.dll'), (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll'), (Join-Path $base 'System.Xaml.dll'),
    (Join-Path $base 'System.dll'), (Join-Path $base 'System.Core.dll'), (Join-Path $base 'System.Xml.dll'),
    (Join-Path $facade 'System.Runtime.dll'),
    # Apercu : metadonnees WinRT livrees avec Windows, pas de SDK.
    (Join-Path $winmd 'Windows.Foundation.winmd'), (Join-Path $winmd 'Windows.Data.winmd'),
    (Join-Path $winmd 'Windows.Storage.winmd')
)
foreach ($r in $refs) { if (-not (Test-Path -LiteralPath $r)) { Echec "reference absente : $r" } }
Write-Host "  references : $wpf"

$obj = Join-Path $env:TEMP ('FusionPDF-build-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $obj -Force | Out-Null

# Attributs de version, generes ici : on ne les edite jamais a la main.
# TargetFramework est indispensable : sans lui, le runtime traite l'exe
# comme une application .NET 4.0 et applique les anciens comportements
# (chemins longs refuses, notamment).
$attributs = @"
using System.Reflection;
[assembly: AssemblyTitle("Fusion PDF")]
[assembly: AssemblyDescription("Assembler plusieurs PDF en un seul fichier")]
[assembly: AssemblyCompany("Fusion PDF")]
[assembly: AssemblyProduct("Fusion PDF")]
[assembly: AssemblyCopyright("Fusion PDF contributors $((Get-Date).Year)")]
[assembly: AssemblyVersion("$Version")]
[assembly: AssemblyFileVersion("$Version")]
[assembly: AssemblyInformationalVersion("$Version")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
"@
[IO.File]::WriteAllText((Join-Path $obj 'Version.cs'), $attributs, (New-Object Text.ASCIIEncoding))

$exeTmp = Join-Path $obj 'FusionPDF.exe'
$rsp = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001', '/warn:4',
    "/out:`"$exeTmp`"",
    "/win32icon:`"$(Join-Path $res 'FusionPDF.ico')`"",
    "/win32manifest:`"$(Join-Path $src 'app.manifest')`"",
    "/resource:`"$(Join-Path $src 'MainWindow.xaml')`",FusionPDF.MainWindow.xaml",
    "/resource:`"$(Join-Path $src 'textes.en.txt')`",FusionPDF.textes.en.txt",
    "/resource:`"$(Join-Path $res 'tuile-128.png')`",FusionPDF.Logo.png",
    "/resource:`"$(Join-Path $res 'FusionPDF.ico')`",FusionPDF.Icone.ico"
)
$rsp += $refs | ForEach-Object { "/reference:`"$_`"" }
$rsp += $sources | Where-Object { $_.Extension -eq '.cs' } | ForEach-Object { "`"$($_.FullName)`"" }
$rsp += "`"$(Join-Path $obj 'Version.cs')`""
# Fichier de reponse : evite les pieges de PowerShell sur les virgules
# et les espaces des chemins.
$fRsp = Join-Path $obj 'csc.rsp'
[IO.File]::WriteAllLines($fRsp, $rsp, (New-Object Text.UTF8Encoding $false))

$sortieCsc = & $csc "@$fRsp" 2>&1
$code = $LASTEXITCODE
$sortieCsc | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
if ($code -ne 0 -or -not (Test-Path -LiteralPath $exeTmp)) { Echec "csc a rendu $code" }
Write-Host ("  FusionPDF.exe : {0:N0} octets" -f (Get-Item -LiteralPath $exeTmp).Length)

# --- 3. autotest ------------------------------------------------------
Etape 'Autotest'
$rapport = Join-Path $obj 'autotest.txt'
$p = Start-Process -FilePath $exeTmp -ArgumentList @('--autotest', "`"$rapport`"") -PassThru -WindowStyle Hidden
$null = $p.Handle
if (-not $p.WaitForExit(180000)) { try { $p.Kill() } catch { }; Echec 'autotest : delai de 3 min depasse' }
if (Test-Path -LiteralPath $rapport) {
    Get-Content -LiteralPath $rapport -Encoding UTF8 | ForEach-Object {
        $c = if ($_ -like 'ECHEC*' -or $_ -like '*en echec*') { 'Red' } elseif ($_ -like 'OK*') { 'Green' } else { 'Gray' }
        Write-Host "  $_" -ForegroundColor $c
    }
}
if ($p.ExitCode -ne 0) { Echec "autotest : code $($p.ExitCode)" }

# --- 4. demarrage reel ------------------------------------------------
if (-not $SansFenetre) {
    Etape 'Demarrage de la fenetre'
    $p = Start-Process -FilePath $exeTmp -PassThru
    $limite = (Get-Date).AddSeconds(20)
    do { Start-Sleep -Milliseconds 300; $p.Refresh() } while (-not $p.HasExited -and $p.MainWindowHandle -eq 0 -and (Get-Date) -lt $limite)
    if ($p.HasExited) { Echec "la fenetre s'est fermee seule (code $($p.ExitCode)), voir %LOCALAPPDATA%\Fusion PDF\FusionPDF.log" }
    if ($p.MainWindowHandle -eq 0) { try { $p.Kill() } catch { }; Echec 'aucune fenetre apres 20 s' }
    Start-Sleep -Milliseconds 800
    $p.Refresh()
    $repond = $p.Responding
    Write-Host ("  fenetre '{0}' ouverte, repond : {1}" -f $p.MainWindowTitle, $repond)
    [void]$p.CloseMainWindow()
    if (-not $p.WaitForExit(10000)) { try { $p.Kill() } catch { }; Echec 'la fenetre ne se ferme pas' }
    if (-not $repond) { Echec 'la fenetre ne repond pas' }
}

# --- 5. copie dans le paquet ------------------------------------------
Etape 'Copie dans le paquet'
$dest = Join-Path $Paquet 'FusionPDF.exe'
Copy-Item -LiteralPath $exeTmp -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path $res 'tuile-512.png') -Destination (Join-Path $Paquet 'logo.png') -Force
# FAQ complete, ouverte depuis l'aide integree. Facultative : deposer
# 'FAQ Fusion PDF.pdf' a la racine du depot.
$faq = Join-Path (Split-Path $Ici -Parent) 'FAQ Fusion PDF.pdf'
if (Test-Path -LiteralPath $faq) {
    Copy-Item -LiteralPath $faq -Destination (Join-Path $Paquet 'FAQ Fusion PDF.pdf') -Force
    Write-Host ("  FAQ Fusion PDF.pdf (du {0:dd/MM/yyyy HH:mm})" -f (Get-Item -LiteralPath $faq).LastWriteTime)
} else {
    Write-Host "  FAQ introuvable ($faq) : le bouton FAQ sera masque" -ForegroundColor Yellow
}
$relu = (Get-Item -LiteralPath $dest).VersionInfo.FileVersion
if ($relu -ne $Version) { Echec "version relue sur $dest : '$relu', attendue $Version" }
$h = (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash
Remove-Item -LiteralPath $obj -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  $dest"
Write-Host "  version $relu, SHA256 $h" -ForegroundColor Green
Write-Host ''
Write-Host 'Build termine. Pour Intune : empaqueter package\ (install.ps1 en fichier d installation), hors de tout dossier synchronise.' -ForegroundColor Green
exit 0
