<#
.SYNOPSIS
    Builds the two Intune packages: FusionPDF.intunewin and qpdf.intunewin.

.DESCRIPTION
    Uses Microsoft's IntuneWinAppUtil.exe (Microsoft Win32 Content Prep Tool,
    https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool), which is
    NOT included here: pass its path with -IntuneWinAppUtil.

    Output goes to dist\intune\, outside the source folders, and the output
    folder must not be inside a synced folder (OneDrive): an interrupted
    packaging run can keep writing, and a half-written .intunewin left in a
    source folder ends up inside the next package.

    Each package is checked after the build: Detection.xml is read and its
    UnencryptedContentSize compared with the size of the source folder.

    Run tools\Build-FusionPDF.ps1 first: package\ must contain FusionPDF.exe.

.EXAMPLE
    .\intune\New-Intunewin.ps1 -IntuneWinAppUtil C:\Tools\IntuneWinAppUtil.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$IntuneWinAppUtil,
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path $here -Parent
if (-not $Output) { $Output = Join-Path $repo 'dist\intune' }
if ($Output -like '*OneDrive*') { throw "Do not package into a synced folder: $Output" }
if (-not (Test-Path -LiteralPath $IntuneWinAppUtil)) { throw "IntuneWinAppUtil.exe not found: $IntuneWinAppUtil" }

$sources = [ordered]@{
    'FusionPDF' = Join-Path $repo 'package'
    'qpdf'      = Join-Path $here 'qpdf'
}
if (-not (Test-Path -LiteralPath (Join-Path $sources['FusionPDF'] 'FusionPDF.exe'))) {
    throw 'package\FusionPDF.exe is missing: run tools\Build-FusionPDF.ps1 first.'
}
New-Item -ItemType Directory -Path $Output -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($name in $sources.Keys) {
    $src = $sources[$name]
    $tmp = Join-Path $env:TEMP ("intunewin-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        & $IntuneWinAppUtil -c $src -s 'install.ps1' -o $tmp -q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "IntuneWinAppUtil failed for $name (exit $LASTEXITCODE)" }
        $built = Join-Path $tmp 'install.intunewin'
        $dest = Join-Path $Output "$name.intunewin"
        Move-Item -LiteralPath $built -Destination $dest -Force

        # Read back: the declared content size must match the source folder.
        $zip = [IO.Compression.ZipFile]::OpenRead($dest)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -like '*Metadata/Detection.xml' } | Select-Object -First 1
            $reader = New-Object IO.StreamReader($entry.Open())
            [xml]$xml = $reader.ReadToEnd()
            $reader.Dispose()
        } finally { $zip.Dispose() }
        $declared = [int64]$xml.ApplicationInfo.UnencryptedContentSize
        $folder = (Get-ChildItem -LiteralPath $src -Recurse -File | Measure-Object Length -Sum).Sum
        "{0,-10} {1}  ({2:N0} bytes compressed content, source folder {3:N0} bytes)" -f $name, $dest, $declared, $folder
        if ($declared -gt $folder * 1.1 + 4096) { throw "$name package is larger than its source folder: stray files were packaged." }
    } finally {
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
