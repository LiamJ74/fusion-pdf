# Deployment

[Français](../fr/deploiement.md) · [Back to README](../../README.md)

Three ways to install Fusion PDF, from the simplest to the most managed.

## 1. Just the executable

Download `FusionPDF.exe` from the releases and run it. It needs nothing else
than qpdf (`winget install QPDF.QPDF`). No Start menu shortcut, no right-click
command. Downloaded files carry the "Mark of the Web": Windows SmartScreen may
warn the first time, because the executable is not code-signed.

## 2. Machine-wide, by script

From the release zip (or from `package\` after a build), in an **elevated**
PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

`install.ps1` installs, for all users:

| | |
|---|---|
| Executable | `C:\Program Files\Fusion PDF\FusionPDF.exe` |
| Start menu | `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Fusion PDF.lnk` |
| Right-click on a folder | `HKLM\SOFTWARE\Classes\Directory\shell\FusionPDF` (`"%1"`) |
| Right-click inside a folder | `HKLM\SOFTWARE\Classes\Directory\Background\shell\FusionPDF` (`"%V"`) |
| Same, with subfolders | `…\Directory\shell\FusionPDFSousDossiers` and `…\Directory\Background\shell\FusionPDFSousDossiers` (`--sous-dossiers "%1"` / `"%V"`) |
| Log | `C:\ProgramData\FusionPDF\FusionPDF-install.log` (or `%LOCALAPPDATA%\FusionPDF\` when run without admin rights) |

The right-click label follows the language Windows was installed in. The script
ends on the **state read back** (executable version, shortcut, both keys), not
on the operations attempted: exit code 0 only if everything is there.

`uninstall.ps1` closes running windows (bounded wait), removes everything, reads
back, and exits 0 only if nothing remains. qpdf is left in place.

`detection.ps1` writes a single line and exits 0 when the executable is present
in version `$VersionAttendue` or later **and** both right-click keys exist.

## 3. Microsoft Intune (Win32 app)

### With the scripts of the `intune\` folder

| | |
|---|---|
| `intune\qpdf\` | a generic qpdf package: `install.ps1` (winget `QPDF.QPDF`, machine scope, SYSTEM), `detection.ps1`, `uninstall.ps1` |
| `intune\New-Intunewin.ps1` | builds `dist\intune\FusionPDF.intunewin` and `dist\intune\qpdf.intunewin`, and checks each package's declared size against its source folder |
| `intune\Publish-FusionPDF.ps1` | creates the apps through Microsoft Graph, sets qpdf as an automatic-install dependency (read back before anything else), uploads the logo, allows available uninstall, assigns as Available. **Plan by default**: nothing is written without `-Apply` |

```powershell
# 1. build the executable (package\FusionPDF.exe, package\logo.png)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-FusionPDF.ps1

# 2. build the two .intunewin (Microsoft Win32 Content Prep Tool, not included)
.\intune\New-Intunewin.ps1 -IntuneWinAppUtil C:\Tools\IntuneWinAppUtil.exe

# 3. publish (PowerShell 7, module Microsoft.Graph.Authentication)
.\intune\Publish-FusionPDF.ps1 -GroupId <entra-group-id>            # plan
.\intune\Publish-FusionPDF.ps1 -GroupId <entra-group-id> -Apply     # write
```

Already have a qpdf app in Intune? Pass `-QpdfAppId <its id>` and it is reused
as the dependency instead of creating a new one. The script refuses to touch an
existing app with the same name.

### By hand, in the Intune console

1. Build the `.intunewin` from the `package\` folder (with the executable in it),
   with `install.ps1` as setup file. Do it **outside** any synced folder
   (OneDrive): an interrupted packaging run can keep writing in the source folder.
2. Create a Win32 app:

   | | |
   |---|---|
   | Install command | `powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -File install.ps1` |
   | Uninstall command | `powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -File uninstall.ps1` |
   | Install behaviour | System |
   | Detection | custom script `detection.ps1`, run as 64-bit |
   | Logo | `package\logo.png` (produced by the build) |

3. **Dependency**: create (or reuse) a Win32 app for qpdf — for instance winget
   `QPDF.QPDF` — and add it as a dependency of Fusion PDF with **automatic install**.
   Intune then installs qpdf first, even for users outside the qpdf app's assignment.
4. Assign as **Available** to the users who need it.

Good to know — all observed in production:

- **Intune runs install scripts in 32-bit PowerShell.** `$env:ProgramFiles` is then
  `Program Files (x86)` and `HKLM:` is redirected to `WOW6432Node`. The scripts use
  `$env:ProgramW6432` and the explicit 64-bit registry view, so this does not matter.
- **"Uninstall" in Company Portal** only appears for an available Win32 app if
  *Allow available uninstall* is set to **Yes**.
- **Changing the detection rule of a published app** makes devices with the
  previous version look "not installed", and an *available* app is not reinstalled
  automatically. Publish each new version as a **new app that supersedes the
  previous one**, with automatic update.
- **A Graph `PATCH` on the app that does not include `largeIcon`** may wipe its icon.
- **Windows 11 context menu**: classic shell verbs appear under *Show more options*
  (or directly with Shift + right-click). Showing in the first-level menu requires
  a packaged, **signed** `IExplorerCommand` extension.

## Logs

| | |
|---|---|
| Installation, uninstallation | `C:\ProgramData\FusionPDF\FusionPDF-*.log` as administrator or SYSTEM, otherwise `%LOCALAPPDATA%\FusionPDF\` |
| Application (per user) | `%LOCALAPPDATA%\FusionPDF\FusionPDF.log` — full qpdf output and unexpected errors |
| Language choice (per user) | `%LOCALAPPDATA%\FusionPDF\langue.txt` |

## Command line

```
FusionPDF.exe                         empty window
FusionPDF.exe "<folder>"              window filled with the PDFs of the folder
FusionPDF.exe --sous-dossiers "<dir>" same, with the whole tree (preference unchanged)
FusionPDF.exe "<a.pdf>" "<b.pdf>"     window filled with these files, in this order
FusionPDF.exe --lang en|fr ...        forces the language
FusionPDF.exe --autotest <report>     self-tests without a window (used by the build)
```
