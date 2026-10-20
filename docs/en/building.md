# Building from source

[Français](../fr/construire.md) · [Back to README](../../README.md)

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
