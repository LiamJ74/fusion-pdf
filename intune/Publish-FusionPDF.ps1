#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes Fusion PDF (and, if needed, qpdf) to Microsoft Intune as Win32
    apps, sets qpdf as an automatic-install dependency, and assigns Fusion PDF
    as Available to a group.

.DESCRIPTION
    PLAN FIRST. Without -Apply nothing is written: the script connects,
    reads what exists, and prints exactly what it would do.

    WHAT IT DOES WITH -Apply
      1. qpdf: reuses the app given by -QpdfAppId, or creates it from
         dist\intune\qpdf.intunewin;
      2. creates 'Fusion PDF' (or '-Name') from dist\intune\FusionPDF.intunewin:
         system context, detection.ps1 (64-bit), install/uninstall commands,
         logo, 'Allow available uninstall' on;
      3. adds qpdf as a dependency with automatic install, and READS IT BACK
         before going further: an app without its dependency is never assigned;
      4. assigns it as Available to -GroupId;
      5. reads everything back and prints the result.

    LESSONS BUILT IN (all observed in production)
      - a Graph PATCH on a win32LobApp that does not carry largeIcon may wipe
        the icon: the logo is sent again with every PATCH;
      - the install context (runAsAccount) cannot be patched afterwards: get it
        right at creation (system);
      - an app with an existing name is never deleted or overwritten: the
        script stops. Publish next versions as a new app that supersedes it.

    Requires the Microsoft.Graph.Authentication module and an account with
    the DeviceManagementApps.ReadWrite.All permission.

.EXAMPLE
    # plan
    .\intune\Publish-FusionPDF.ps1 -GroupId <entra-group-id>
    # apply
    .\intune\Publish-FusionPDF.ps1 -GroupId <entra-group-id> -Apply
    # reuse an existing qpdf app
    .\intune\Publish-FusionPDF.ps1 -GroupId <id> -QpdfAppId <intune-app-id> -Apply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GroupId,
    [string]$Name = 'Fusion PDF',
    [string]$QpdfAppId = '',
    [string]$TenantId = '',
    [string]$Dist = '',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path $here -Parent
if (-not $Dist) { $Dist = Join-Path $repo 'dist\intune' }
$Graph = 'https://graph.microsoft.com/beta'
$Apps  = "$Graph/deviceAppManagement/mobileApps"

$pkgFusion = Join-Path $Dist 'FusionPDF.intunewin'
$pkgQpdf   = Join-Path $Dist 'qpdf.intunewin'
$logo      = Join-Path $repo 'package\logo.png'
foreach ($f in @($pkgFusion, $logo) + $(if (-not $QpdfAppId) { @($pkgQpdf) } else { @() })) {
    if (-not (Test-Path -LiteralPath $f)) { throw "Missing: $f (run tools\Build-FusionPDF.ps1, then intune\New-Intunewin.ps1)" }
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Invoke-Graph([string]$Method, [string]$Uri, $Body) {
    $p = @{ Method = $Method; Uri = $Uri; OutputType = 'PSObject' }
    if ($null -ne $Body) { $p.Body = ($Body | ConvertTo-Json -Depth 25); $p.ContentType = 'application/json' }
    Invoke-MgGraphRequest @p
}

function Get-Icon {
    @{ '@odata.type' = '#microsoft.graph.mimeContent'; type = 'image/png'
       value = [Convert]::ToBase64String([IO.File]::ReadAllBytes($logo)) }
}

function Read-Intunewin([string]$Path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $work = Join-Path $env:TEMP ('iw_' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $x = $zip.Entries | Where-Object { $_.FullName -like '*Metadata/Detection.xml' } | Select-Object -First 1
        $b = $zip.Entries | Where-Object { $_.FullName -like '*Contents/*.intunewin' } | Select-Object -First 1
        if (-not $x -or -not $b) { throw "Invalid .intunewin: $Path" }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($x, (Join-Path $work 'Detection.xml'), $true)
        [IO.Compression.ZipFileExtensions]::ExtractToFile($b, (Join-Path $work 'payload.bin'), $true)
    } finally { $zip.Dispose() }
    [xml]$xml = Get-Content -LiteralPath (Join-Path $work 'Detection.xml') -Raw
    $i = $xml.ApplicationInfo; $e = $i.EncryptionInfo
    [pscustomobject]@{
        Work = $work; Payload = (Join-Path $work 'payload.bin'); FileName = $i.FileName
        Size = [int64]$i.UnencryptedContentSize; EncryptedSize = (Get-Item -LiteralPath (Join-Path $work 'payload.bin')).Length
        Encryption = @{ '@odata.type' = '#microsoft.graph.fileEncryptionInfo'
            encryptionKey = $e.EncryptionKey; macKey = $e.MacKey; initializationVector = $e.InitializationVector
            mac = $e.Mac; profileIdentifier = $e.ProfileIdentifier; fileDigest = $e.FileDigest; fileDigestAlgorithm = $e.FileDigestAlgorithm }
    }
}

function Wait-FileState([string]$Uri, [string]$Target) {
    $deadline = (Get-Date).AddMinutes(20)
    do {
        Start-Sleep -Seconds 3
        $f = Invoke-Graph GET $Uri
        if ($f.uploadState -eq $Target) { return $f }
        if ($f.uploadState -like '*Failed*') { throw "Upload state: $($f.uploadState)" }
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for '$Target' (last: $($f.uploadState))"
}

function Send-Blob([string]$Sas, [string]$Path) {
    $chunk = 6MB; $ids = @(); $i = 0
    $fs = [IO.File]::OpenRead($Path)
    try {
        $buf = New-Object byte[] $chunk
        while (($n = $fs.Read($buf, 0, $buf.Length)) -gt 0) {
            $id = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($i.ToString('D6'))); $ids += $id
            $part = New-Object byte[] $n; [Array]::Copy($buf, 0, $part, 0, $n)
            Invoke-RestMethod -Method Put -Uri "$Sas&comp=block&blockid=$([uri]::EscapeDataString($id))" -Body $part `
                -Headers @{ 'x-ms-blob-type' = 'BlockBlob' } -ContentType 'application/octet-stream' | Out-Null
            $i++
        }
    } finally { $fs.Dispose() }
    $list = '<?xml version="1.0" encoding="utf-8"?><BlockList>' + (($ids | ForEach-Object { "<Latest>$_</Latest>" }) -join '') + '</BlockList>'
    Invoke-RestMethod -Method Put -Uri "$Sas&comp=blocklist" -Body $list -ContentType 'text/plain' | Out-Null
}

function New-Win32App([string]$DisplayName, [string]$Publisher, [string]$Description, [string]$Package,
                      [string]$Detection, [bool]$WithIcon) {
    $meta = Read-Intunewin $Package
    try {
        $body = [ordered]@{
            '@odata.type' = '#microsoft.graph.win32LobApp'
            displayName = $DisplayName; publisher = $Publisher; description = $Description
            installCommandLine   = 'powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -File install.ps1'
            uninstallCommandLine = 'powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -File uninstall.ps1'
            applicableArchitectures = 'x64'; minimumSupportedWindowsRelease = '1903'
            setupFilePath = 'install.ps1'; fileName = $meta.FileName
            allowAvailableUninstall = $true
            installExperience = @{ '@odata.type' = '#microsoft.graph.win32LobAppInstallExperience'; runAsAccount = 'system'; deviceRestartBehavior = 'suppress' }
            returnCodes = @(
                @{ returnCode = 0; type = 'success' }, @{ returnCode = 1707; type = 'success' },
                @{ returnCode = 3010; type = 'softReboot' }, @{ returnCode = 1641; type = 'hardReboot' },
                @{ returnCode = 1618; type = 'retry' })
            detectionRules = @(@{ '@odata.type' = '#microsoft.graph.win32LobAppPowerShellScriptDetection'
                enforceSignatureCheck = $false; runAs32Bit = $false
                scriptContent = [Convert]::ToBase64String([IO.File]::ReadAllBytes($Detection)) })
        }
        if ($WithIcon) { $body.largeIcon = Get-Icon }
        $app = Invoke-Graph POST $Apps $body
        Write-Host "  created $DisplayName ($($app.id))" -ForegroundColor Green
        try {
            $w32 = "$Apps/$($app.id)/microsoft.graph.win32LobApp"
            $cv = Invoke-Graph POST "$w32/contentVersions" @{}
            $file = Invoke-Graph POST "$w32/contentVersions/$($cv.id)/files" @{
                '@odata.type' = '#microsoft.graph.mobileAppContentFile'; name = $meta.FileName
                size = $meta.Size; sizeEncrypted = $meta.EncryptedSize; manifest = $null; isDependency = $false }
            $fileUri = "$w32/contentVersions/$($cv.id)/files/$($file.id)"
            $ready = Wait-FileState $fileUri 'azureStorageUriRequestSuccess'
            Write-Host ("  uploading {0:N0} KB..." -f ($meta.EncryptedSize / 1KB))
            Send-Blob $ready.azureStorageUri $meta.Payload
            Invoke-Graph POST "$fileUri/commit" @{ fileEncryptionInfo = $meta.Encryption } | Out-Null
            Wait-FileState $fileUri 'commitFileSuccess' | Out-Null
            $patch = @{ '@odata.type' = '#microsoft.graph.win32LobApp'; committedContentVersion = "$($cv.id)" }
            if ($WithIcon) { $patch.largeIcon = Get-Icon }      # a PATCH without it may wipe the icon
            Invoke-Graph PATCH "$Apps/$($app.id)" $patch | Out-Null
            $read = Invoke-Graph GET "$Apps/$($app.id)"
            if ("$($read.committedContentVersion)" -ne "$($cv.id)") { throw "content version not committed (read $($read.committedContentVersion))" }
            return $app.id
        } catch {
            # An app without committed content is useless and confusing: remove it.
            Write-Host "  failed, removing the incomplete app $($app.id)" -ForegroundColor Yellow
            try { Invoke-Graph DELETE "$Apps/$($app.id)" | Out-Null } catch { }
            throw
        }
    } finally {
        Remove-Item -LiteralPath $meta.Work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Read the current state
# ---------------------------------------------------------------------------
Import-Module Microsoft.Graph.Authentication
$connect = @{ Scopes = 'DeviceManagementApps.ReadWrite.All', 'Group.Read.All'; NoWelcome = $true }
if ($TenantId) { $connect.TenantId = $TenantId }
Connect-MgGraph @connect
Write-Host "Connected: $((Get-MgContext).Account)" -ForegroundColor Cyan

$all = @(); $u = "$Apps`?`$select=id,displayName&`$top=999"
while ($u) { $p = Invoke-Graph GET $u; $all += $p.value; $u = $p.'@odata.nextLink' }
# Compared client-side: OData filters on displayName miss names with brackets.
$same = @($all | Where-Object { $_.displayName -eq $Name })
if ($same.Count) { throw "An app named '$Name' already exists ($($same.id -join ', ')). Choose another -Name, or publish as a new app that supersedes it." }

$group = Invoke-Graph GET "https://graph.microsoft.com/v1.0/groups/$GroupId`?`$select=id,displayName"
if ($QpdfAppId) {
    $q = Invoke-Graph GET "$Apps/$QpdfAppId"
    if ($q.'@odata.type' -ne '#microsoft.graph.win32LobApp') { throw "$QpdfAppId is not a Win32 app ($($q.'@odata.type'))" }
}

Write-Host ''
Write-Host "=== MODE: $(if ($Apply) { 'APPLY' } else { 'PLAN - nothing is written' })" -ForegroundColor Cyan
Write-Host "  qpdf        : $(if ($QpdfAppId) { "reuse '$($q.displayName)' ($QpdfAppId)" } else { "create from $pkgQpdf" })"
Write-Host "  app         : create '$Name' from $pkgFusion (system, available uninstall allowed, logo)"
Write-Host "  dependency  : qpdf, automatic install"
Write-Host "  assignment  : available to '$($group.displayName)' ($GroupId)"
if (-not $Apply) { Write-Host ''; Write-Host 'Nothing was written. Add -Apply.' -ForegroundColor Yellow; return }

# ---------------------------------------------------------------------------
# Apply
# ---------------------------------------------------------------------------
if (-not $QpdfAppId) {
    $QpdfAppId = New-Win32App 'qpdf' 'qpdf (Jay Berkenbilt)' 'Command-line PDF tool, required by Fusion PDF. Installed with winget (QPDF.QPDF).' `
                              $pkgQpdf (Join-Path $here 'qpdf\detection.ps1') $false
}
$appId = New-Win32App $Name 'Fusion PDF' 'Combine several PDFs into a single file, in the order you choose. Start menu and right-click on a folder. Engine: qpdf (dependency).' `
                      $pkgFusion (Join-Path $repo 'package\detection.ps1') $true

Invoke-Graph POST "$Apps/$appId/updateRelationships" @{ relationships = @(@{
    '@odata.type' = '#microsoft.graph.mobileAppDependency'; targetId = $QpdfAppId; dependencyType = 'autoInstall' }) } | Out-Null
$dep = @((Invoke-Graph GET "$Apps/$appId/relationships").value | Where-Object { $_.targetId -eq $QpdfAppId -and $_.dependencyType -eq 'autoInstall' })
if (-not $dep.Count) { throw "The qpdf dependency was not read back: '$Name' ($appId) is NOT assigned. Add the dependency in the console, then assign." }

Invoke-Graph POST "$Apps/$appId/assign" @{ mobileAppAssignments = @(@{
    '@odata.type' = '#microsoft.graph.mobileAppAssignment'; intent = 'available'
    target   = @{ '@odata.type' = '#microsoft.graph.groupAssignmentTarget'; groupId = $GroupId }
    settings = @{ '@odata.type' = '#microsoft.graph.win32LobAppAssignmentSettings'; notifications = 'showAll' } }) } | Out-Null

# ---------------------------------------------------------------------------
# Read back
# ---------------------------------------------------------------------------
Start-Sleep -Seconds 4
$r = Invoke-Graph GET "$Apps/$appId"
$a = @((Invoke-Graph GET "$Apps/$appId/assignments").value)
Write-Host ''
Write-Host '=== READ BACK' -ForegroundColor Cyan
Write-Host "  app         : $($r.displayName) ($appId), content version $($r.committedContentVersion)"
Write-Host "  logo        : $(if ($r.largeIcon.value) { 'present' } else { 'MISSING' })"
Write-Host "  uninstall   : available uninstall $(if ($r.allowAvailableUninstall) { 'allowed' } else { 'NOT allowed' })"
Write-Host "  dependency  : qpdf $QpdfAppId, $($dep[0].dependencyType)"
Write-Host "  assignments : $(($a | ForEach-Object { "$($_.intent) $($_.target.groupId)" }) -join ', ')"
