# Troubleshooting

[Français](../fr/depannage.md) · [Back to README](../../README.md)

Every message names the file at fault, and its row is selected in the list.
Nothing is written under the final name when a merge fails.

| Message | What to do |
|---|---|
| **qpdf, the merge engine, is not installed** | Install qpdf (`winget install QPDF.QPDF`, or your Company Portal), then click **Merge** again: no need to reopen the app. The badge top right turns green: *qpdf x.y.z ready*. |
| **The file … is password protected** | qpdf cannot open it. Remove it from the list, or open it with its password and save an unprotected copy (Print › *Microsoft Print to PDF*), then add that copy. |
| **The file … is damaged or is not a real PDF** | Open it to check, remove it from the list, merge again. A slightly damaged PDF is accepted: the final banner says so, and the result is readable. |
| **The file … is open in another program** / **locked** | Close it in your PDF reader (or the program using it). With *Rename* ticked, no file is renamed until all of them are free. |
| **The folder … contains no PDF** | Only PDFs placed directly in the folder are taken, not those in subfolders. Right-click the subfolder that holds them. |
| **OneDrive could not download …** | Check the connection and that OneDrive is running. Otherwise: right-click the folder › *Always keep on this device*. |
| **The output file … is open in another program** | Close the previous merged file, or change the output name. |
| **You are not allowed to write in this folder** | Choose another location with **Save as…**. |
| No *Merge the PDFs in this folder* in the right-click menu | On Windows 11 it is under *Show more options* (or Shift + right-click). If it is not there either, the command was not installed: use `package\install.ps1`, or the Start menu and drag and drop. |
| SmartScreen warns on first launch | The executable is not code-signed and was downloaded. Installing it by script or Intune into Program Files avoids the warning. |

## Where to look

- Application log, per user: `%LOCALAPPDATA%\Fusion PDF\FusionPDF.log` — the full
  qpdf output and any unexpected error are there; the user only sees a plain message.
- qpdf exit codes: **0** perfect, **3** warnings (result kept, readable); anything
  else is a failure, and the temporary output is deleted.
- Installation: `C:\ProgramData\Autopilot-Setup\FusionPDF-install.log`.
