# Manual clean-machine verification

Record Windows version, display resolution, scaling, installed Word/Excel/
PowerPoint versions, ZIP size, unpacked size, commit SHA, and tester name.

## Package integrity

- Fully extract the ZIP to a new local folder.
- Confirm `ZletConverter.exe` and
  `Zlet.FolderConverter.OfficeWorker.exe` are present.
- Confirm there are no source, test, local config, Python, Java, or Office
  runtime files.
- Start `ZletConverter.exe` from the fully extracted folder.
- Confirm the title is `Zlet Converter v0.0.2`.
- Confirm the portable package name follows
  `ZletConverter-v<version>-win-x64.zip`.

## Installer clean install

- Build the installer with `scripts/build-installer.ps1`.
- Confirm the output name follows
  `ZletConverter-v<version>-Setup-win-x64.exe`.
- Install for the current user on a machine without an existing installation.
- Confirm the installer, Installed Apps entry, Start-menu shortcut, optional
  desktop shortcut, and post-install launch text use `Zlet Converter`.
- Confirm a clean install uses the visible install folder name `Zlet Converter`.
- Confirm uninstall succeeds and removes app-owned installed files/shortcuts.

## Installer upgrade from the previous public name

When an old-name installer fixture is available, use the published v0.0.2
installer as the upgrade baseline.

- Install the historical `Zlet Batch Converter` build first.
- Run the new `Zlet Converter` installer without manually uninstalling the old
  build.
- Confirm the existing Inno Setup AppId is treated as the same product and only
  one Installed Apps entry remains, named `Zlet Converter`.
- Confirm there are no duplicate app-owned Start-menu or desktop shortcuts.
- Confirm the old app-owned `ZletBatchConverter.exe` host files are not left as
  a second launchable application in the install directory.
- Confirm `ZletConverter.exe` launches normally after the upgrade.
- Confirm uninstall still succeeds after the upgrade.
- Do not delete or rename unrelated/user-created files while checking cleanup.

## Office capability display

- Confirm the UI always shows separate Word, Excel, and PowerPoint statuses.
- On a machine with Word and Excel but no PowerPoint, confirm:
  - DOC is ready;
  - XLS is ready;
  - PPT says `Требуется Microsoft PowerPoint`;
  - the batch can still process DOC and XLS.
- With PowerPoint already open, confirm PPT reports
  `PowerPoint уже запущен. Закройте его и повторите преобразование.`;
  confirm the user presentation stays open and DOC/XLS still process.
- Confirm modern DOCX/XLSX/PPTX files say
  `Будет скопирован без изменений`.

## Source and selection workflow

- Enter a source path manually.
- Paste a source path surrounded by quotes.
- Choose a folder with the picker.
- Scan with subfolders on and off.
- Confirm reparse folders/files and Office `~$` files are skipped.
- Select individual files.
- Use select all, clear selection, and invert selection.
- Change a rule and confirm preview refreshes.

## Folder output

- Choose a result folder.
- Confirm nested relative paths are preserved.
- Convert/copy a mixed batch.
- Confirm one failed file does not stop later files.
- Confirm source SHA-256 values do not change.
- Create an existing target file and directory; confirm neither is overwritten.
- Run the same batch again and confirm conflicts are reported.
- Confirm temporary operation folders are removed.

## ZIP output

- Choose a new `.zip` path.
- Confirm only successful selected outputs are included.
- Confirm nested relative paths are preserved.
- Confirm an existing ZIP is not overwritten.
- Repeat the run and confirm the conflict is clear.

## Office behavior

- Use non-sensitive DOC, XLS, and PPT fixtures.
- Confirm the relevant Office UI and dialogs do not appear.
- Confirm original files are unchanged.
- Confirm produced DOCX/XLSX/PPTX files open normally.
- Trigger or simulate timeout and confirm already-open user Office documents are
  not terminated.
- Record separately which real conversions were actually executed.

## Layout

- Verify at 100% and 125% Windows scaling.
- Verify at 1366x768.
- Confirm source/output controls, capability statuses, preview rows, selection
  controls, report, and primary action remain visible and usable.

Do not mark the candidate complete from automated tests alone. Record every
unperformed item explicitly before merge.
