# Zlet Folder Converter

Local Windows desktop tool for bulk, rule-based conversion of mixed files in a
folder and its subfolders. The application scans files, lets the user choose one
action per detected format, previews every operation, and writes results under
`<selected folder>\_converted`.

Files are processed locally. Originals are never overwritten, deleted, moved,
or intentionally modified. Existing output files and directories are conflicts
and are not replaced.

## Conversion rules

| Source | Available targets | Default |
| --- | --- | --- |
| JSON | TXT, Markdown, Skip | TXT |
| DOC | DOCX, PDF, Skip | DOCX |
| XLS | XLSX, PDF, Skip | XLSX |
| PPT | PPTX, PDF, Skip | PPTX |
| DOCX | PDF, Skip | Skip |
| XLSX | PDF, Skip | Skip |
| PPTX | PDF, Skip | Skip |
| ODT | DOCX, PDF, Skip | Skip |
| ODS | XLSX, PDF, Skip | Skip |
| ODP | PPTX, PDF, Skip | Skip |
| PDF, images, archives, unknown | Skip | Skip |

JSON conversion is implemented by the in-process `JsonConversionAdapter`.
Office and OpenDocument conversion uses a bundled LibreOffice runtime through
an adapter and process abstraction. The app does not use Microsoft Office COM,
cloud conversion, analytics, telemetry, or runtime downloads.

LibreOffice runs in headless mode with an isolated temporary profile and
workspace for each operation. Generated OOXML files are checked as ZIP
containers with required base parts; PDF files are checked for a PDF signature.
Conversion compatibility and visual fidelity depend on LibreOffice and the
source document. The project does not claim 100% preservation of formatting.

XLSX-to-CSV-per-sheet is not part of ZL-041.

## Portable package

The application is .NET self-contained, but the product is not one physical
standalone EXE. The release layout is:

```text
ZletFolderConverter/
  ZletFolderConverter.exe
  runtime/
    libreoffice/
  licenses/
  THIRD_PARTY_NOTICES.md
  README_PORTABLE.txt
```

The package requires no separately installed .NET Runtime, Microsoft Office, or
LibreOffice. Package size is dominated by the selected LibreOffice runtime and
must be measured on the actual release artifact.

LibreOffice binaries are not committed to Git. Packaging requires an explicitly
selected runtime and copies that package's own license documents:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 `
  -LibreOfficePath "C:\path\to\LibreOffice"
```

Packaging fails if the runtime, version, or license material cannot be
confirmed. No public release artifact should be published until the selected
LibreOffice build and generated notices have been reviewed.

## Build and test

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Opt-in synthetic LibreOffice integration tests run only when the environment
variable points to a real runtime:

```powershell
$env:ZLET_LIBREOFFICE_PATH = "C:\path\to\LibreOffice"
dotnet test FolderConverter.sln -c Release --filter Category=LibreOfficeIntegration
```

For local development, the same path can be supplied in the ignored
`ZletFolderConverter.local.json` file (see the committed empty example). The
actual local path must not be committed, logged, or included in exports.

## Safety details

- root `_converted` is excluded from scans; a nested `archive\_converted` remains
  a legitimate input folder;
- temporary Office files matching `~$*` are skipped;
- directory reparse points, junctions, and symlinks are not traversed;
- relative subfolder structure is preserved;
- path traversal and targets outside root `_converted` are rejected;
- one failed conversion does not stop the rest of the batch;
- no document contents, command lines, credentials, or secrets are logged.
