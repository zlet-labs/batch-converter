# Changelog

All notable changes to Zlet Folder Converter will be documented here.

The project follows a simple release history suitable for a small desktop utility. Public versions use semantic versioning where practical, and release assets are published through GitHub Releases.

## Unreleased

### Added

- Local JSON → TXT conversion.
- Local JSON → Markdown conversion.
- Recursive folder scanning with preserved relative paths.
- Operation preview and conflict detection.
- Portable Windows x64 packaging.

### Safety

- Source files are not modified, moved, deleted, or overwritten.
- Existing target files and directories are treated as conflicts.
- Root `_converted` output, Office temporary files, and reparse directories are skipped during scanning.
- Per-file errors do not stop the remaining batch.

### Known limitations

- No public release asset has been published yet.
- DOC → DOCX, XLS → XLSX, and PPT → PPTX are not implemented in the current alpha.
- Clean-machine verification is required before the first release.

## Release links

Published releases and downloadable Windows packages belong on the [GitHub Releases page](https://github.com/zlet-labs/folder-converter/releases).
