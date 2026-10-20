<#
.SYNOPSIS
    Liste les textes d'interface a traduire et verifie src\textes.en.txt.

.DESCRIPTION
    Extrait, comme le fait Build-FusionPDF.ps1 :
      - dans MainWindow.xaml, chaque texte entoure de U+00A7 (le signe paragraphe) ;
      - dans les .cs, le premier argument litteral de L(...) / Langue.L(...),
        et les deux mots de Pl(n, "...", "...") / Langue.Pl(...).
    Puis compare avec src\textes.en.txt (paires 'fr: ' / 'en: ').

    Rend un objet par cle : Cle, Traduite. Sert au build (refus si une cle
    n'est pas traduite) et a la maintenance (-Manquantes pour les lister).

    PowerShell 5.1, ASCII strict.
#>
[CmdletBinding()]
param(
    [string]$Src = '',
    [switch]$Manquantes
)

$ErrorActionPreference = 'Stop'
if (-not $Src) { $Src = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'src' }
$para = [string][char]0xA7

function Normaliser([string]$s) {
    return $s.Replace('&#160;', ' ').Replace([string][char]0xA0, ' ').Trim()
}
function Deschapper([string]$s) {
    # litteral C# -> texte : \" \\ \n
    return [regex]::Replace($s, '\\(.)', { param($m) switch ($m.Groups[1].Value) { 'n' { "`n" } default { $m.Groups[1].Value } } })
}

$cles = New-Object 'System.Collections.Generic.List[string]'
$xaml = [IO.File]::ReadAllText((Join-Path $Src 'MainWindow.xaml'))
foreach ($m in [regex]::Matches($xaml, [regex]::Escape($para) + '(.*?)' + [regex]::Escape($para), 'Singleline')) { $cles.Add((Normaliser $m.Groups[1].Value)) }

$lit = '"((?:[^"\\]|\\.)*)"'
foreach ($f in Get-ChildItem -LiteralPath $Src -Filter '*.cs' -File) {
    $c = [IO.File]::ReadAllText($f.FullName)
    foreach ($m in [regex]::Matches($c, '(?<![A-Za-z_])(?:Langue\.)?L\(\s*' + $lit)) { $cles.Add((Normaliser (Deschapper $m.Groups[1].Value))) }
    foreach ($m in [regex]::Matches($c, '(?<![A-Za-z_])(?:Langue\.)?Pl\([^,;]+,\s*' + $lit + '\s*,\s*' + $lit)) {
        $cles.Add((Normaliser (Deschapper $m.Groups[1].Value)))
        $cles.Add((Normaliser (Deschapper $m.Groups[2].Value)))
    }
}

# PAS une table de hachage PowerShell : elle ignore la casse, et 'fichier'
# y ecraserait 'FICHIER' (deux textes distincts). Dictionary .NET : ordinal.
$dico = New-Object 'System.Collections.Generic.Dictionary[string,bool]'
$fichier = Join-Path $Src 'textes.en.txt'
if (Test-Path -LiteralPath $fichier) {
    $fr = $null
    foreach ($l in [IO.File]::ReadAllLines($fichier, [Text.Encoding]::UTF8)) {
        if ($l.StartsWith('fr: ')) { $fr = $l.Substring(4) }
        elseif ($l.StartsWith('en: ') -and $null -ne $fr) { $dico[(Normaliser $fr.Replace('\n', "`n"))] = $true; $fr = $null }
    }
}

$vues = New-Object 'System.Collections.Generic.HashSet[string]'
$res = foreach ($k in $cles) {
    if (-not $vues.Add($k)) { continue }
    [pscustomobject]@{ Cle = $k; Traduite = $dico.ContainsKey($k) }
}
if ($Manquantes) { $res | Where-Object { -not $_.Traduite } | ForEach-Object { $_.Cle } }
else { $res }
