# Zlet Folder Converter

> Local batch file conversion for Windows, with previews, safe output paths, and no cloud uploads.

**Zlet Labs** · **Alpha**

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-2563EB?logo=windows11&logoColor=white)](https://github.com/zlet-labs/folder-converter)
[![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-22C55E)](LICENSE)

Zlet Folder Converter scans a folder, previews every planned operation, and writes converted files under `<selected folder>\_converted`. Source files are never modified, moved, deleted, or overwritten.

> **Current alpha scope:** JSON → TXT and JSON → Markdown. Legacy Office conversion is not available in the current build.

[Русская документация](README_RU.md)

## Download and releases

No public release has been published yet.

Verified portable Windows builds will be published on the [GitHub Releases page](https://github.com/zlet-labs/folder-converter/releases). Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

Do not use GitHub's automatically generated **Source code** archives as the Windows application. A usable release must contain the portable application package and its required runtime files.

## What it does

- scans one folder or a folder tree;
- finds supported source files without entering the root `_converted` output directory;
- previews source-to-target paths before writing anything;
- converts each supported file independently;
- preserves relative subfolder structure;
- reports successes, conflicts, unsupported files, and per-file errors;
- continues processing when one file fails.

## Why it exists

Old folders often contain mixed formats, duplicated copies, and files that need conversion before they can be reused. This project aims to make that work explicit and safe: select a folder, review the plan, convert, then copy or move the results where they belong.

No account, dashboard, upload queue, or cloud service is required. Humanity may survive without another onboarding funnel.

## Supported formats

| Source | Output | Status |
|---|---|---|
| JSON | TXT | Supported |
| JSON | Markdown | Supported |
| DOC | DOCX | Not supported in the current alpha |
| XLS | XLSX | Not supported in the current alpha |
| PPT | PPTX | Not supported in the current alpha |

Extension matching is case-insensitive. JSON output is UTF-8 and preserves Unicode content.

## Current limitations

- Windows x64 only;
- no DOC, XLS, or PPT conversion in the current alpha;
- no XLSX-to-CSV, PDF conversion, OCR, or JSON merging;
- existing target files and directories are treated as conflicts and are not overwritten;
- there is no installer, auto-update, file association, telemetry, or cloud synchronization;
- visual fidelity claims are not made for formats that are not implemented and verified.

## Privacy and file safety

- processing is local;
- files are not uploaded;
- originals remain unchanged;
- output stays under the selected root `_converted` folder;
- existing output paths are protected from overwrite;
- root `_converted`, Office temporary files, and reparse directories are skipped during scanning;
- errors are isolated per file;
- logs and reports must not contain document contents, secrets, or credentials.

Use synthetic or sanitized files when reporting a problem. Do not attach private customer, work, financial, identity, or medical documents to public issues.

## Run the application

When a release is available:

1. Open [GitHub Releases](https://github.com/zlet-labs/folder-converter/releases).
2. Download the Windows portable ZIP attached to the release.
3. Extract the complete archive to a local folder.
4. Run `ZletFolderConverter.exe`.
5. Select a source folder, review the preview, and start conversion.
6. Find results under the source folder's `_converted` directory.

## Build from source

Requirements:

- Windows 10 or Windows 11 x64;
- .NET 8 SDK;
- PowerShell for portable packaging.

```powershell
git clone https://github.com/zlet-labs/folder-converter.git
cd folder-converter
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

Portable artifacts are created under `artifacts\portable\win-x64` and are not committed to Git.

## Project structure

```text
src/
  Zlet.FolderConverter.App/     WPF application and presentation layer
  Zlet.FolderConverter.Core/    scanning, planning, conversion, and validation
tests/
  Zlet.FolderConverter.Tests/   unit and filesystem tests
docs/                            verification and release documentation
scripts/                         local packaging scripts
.github/                         issue, pull request, and release configuration
```

## Conversion research

The repository documents rejected and investigated conversion approaches in [CONVERSION_RESEARCH.md](CONVERSION_RESEARCH.md). Product claims must follow verified implementation, licensing, and clean-machine testing, not wishful architecture diagrams.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Bug reports and feature requests have dedicated templates under `.github/ISSUE_TEMPLATE`.

## Security

Read [SECURITY.md](SECURITY.md) before reporting a vulnerability. Never publish sensitive source documents, credentials, tokens, or local user paths as evidence.

## Release process

Maintainer guidance is documented in [docs/RELEASING.md](docs/RELEASING.md). Public binaries belong in GitHub Releases, not in the repository history.

## License

MIT. See [LICENSE](LICENSE).
