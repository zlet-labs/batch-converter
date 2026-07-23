# Manual verification

Full clean-machine verification was not run by the agent. Before merge, test the portable ZIP on Windows 10/11 x64 without installed .NET Runtime, Microsoft Office, or LibreOffice.

## Clean-machine checklist

- Extract to paths with spaces and Cyrillic characters.
- Launch `ZletFolderConverter.exe` without administrator rights.
- Confirm no runtime installation prompt appears.
- Confirm no network access or external converter is used.

## Functional checklist

- Valid JSON folder: convert separately to TXT and Markdown.
- Mixed valid and invalid JSON: valid output succeeds and invalid output is absent.
- Verify Cyrillic, Unicode, emoji, nested objects, arrays, `null`, and property order.
- Verify nested folder structure under root `_converted`.
- Verify a pre-existing target file and directory both remain unchanged and show conflicts.
- Verify DOC/XLS/PPT alongside JSON remain «Не поддерживается».
- Verify root `_converted` is excluded but `archive\_converted` is scanned.
- Repeat scan after processing and repeat conversion; existing results must become conflicts.
- Confirm all originals are byte-for-byte unchanged.

## UI checklist

- Test at 1366×768.
- Test Windows scaling at 125%.
- Confirm folder selection, scan, settings, and conversion controls are disabled while their operation runs.
- Confirm progress, errors, final counts, and output path remain visible without relying on a toast.
