# Zlet Batch Converter v0.0.2 — Release Notes

[English](#english) · [Русский](#русский)

> **PRE-ALPHA.** This document is the release description for v0.0.2. Publish it together with verified binaries only after the release build and manual verification are complete.

## English

Zlet Batch Converter v0.0.2 is a PRE-ALPHA Windows x64 release focused on faster Office batch processing, safer cancellation, and a clearer preview/result workflow.

### What's new in v0.0.2

- **Much faster multi-file Office batches.** Word/Excel/PowerPoint worker sessions are reused where safe instead of starting a fresh worker for every file.
- **Safe Stop control.** An active batch can be stopped without launching new queued operations and without killing unrelated user Office processes.
- **Per-file progress and timing.** Active rows show stage-based progress and elapsed execution time; completed rows keep their final values.
- **Source file size in Preview.** File size is visible before processing and remains available afterward.
- **Clearer status styling.** Ready, in-progress, success, warning, error and cancelled states are easier to distinguish while still keeping explicit text labels.
- **Copy conversion list.** Planned real conversions can be copied as relative `source → result` paths without exposing absolute local paths.
- **Better final batch summary.** Converted, copied, failed, conflict, unavailable, skipped and unselected counts are shown separately, together with total elapsed time.
- **Improved rerun behavior.** Previously unselected ready rows can be selected and processed later without rebuilding the Preview.
- **Failure diagnostics survive Stop/rerun.** Error codes and HRESULT details are retained without duplicate reporting.
- **Stronger Office process ownership safety.** Forced cleanup still requires exact app-owned process identity using PID + start time; processes are never killed by name alone.

### Supported operations

| Source | Result | Requirement |
|---|---|---|
| `.doc` | `.docx` | Microsoft Word installed |
| `.xls` | `.xlsx` | Microsoft Excel installed |
| `.ppt` | `.pptx` | Microsoft PowerPoint installed |
| `.docx`, `.xlsx`, `.pptx` | unchanged safe copy | Office not required |
| `.json` | `.txt` or `.md` | Office not required |

Microsoft Office is not included.

### Downloads

Expected release assets:

- `ZletBatchConverter-v0.0.2-Setup-win-x64.exe`
- `ZletBatchConverter-v0.0.2-win-x64.zip`

Do not publish or link these names as downloadable files until the actual release assets have been built, verified and attached to GitHub Release `v0.0.2`.

### Important notes

- Files are processed locally; document contents are not uploaded to a cloud service.
- Existing output files/directories are not silently overwritten.
- PPT conversion is refused while user PowerPoint is already running, to avoid interfering with an open presentation.
- The installer is currently unsigned, so Windows may show an unknown-publisher or SmartScreen warning.
- Complex, corrupted, password-protected or unsupported legacy files may fail to convert.
- Conversion fidelity depends on the installed Microsoft Office version and document features.

### Verification before publication

Before publishing v0.0.2 binaries, verify at minimum:

- Release build succeeds with no errors.
- Automated tests pass; Office integration skips are explicitly distinguished from passes.
- Real Office integration checks pass for the formats available on the verification machine.
- Portable ZIP launches from a clean extracted directory.
- Installer install/launch/uninstall flow is checked.
- Stop/cancellation does not leave app-owned worker/Office processes behind and does not affect unrelated user Office processes.
- Existing outputs are not overwritten and partial temporary output is cleaned up.
- SHA-256 is recorded for each published asset.

Use `docs/manual-clean-machine-verification.md` for the full checklist.

## Русский

Zlet Batch Converter v0.0.2 — PRE-ALPHA релиз для Windows x64 с упором на ускорение пакетной Office-конвертации, безопасную остановку и более понятную работу с Preview и результатами.

### Что нового в v0.0.2

- **Пакетная Office-конвертация стала заметно быстрее.** Worker/session Word, Excel и PowerPoint переиспользуется там, где это безопасно, вместо запуска нового worker для каждого файла.
- **Безопасная кнопка «Остановить».** Активную пачку можно остановить без запуска следующих операций и без завершения посторонних пользовательских процессов Office.
- **Прогресс и время по каждому файлу.** Активная строка показывает stage-based progress и фактическое время выполнения; завершённые значения фиксируются.
- **Размер исходного файла в Preview.** Размер виден ещё до обработки и остаётся после завершения.
- **Более понятные статусы.** Ready, in-progress, success, warning, error и cancelled визуально различимы, при этом текст статуса остаётся явным.
- **Копирование списка преобразований.** Реальные запланированные конверсии можно скопировать как относительные пути `исходник → результат` без раскрытия абсолютных локальных путей.
- **Раздельная итоговая сводка.** Отдельно показываются преобразованные, скопированные, ошибки, конфликты, недоступные, пропущенные и не выбранные файлы, плюс общее время выполнения.
- **Улучшен повторный запуск.** Ранее не выбранные готовые строки можно выбрать и обработать позже без нового Scan.
- **Диагностика ошибок не теряется после Stop/rerun.** ErrorCode и HRESULT сохраняются без дублирования.
- **Усилена безопасность владения Office-процессами.** Принудительное завершение возможно только для точно подтверждённого app-owned процесса по PID + времени запуска; завершения процессов только по имени нет.

### Поддерживаемые операции

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Office не нужен |
| `.json` | `.txt` или `.md` | Office не нужен |

Microsoft Office в комплект не входит.

### Файлы релиза

Ожидаемые assets:

- `ZletBatchConverter-v0.0.2-Setup-win-x64.exe`
- `ZletBatchConverter-v0.0.2-win-x64.zip`

Не публиковать и не указывать их как доступные для скачивания, пока реальные файлы не собраны, не проверены и не прикреплены к GitHub Release `v0.0.2`.

### Важно

- Файлы обрабатываются локально, содержимое документов не загружается в облачный сервис.
- Существующие файлы/каталоги результата не перезаписываются молча.
- PPT-конвертация не запускается, пока у пользователя уже открыт PowerPoint, чтобы не вмешиваться в открытую презентацию.
- Установщик пока не подписан Authenticode, поэтому Windows может показать Unknown publisher или предупреждение SmartScreen.
- Сложные, повреждённые, защищённые паролем или неподдерживаемые legacy-файлы могут не преобразоваться.
- Результат зависит от установленной версии Microsoft Office и возможностей конкретного документа.

### Проверка перед публикацией

Перед публикацией бинарников v0.0.2 проверить минимум:

- Release build собирается без ошибок.
- Автоматические тесты проходят; Office integration skips явно отличаются от passed.
- Реальные Office integration checks проходят для форматов, доступных на проверочной машине.
- Portable ZIP запускается после распаковки в чистую папку.
- Проверен install/launch/uninstall flow установщика.
- Stop/cancellation не оставляет app-owned worker/Office процессы и не затрагивает посторонние пользовательские Office процессы.
- Существующие результаты не перезаписываются, временные/частичные outputs очищаются.
- Для каждого опубликованного asset записан SHA-256.

Полный чек-лист: `docs/manual-clean-machine-verification.md`.
