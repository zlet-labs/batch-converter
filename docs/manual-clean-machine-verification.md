# Manual verification checklist

Record the Windows version, display resolution, scaling, LibreOffice runtime
version, portable ZIP name, and tester for every release candidate. Do not mark
an item passed from unit tests alone.

## Portable and privacy

- Extract the complete ZIP to a path containing spaces and Cyrillic characters.
- Verify `ZletBatchConverter.exe`, `runtime\libreoffice`, `licenses`,
  `THIRD_PARTY_NOTICES.md`, and `README_PORTABLE.txt` are present.
- Launch without administrator rights on a machine without separately installed
  .NET Runtime, Microsoft Office, or LibreOffice.
- Confirm no installation prompt, automatic download, network request,
  analytics, or telemetry occurs.
- Confirm the runtime version matches `licenses\libreoffice\VERSION.txt`.
- Review the exact license documents copied from the selected runtime package.

## Mixed-folder workflow

Prepare only synthetic fixtures in a folder with nested paths, spaces,
Cyrillic, and Unicode:

- valid and invalid JSON;
- DOC with Cyrillic, a table, and an image;
- XLS with Cyrillic, a table, and a formula;
- PPT with multiple slides and an image;
- DOCX, XLSX, PPTX, ODT, ODS, ODP;
- PDF, PNG, ZIP, and an unknown extension.

Verify:

- default rules are JSON→TXT, DOC→DOCX, XLS→XLSX, PPT→PPTX;
- source and output paths accept typed and pasted Unicode/UNC paths; Enter
  starts scan only for a valid source;
- all Ready rows are selected after scan; individual selection, «Выбрать все»,
  «Снять выбор», and «Инвертировать» work across filters;
- modern Office, OpenDocument, PDF, image, archive, and unknown defaults are
  «Не трогать»;
- changing every permitted rule rebuilds preview immediately;
- XLSX has PDF and «Не трогать», but no CSV option;
- preview shows only relative paths; full paths appear only in tooltips;
- filters «Все», «К преобразованию», «Не трогаем», «Конфликты», and «Ошибки»
  show the correct rows;
- folder output preserves source subfolders at the explicitly selected root;
- only the selected output folder or exact result ZIP is excluded from scan;
- `~$*` and directory reparse points are skipped.

## Processing and failures

- Run JSON→TXT and JSON→Markdown.
- Run DOC/XLS/PPT to modern formats and PDF with bundled runtime present.
- Run at least one DOCX/XLSX/PPTX and ODT/ODS/ODP to PDF.
- Confirm invalid JSON fails without partial output and does not stop the batch.
- Confirm missing runtime produces «LibreOffice не найден в portable package».
- Confirm unreadable source, timeout, malformed output, and process-start
  failures show short messages without command line or stack trace.
- Create both a target file conflict and a target directory conflict.
- Repeat scan and conversion; no existing target is overwritten.
- Byte-compare every original before and after processing.
- Confirm partial temp files and LibreOffice profiles are removed.
- Run folder output and ZIP output, including partial success, zero success, and
  an existing ZIP conflict. Confirm ZIP contains only successful selected files.
- Confirm progress and current relative file update without freezing the UI.
- Confirm the final report shows successes, errors, conflicts, unavailable,
  skipped, not-selected count, and the correct folder/ZIP result action.

## Display checks

- 1366×768 at 100% and 125%.
- 1920×1080 at 100%, 125%, and 150%.
- Verify compact startup size, readable rule selectors, no white ComboBox,
  usable scrolling, visible CTA, progress, and final report.

## Release decision

- Confirm ZIP contains no repository sources, `bin`, `obj`, test fixtures,
  personal documents, local absolute paths, credentials, tokens, or API keys.
- Record ZIP path and exact size.
- Do not publish if runtime licensing, source availability, clean-machine launch,
  conversion fidelity, or any safety check remains unverified.

Full e2e not run by agent. Local verification required before merge.

## Current local agent verification

- Official LibreOffice 26.2.4 Windows x86-64 package acquired and hash-checked;
  runtime reports 26.2.4.2.
- 15/15 synthetic LibreOffice integration tests passed with no skips.
- Renamed portable ZIP audit passed: 589,149,052 bytes; extracted size
  1,771,068,495 bytes; 20,275 files.
- `ZletBatchConverter.exe` launch from a new extraction passed with
  `ZLET_LIBREOFFICE_PATH` cleared. The process remained responsive with title
  `Zlet Batch Converter v0.0.0`; bundled `program\soffice.com` was present.
- The previous local bundled-runtime JSON + DOC conversion completed with
  2 successes, 0 failures, 0 conflicts, and 0 unavailable operations. The
  expanded selection/folder/ZIP workflow still requires human UI e2e review.
- Runtime-present screenshots were retained only as ignored local artifacts.
- No separate clean Windows machine or VM was available; that release gate is
  not complete.
