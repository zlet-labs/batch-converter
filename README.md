# Zlet Batch Converter

![Version](https://img.shields.io/badge/version-v0.0.2-blue)
![Status](https://img.shields.io/badge/status-PRE--ALPHA-orange)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)
![Processing](https://img.shields.io/badge/processing-local%20only-success)

**English** · [Русский](README_RU.md)

Zlet Batch Converter is a small local Windows utility for batch-processing files in folders and subfolders. It converts supported legacy Microsoft Office formats, safely copies already-modern Office files, and keeps processing on your computer.

**No accounts. No cloud uploads. No document content is sent anywhere.**

> **Current source version: v0.0.2 PRE-ALPHA.** Verified binaries are published separately on the GitHub Releases page after manual verification. If `v0.0.2` is not listed there yet, the repository source is newer than the latest published package.

## What it does

- Scans a selected folder and its subfolders.
- Shows a preview before processing.
- Lets you select which operations to run.
- Converts supported legacy Office files using the installed Microsoft Office applications.
- Safely copies modern Office files without converting them.
- Preserves relative subfolder structure.
- Never silently overwrites an existing output file or directory.
- Shows per-file status, progress, source size, and execution time.
- Supports stopping an active batch without killing unrelated user Office processes.
- Can copy a clean list of planned conversions using relative paths only.

## Supported formats

| Source | Result | Requirement |
|---|---|---|
| `.doc` | `.docx` | Microsoft Word installed |
| `.xls` | `.xlsx` | Microsoft Excel installed |
| `.ppt` | `.pptx` | Microsoft PowerPoint installed |
| `.docx`, `.xlsx`, `.pptx` | unchanged safe copy | Microsoft Office not required |
| `.json` | `.txt` or `.md` | Microsoft Office not required |

Word, Excel, and PowerPoint are detected independently. If one application is missing, only the corresponding legacy format is unavailable; the rest of the batch can still run.

Microsoft Office is **not bundled** with Zlet Batch Converter.

### PowerPoint safety restriction

PowerPoint uses a shared COM process. To avoid interfering with a user's open presentation, PPT conversion is refused while `POWERPNT` is already running. Close PowerPoint and retry the conversion.

This restriction does not block DOC/XLS conversion or unchanged PPTX copying.

## Quick start

1. Download a verified package from [GitHub Releases](https://github.com/zlet-labs/batch-converter/releases), or build the current source yourself.
2. Run `ZletBatchConverter.exe`.
3. Choose the source folder.
4. Scan files and review the planned actions.
5. Select the operations you want.
6. Choose the output location/mode and start processing.
7. Review the final summary and generated files.

For packaged builds, .NET is self-contained. You do not need to install the .NET runtime separately.

## Downloads and releases

Published packages live on the [Releases page](https://github.com/zlet-labs/batch-converter/releases).

A release may contain:

- `ZletBatchConverter-v<version>-Setup-win-x64.exe` — Windows installer;
- `ZletBatchConverter-v<version>-win-x64.zip` — portable package.

Only use files attached to an actual GitHub Release. Do not treat a source version number in the repository as proof that matching binaries have already been published.

The installer is currently unsigned, so Windows may show an unknown-publisher or SmartScreen warning.

## Safety and privacy

The converter is intentionally local-first and defensive around user files.

- Files are processed locally and are not uploaded.
- The UI process does not perform Office COM automation directly.
- Office conversion runs through an isolated STA worker process.
- Legacy files are opened read-only.
- Macros, dialogs, and Recent/MRU additions are disabled for worker-controlled Office sessions.
- Source files must remain inside the selected source folder.
- Reparse-point files and folders are skipped.
- Source SHA-256 is checked before and after processing.
- Outputs are produced in temporary/staging locations and validated before the final move.
- Existing output files/directories are not overwritten.
- Office processes are never terminated by process name alone.
- Forced cleanup is allowed only for an app-owned process whose PID and start time were proven.
- Failure of one file does not automatically stop the remaining selected files.

Technical diagnostics may contain error codes and process metadata. They must not contain document contents, secrets, or full local document paths.

## Build from source

Requirements:

- Windows x64;
- .NET 8 SDK.

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

## Build a portable package

```powershell
.\scripts\publish-portable.ps1
```

For v0.0.2 the expected ZIP name is:

```text
artifacts/portable/win-x64/ZletBatchConverter-v0.0.2-win-x64.zip
```

The portable package includes the self-contained .NET 8 application and worker. It does not include Microsoft Office, Python, Java, source files, or test fixtures.

## Build the Windows installer

Installer creation uses Inno Setup 6:

```powershell
.\scripts\build-installer.ps1
```

The script first builds the portable payload and then creates the Windows x64 installer. It also prints the installer SHA-256 and Authenticode status.

## Office integration tests

Real Office integration tests are opt-in because they require installed Microsoft Office and real legacy fixtures:

```powershell
$env:ZLET_OFFICE_INTEGRATION = "1"
$env:ZLET_OFFICE_WORD_FIXTURE = "C:\fixtures\sample.doc"
$env:ZLET_OFFICE_WORD_BATCH_FIXTURE_DIR = "C:\fixtures\word-batch"
$env:ZLET_OFFICE_EXCEL_FIXTURE = "C:\fixtures\sample.xls"
$env:ZLET_OFFICE_POWERPOINT_FIXTURE = "C:\fixtures\sample.ppt"
dotnet test FolderConverter.sln -c Release --filter Category=OfficeIntegration
```

If an Office application or required fixture is missing, the corresponding integration test is skipped rather than counted as passed.

## Limitations

Zlet Batch Converter is still **PRE-ALPHA**.

Complex, corrupted, password-protected, or unsupported legacy documents may fail to convert. Conversion fidelity depends on the installed Microsoft Office version and the document features used. The project does not promise successful legacy conversion on a computer without the corresponding Office application.

Original files are not intentionally modified, but keep backups of important data when testing pre-release software.

## Verification

Before publishing release binaries, follow the manual clean-machine checklist:

[docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md)

## Project

Zlet Batch Converter is a **Zlet Labs** project: small, practical, self-serve tools without unnecessary SaaS machinery.

- Issues: [GitHub Issues](https://github.com/zlet-labs/batch-converter/issues)
- Releases: [GitHub Releases](https://github.com/zlet-labs/batch-converter/releases)
- License: [MIT](LICENSE)
- Russian README: [README_RU.md](README_RU.md)
