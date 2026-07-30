# Zlet Batch Converter v0.0.1

Zlet Batch Converter is a local Windows desktop prototype for processing files
in a folder while preserving relative subfolder paths. Files are not uploaded.
Repository: https://github.com/zlet-labs/batch-converter

## Supported operations

| Source | Result | Requirement |
|---|---|---|
| `.doc` | `.docx` | Microsoft Word installed |
| `.xls` | `.xlsx` | Microsoft Excel installed |
| `.ppt` | `.pptx` | Microsoft PowerPoint installed |
| `.docx`, `.xlsx`, `.pptx` | unchanged safe copy | no Office application required |
| `.json` | `.txt` or `.md` | no Office application required |

Word, Excel, and PowerPoint are detected independently. A missing application
disables only its own legacy format; it does not block the rest of a batch.
Microsoft Office is not included in the portable package.

PowerPoint uses a shared COM process. For user-document safety, PPT conversion
is refused while `POWERPNT` is already running; close PowerPoint and retry.
This restriction does not affect DOC, XLS, or unchanged PPTX copying.

## Safety model

- The UI process never performs COM automation.
- One STA `.NET` worker process handles one Office conversion.
- The UI sends a JSON request through redirected standard input; file paths are
  not interpolated into a shell command.
- The worker opens legacy files read-only, disables macros, suppresses dialogs,
  and does not add files to Recent/MRU lists.
- Source paths must remain inside the selected source folder.
- Reparse-point files and folders are skipped.
- SHA-256 is calculated before and after processing.
- Output is created in a temporary directory, validated as OOXML, copied to a
  same-directory staging file, and atomically moved to the final path.
- Existing files and directories are never overwritten.
- The worker reports the owned Office PID immediately after COM activation and
  before changing application settings.
- A timeout first terminates the worker, briefly drains its lifecycle output,
  and only then considers an Office PID proven to be a new worker-owned
  instance. Processes are never killed by name.
- Failure of one file does not stop later selected files.

Technical diagnostics contain error codes and process metadata only. They do not
contain document content, secret values, or full document paths.

## Build and test

Requirements: Windows x64 and the .NET 8 SDK.

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Office integration tests are opt-in and require a real legacy fixture for each
application being tested:

```powershell
$env:ZLET_OFFICE_INTEGRATION = "1"
$env:ZLET_OFFICE_WORD_FIXTURE = "C:\fixtures\sample.doc"
$env:ZLET_OFFICE_EXCEL_FIXTURE = "C:\fixtures\sample.xls"
$env:ZLET_OFFICE_POWERPOINT_FIXTURE = "C:\fixtures\sample.ppt"
dotnet test FolderConverter.sln -c Release --filter Category=OfficeIntegration
```

An integration test is skipped, not passed, when its Office application or
fixture is absent.

## Portable package

```powershell
.\scripts\publish-portable.ps1
```

The script publishes Windows x64, self-contained .NET 8 application and worker
files, then creates:

`artifacts/portable/win-x64/ZletBatchConverter-v0.0.1-win-x64.zip`

The package contains neither Microsoft Office nor conversion runtimes such as
Python or Java. Do not publish a GitHub Release before separate review and
manual verification.

## Limitations

Originals are not intentionally modified, but complex, password-protected,
corrupted, or unsupported legacy documents may fail to convert. Results can
differ from the source because the installed Office version and document
features vary. This version does not promise conversion on a computer without
the corresponding Microsoft Office application.

See [README_RU.md](README_RU.md) and
[docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md).
