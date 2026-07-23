# Zlet Batch Converter

`v0.0.0` pre-alpha Windows desktop application for local, rule-based batch
conversion. The repository intentionally remains named `folder-converter`
until PR #3 is reviewed and merged:
`https://github.com/zlet-labs/folder-converter`.

Choose or paste a source folder, scan it, adjust one rule per detected format,
and select individual ready operations. The preview supports Select All, Clear,
Invert, and status filters without losing selection.

Results can be written to an editable folder destination or to a result ZIP.
Relative subfolder structure is preserved. Originals are never overwritten,
deleted, moved, or intentionally modified; existing targets and ZIP files are
reported as conflicts.

## Conversion rules

| Source | Available targets | Default |
| --- | --- | --- |
| JSON | TXT, Markdown, Skip | TXT |
| DOC | DOCX, PDF, Skip | DOCX |
| XLS | XLSX, PDF, Skip | XLSX |
| PPT | PPTX, PDF, Skip | PPTX |
| DOCX, XLSX, PPTX | PDF, Skip | Skip |
| ODT | DOCX, PDF, Skip | Skip |
| ODS | XLSX, PDF, Skip | Skip |
| ODP | PPTX, PDF, Skip | Skip |
| PDF, images, archives, unknown | Skip | Skip |

JSON uses the in-process adapter. Office and OpenDocument conversions use the
bundled LibreOffice runtime in headless mode with isolated temporary profiles.
OOXML and PDF outputs are structurally validated. Conversion fidelity depends
on LibreOffice and the source document; pixel-perfect compatibility is not
guaranteed. XLSX-to-CSV-per-sheet is outside ZL-041.

## Result modes

- Folder: defaults to `<source>\_converted`, but another child or external
  folder can be entered or selected.
- ZIP archive: defaults to
  `<source>\ZletBatchConverter-v0.0.0-results.zip`. Only successful selected
  outputs are included; partial success creates a ZIP, while zero successes do
  not create an empty archive.

The selected output folder or exact output ZIP is excluded from later scans.
Path traversal, unsafe ZIP entries, reparse-point escapes, and overwrites are
rejected.

## Portable package

The self-contained Windows x64 layout is:

```text
ZletBatchConverter-v0.0.0-win-x64/
  ZletBatchConverter.exe
  runtime/
    libreoffice/
  licenses/
  THIRD_PARTY_NOTICES.md
  README_PORTABLE.txt
```

Build it from an explicitly selected official LibreOffice runtime:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 `
  -LibreOfficePath "C:\path\to\LibreOffice"
```

LibreOffice binaries and generated portable artifacts are not committed.
The package requires no separately installed .NET Runtime, Microsoft Office,
or LibreOffice. No public release is published before manual review.

## Build and test

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Real synthetic LibreOffice integration tests require:

```powershell
$env:ZLET_LIBREOFFICE_PATH = "C:\path\to\LibreOffice"
dotnet test FolderConverter.sln -c Release --filter Category=LibreOfficeIntegration
```

The verified runtime is official LibreOffice 26.2.4 for Windows x86-64
(reported version 26.2.4.2). The 15 exercised mappings cover legacy Office to
OOXML/PDF, modern Office to PDF, and OpenDocument to OOXML/PDF.

Files stay local: there is no cloud conversion, telemetry, analytics, or
runtime download. Actual local paths must not be committed, logged, or included
in exported packages.
