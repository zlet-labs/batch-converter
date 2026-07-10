# Manual Clean-Machine Verification

Full clean-machine verification was not run by the agent. Before merge, verify the portable ZIP manually on Windows 10/11 x64.

## Checklist

- Run on a machine or VM without installed .NET Runtime, Microsoft Office, or LibreOffice.
- Extract the ZIP to a path with spaces.
- Extract the ZIP to a path with Cyrillic characters.
- Run `ZletFolderConverter.exe` without administrator rights.
- Select a synthetic test folder containing `.doc`, `.xls`, and `.ppt` files.
- Confirm scan results and planned operations appear.
- Confirm DOC/XLS/PPT mappings are shown as unsupported.
- Confirm existing targets under `_converted` are shown as conflicts.
- Confirm original files are unchanged.
- Confirm no network upload or external converter is used.
