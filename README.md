# Zlet Folder Converter

Zlet Folder Converter is a local Windows prototype for scanning folders that contain legacy Microsoft Office files and building a safe conversion plan.

The current prototype does not perform real document conversion. It detects `.doc`, `.xls`, and `.ppt` files, proposes output paths under `_converted`, protects existing target files, and shows honest `Unsupported` statuses until an embedded .NET converter is approved.

Russian documentation: [README_RU.md](README_RU.md)

## Prototype Status

- Folder scanning: available.
- Operation preview: available.
- Target conflict detection: available.
- Portable Windows x64 packaging: available.
- Real DOC/XLS/PPT conversion: not supported yet.

## Supported Mappings

None in this prototype.

## Unsupported Mappings

- `.doc` to `.docx`
- `.xls` to `.xlsx`
- `.ppt` to `.pptx`

These mappings remain unsupported until a fully embedded .NET adapter passes licensing review and synthetic validation.

## Privacy And Safety

- The app works locally.
- Files are not sent to a server or cloud API.
- No backend, analytics, telemetry, or remote config is used.
- Microsoft Office and LibreOffice are not required.
- Original files are not changed, deleted, moved, or overwritten.
- Existing target files are treated as conflicts and are not overwritten.
- Planned output paths are under `<selected-folder>/_converted`.

## Windows Requirements

- Windows 10 or Windows 11 x64.
- For users of the portable ZIP: no installed .NET Runtime is required.
- For developers: .NET 8 SDK is required.

## Build

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
```

## Test

```powershell
dotnet test FolderConverter.sln -c Release
```

## Portable ZIP

The primary distribution format for early versions is a portable self-contained ZIP for Windows x64.

Users should:

1. Download the ZIP.
2. Extract it to a local folder.
3. Run `ZletFolderConverter.exe`.

Build the portable package locally:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

Build with a versioned ZIP name:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 -Version "0.1.0-alpha"
```

Artifacts are created under `artifacts/portable/win-x64` and are intentionally ignored by Git.

## Current Limitations

- No conversion adapter is enabled yet.
- No installer, auto-update, service, registry writes, file associations, scheduled tasks, or shell extensions.
- No drag and drop.
- No clean-machine verification has been completed by the agent.

## Conversion Research

See [CONVERSION_RESEARCH.md](CONVERSION_RESEARCH.md).

## Security And Reporting

Do not use real sensitive documents as test fixtures. If you find a security issue, open a GitHub issue without attaching private files or document contents.

## License

MIT. See [LICENSE](LICENSE).
