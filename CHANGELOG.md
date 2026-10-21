# Changelog

## 1.5.0 — 2026-10-21

- **The output name is required.** The *Output file* box now holds only the part
  you choose (for example `Smith file`); the `_MERGED_<date>.pdf` ending
  (`_FUSION_` in French) is added automatically and shown next to the box. With
  an empty box, *Merge* refuses, the box turns red and gets the focus. Before,
  the proposed name began with `_` and many merges were left unnamed.
- The merged file is recognised by that ending, wherever it is in the name, so
  it is still never merged again when the folder is reloaded (names from earlier
  versions, starting with `_FUSION_` / `_MERGED_`, are still recognised).
- An existing file is never overwritten: ` (2)`, ` (3)`… is added.
- After a merge the box is cleared, so the next document asks for its own name.

## 1.4.0 — 2026-10-20

- **Second right-click command**: *Merge the PDFs in this folder and its
  subfolders* (on a folder, and inside an open folder). It loads the whole tree
  whatever the state of the *Include subfolders* checkbox, and without changing
  the user's remembered choice.
- **Subfolder order now matches File Explorer**: at each level, subfolders first
  (in natural name order), then the folder's own PDFs. In 1.3 the folder's own
  PDFs came first.
- Ticking *Include subfolders* after a folder is loaded rebuilds that folder's
  block in tree order, instead of appending the subfolder PDFs at the end of the
  list. Files added separately keep their place.
- *Sort by name* sorts folder by folder in the same tree order, instead of mixing
  every folder's files by file name.
- New command-line option `--sous-dossiers`.
- `install.ps1`, `detection.ps1` and `uninstall.ps1` handle the two new keys
  (`FusionPDFSousDossiers`).

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
