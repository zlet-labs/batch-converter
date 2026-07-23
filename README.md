# Zlet Folder Converter

Local Windows desktop tool for preparing JSON files for NotebookLM. It scans a selected folder, previews every operation, and writes one result per source file under `<selected folder>\_converted`. Source files are never modified or deleted.

## Supported

- JSON → TXT and JSON → Markdown
- batch processing
- optional subfolder scanning with relative folder structure preserved
- local, offline conversion
- portable self-contained Windows x64 package
- conflict detection for existing files and directories
- per-file failures without stopping the batch

## Not supported

- DOC → DOCX, XLS → XLSX, PPT → PPTX
- XLSX → CSV
- PDF or OCR
- combining multiple JSON files
- cloud conversion

Office files are shown in preview as unsupported; the application does not use Office COM, LibreOffice, external CLIs, or network services.

## Build and test

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

The portable ZIP is created under `artifacts\portable\win-x64`.

## Usage

1. Select a folder.
2. Choose whether to include subfolders and select `TXT` or `Markdown`.
3. Click **Проверить файлы** and review paths and conflicts.
4. Click **Преобразовать файлы**.
5. Open the `_converted` folder shown in the final summary.

Invalid JSON is reported as an error for that file. Existing outputs are never overwritten.
