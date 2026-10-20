# Changelog

## 1.3.0 — 2026-10-20

- **Include subfolders**: a checkbox under the drop area also takes the PDFs of
  subfolders, folder by folder, in File Explorer order (the folder's own PDFs
  first, then each subfolder in natural order). The choice is remembered, and it
  also applies to the right-click command.
- Ticking or unticking it adds or removes the subfolder PDFs of the folders
  already loaded, straight away.
- When a folder has no PDF of its own but its subfolders do, a message says how
  many and suggests the checkbox.
- Hidden and system folders are skipped; junctions and symbolic links are not
  followed (a junction pointing to a parent folder would loop). OneDrive folders,
  which are cloud placeholders, are walked without downloading anything.
- The output file goes to the folder that was opened, not to the subfolder of
  the first PDF.

## 1.2.0 — 2026-10-20

First public release.

- English and French interface, following Windows, with an **FR | EN** switch
  in the window (choice remembered per user; switching keeps the current list).
- Output and log file names follow the language: `_MERGED_` / `_RENAMED_` in
  English, `_FUSION_` / `_RENOMMAGE_` in French; both are recognised in either language.
- The right-click command follows the language Windows was installed in.
- Built-in help (F1), shown automatically on first launch.
- Drag and drop of files and folders, reordering, natural sort, first-page
  preview, OneDrive online-only files, clear error messages, reversible
  renaming, background merge with progress.
