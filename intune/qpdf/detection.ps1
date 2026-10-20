# qpdf - Intune detection
#
# Exit 0 + one line = installed | exit 1 = missing
#
# Facts observed on real devices:
#   - qpdf registers as 'qpdf 12.4.2': the VERSION IS IN THE DISPLAY NAME;
#   - it registers under WOW6432Node, even the 64-bit build;
#   - InstallLocation is EMPTY: start from UninstallString;
#   - the installer does NOT add qpdf to the PATH, and the folder carries
#     the version (C:\Program Files\qpdf 12.4.2\bin).
# The pattern is anchored ('^qpdf(\s|$)') so a future 'qpdf-gui' or
# 'libqpdf' does not match.
#
# PowerShell 5.1, strict ASCII, SYSTEM context, 64-bit.

$Pattern = '^qpdf(\s|$)'
$Minimum = '11.0.0'

$entry = $null
foreach ($root in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
    foreach ($k in @(Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue)) {
        $p = $null
        try { $p = Get-ItemProperty -LiteralPath $k.PSPath -ErrorAction Stop } catch { continue }
        if ("$($p.DisplayName)" -match $Pattern) { $entry = $p; break }
    }
    if ($entry) { break }
}
if (-not $entry) { Write-Output 'qpdf missing.'; exit 1 }

# The binary must exist, not only the registry entry.
$base = $entry.InstallLocation
if (-not $base -and $entry.UninstallString) { $base = Split-Path ($entry.UninstallString -replace '^"|"$', '') -Parent }
$exe = $null
if ($base -and (Test-Path -LiteralPath $base)) {
    $exe = Get-ChildItem -LiteralPath $base -Recurse -Filter 'qpdf.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $exe) { Write-Output "Registry entry present ($($entry.DisplayName)) but qpdf.exe not found."; exit 1 }

$v = "$($entry.DisplayVersion)"
try { if ([version]($v -replace '[^0-9.]', '') -lt [version]$Minimum) { Write-Output "qpdf $v older than $Minimum."; exit 1 } }
catch { Write-Output "qpdf: unreadable version ($v)."; exit 1 }

Write-Output "qpdf $v installed: $($exe.FullName)"
exit 0
