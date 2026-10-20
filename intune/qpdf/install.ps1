# ============================================================
# qpdf - Intune Win32 install (SYSTEM context)
#
# Installs qpdf machine-wide with winget (package QPDF.QPDF). Fusion PDF
# calls qpdf; publish this app and add it as an automatic-install
# DEPENDENCY of Fusion PDF.
#
# winget is not on the PATH of the SYSTEM account: it is located in
# Program Files\WindowsApps (App Installer). Bounded wait: 20 minutes.
# Concludes on the state READ BACK (qpdf.exe present), not on the exit code.
#
# Log: C:\ProgramData\Autopilot-Setup\qpdf-install.log
# PowerShell 5.1, strict ASCII.
# ============================================================

$Id      = 'QPDF.QPDF'
$LogDir  = 'C:\ProgramData\Autopilot-Setup'
$Journal = Join-Path $LogDir 'qpdf-install.log'
$DelaiMs = 20 * 60 * 1000

if (-not (Test-Path -LiteralPath $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
function Write-Log([string]$Line) {
    $l = "{0:yyyy-MM-dd HH:mm:ss} - {1}" -f (Get-Date), $Line
    Write-Output $l
    try { Add-Content -LiteralPath $Journal -Value $l -Encoding ASCII } catch { }
}

function Find-Qpdf {
    foreach ($root in @($env:ProgramW6432, $env:ProgramFiles) | Where-Object { $_ } | Select-Object -Unique) {
        $hit = Get-ChildItem -LiteralPath $root -Directory -Filter 'qpdf*' -ErrorAction SilentlyContinue |
               ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter 'qpdf.exe' -ErrorAction SilentlyContinue } |
               Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

function Find-Winget {
    $c = @(Get-ChildItem -Path "$env:ProgramW6432\WindowsApps\Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe\winget.exe" -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Descending)
    if ($c.Count) { return $c[0].FullName }
    $g = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($g) { return $g.Source }
    return $null
}

Write-Log '=== qpdf install ==='
$already = Find-Qpdf
if ($already) { Write-Log "already installed: $already"; exit 0 }

$winget = Find-Winget
if (-not $winget) { Write-Log 'FAILED: winget (App Installer) not found'; exit 1 }
Write-Log "winget: $winget"

$arguments = "install --id $Id --exact --silent --scope machine --accept-package-agreements --accept-source-agreements --disable-interactivity"
$p = Start-Process -FilePath $winget -ArgumentList $arguments -PassThru -WindowStyle Hidden
$null = $p.Handle   # PowerShell 5.1: without this, ExitCode is empty after WaitForExit
if (-not $p.WaitForExit($DelaiMs)) {
    try { $p.Kill() } catch { }
    Write-Log 'FAILED: winget did not finish within 20 minutes'
    exit 1
}
Write-Log ("winget exit code: {0}" -f $p.ExitCode)

$after = Find-Qpdf
if ($after) { Write-Log "OK, read back: $after"; exit 0 }
Write-Log 'FAILED: qpdf.exe not found after installation'
exit 1
