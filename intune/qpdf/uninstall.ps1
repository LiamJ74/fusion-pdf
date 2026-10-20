# ============================================================
# qpdf - Intune Win32 uninstall (SYSTEM context)
#
# winget uninstall QPDF.QPDF, bounded wait (10 min), then reads back:
# exit 0 only if qpdf.exe is gone.
#
# Log: C:\ProgramData\FusionPDF\qpdf-uninstall.log
# PowerShell 5.1, strict ASCII.
# ============================================================

$Id      = 'QPDF.QPDF'
$LogDir  = 'C:\ProgramData\FusionPDF'
$Journal = Join-Path $LogDir 'qpdf-uninstall.log'

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

Write-Log '=== qpdf uninstall ==='
$winget = @(Get-ChildItem -Path "$env:ProgramW6432\WindowsApps\Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe\winget.exe" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1)
if ($winget.Count) {
    $p = Start-Process -FilePath $winget[0].FullName -ArgumentList "uninstall --id $Id --exact --silent --accept-source-agreements --disable-interactivity" -PassThru -WindowStyle Hidden
    $null = $p.Handle
    if (-not $p.WaitForExit(10 * 60 * 1000)) { try { $p.Kill() } catch { }; Write-Log 'winget did not finish within 10 minutes' }
    else { Write-Log ("winget exit code: {0}" -f $p.ExitCode) }
} else {
    Write-Log 'winget not found'
}

$left = Find-Qpdf
if ($left) { Write-Log "FAILED, read back: still present ($left)"; exit 1 }
Write-Log 'OK, read back: qpdf removed'
exit 0
