# Building from source

[Français](../fr/construire.md) · [Back to README](../../README.md)

## Step by step

1. **Get the code.** Either `git clone https://github.com/LiamJ74/fusion-pdf.git`, or on
   GitHub: **Code › Download ZIP**, then unzip it (for instance into `C:\Dev\fusion-pdf`).
   With the ZIP, unblock the files once, otherwise Windows may refuse to run the scripts:
   ```powershell
   Get-ChildItem -Recurse C:\Dev\fusion-pdf | Unblock-File
   ```
2. **(Optional) Install qpdf** — `winget install QPDF.QPDF` — so that the self-tests
   also run a real merge. The build works without it.
3. **Build**, from the repository folder, in an ordinary (non-admin) PowerShell window:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-FusionPDF.ps1
   ```
   A window opens for a second at the end: that is the build checking the app starts.
4. **Get the result** in `package\`:

   | File | |
   |---|---|
   | `package\FusionPDF.exe` | the application — run it, or copy it anywhere |
   | `package\logo.png` | the icon, for Intune or Company Portal |
   | `package\install.ps1` … | machine-wide install scripts (see [deployment](deployment.md)) |

   The last lines of the build print the version and the SHA-256 of the executable.

5. **Change the version** (optional): edit `$VersionAttendue` in `package\detection.ps1`,
   then build again. It is the only place the version is set.

### If the build fails

| Message | Cause, fix |
|---|---|
| `csc.exe introuvable` | .NET Framework 4.8 is missing (Windows older than 10 1903). Install .NET Framework 4.8. |
| `... n'est pas en UTF-8 avec BOM` | a source file was saved without BOM by an editor. Save it again as *UTF-8 with BOM*. |
| `... texte(s) sans traduction anglaise` | a new interface text has no English line in `src\textes.en.txt`. `tools\Get-TextesATraduire.ps1 -Manquantes` lists them. |
| `... contient N octet(s) non ASCII` | a script of `package\` contains an accent. Deployed scripts must stay strict ASCII: PowerShell 5.1 would misread them. |
| `autotest : code 1` | a self-test failed: the report printed just above says which one. |
| `cannot be loaded because running scripts is disabled` | keep `-ExecutionPolicy Bypass` in the command, and unblock the files (step 1). |

The build messages are in French, the language the tool was first written in.

## No SDK needed

The compiler is the `csc.exe` that ships with Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`). It only knows
**C# 5**: no `$"…"` interpolation, no `?.`, no `nameof`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-FusionPDF.ps1
```

The build:

1. checks the sources are UTF-8 **with BOM** and the package scripts are strict
   ASCII and parse under PowerShell 5.1;
2. checks that **every interface text has its English translation**;
3. compiles in a temporary folder;
4. runs `FusionPDF.exe --autotest`: 13 checks on real files — XAML in both
   languages, natural sort, renaming and its undo, collision guard, locked files,
   and, if qpdf is installed, a real merge, a password-protected PDF and a damaged
   PDF recognised and named, first-page preview;
5. opens the window, checks it responds, closes it;
6. copies `FusionPDF.exe` and `logo.png` into `package\`.

Nothing is copied if a step fails. The version comes from **one place**:
`$VersionAttendue` in `package\detection.ps1`.

## How it is built

| | |
|---|---|
| UI | WPF; `src\MainWindow.xaml` is embedded and loaded at run time with `XamlReader` (no MSBuild, so no `x:Class`): controls are found by `x:Name`, events wired in `Fenetre.cs` |
| Engine | `src\Moteur.cs`: qpdf location (uninstall registry, then `Program Files\qpdf*`, then `PATH`), OneDrive, locks, natural sort (`StrCmpLogicalW`), renaming |
| Merge | `src\Fusion.cs`: runs in the background (`Task.Run`), writes to a temporary file, keeps qpdf exit codes 0 and 3, reads the result back before declaring success |
| Preview | `src\Apercu.cs`: `Windows.Data.Pdf`, referenced through the `.winmd` files of `C:\Windows\System32\WinMetadata`, awaited through `Completed` (the `await` helpers need the Windows 10 SDK) |
| Languages | `src\Langue.cs` |

## Translations

French is the key. In the code, every displayed text goes through `L("…")`; in
the XAML, it sits between `§…§`. `src\textes.en.txt` gives the English for each
French text:

```
fr: La liste est vide.
en: The list is empty.
```

`tools\Get-TextesATraduire.ps1 -Manquantes` lists the texts that still lack a
translation; the build refuses to compile while there are any.

## Icon

`tools\New-Icone.ps1 -Dest resources` draws the neutral icon (two pages on a
blue tile). `-Logo <png>` puts your own logo, in white, on the same tile.

## Intel graphics drivers

On some Intel drivers, the Windows PDF engine crashes while the process unloads
(`igd10umt64xe.dll`, 0xC0000409). Fusion PDF therefore ends with
`TerminateProcess` once everything is written and closed.
