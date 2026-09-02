# Zlet Batch Converter v0.0.2

[English](#english) · [Русский](#русский)

> **PRE-ALPHA · Windows x64 · Local processing only**

## English

Zlet Batch Converter v0.0.2 focuses on faster Office batch conversion, safer cancellation, clearer per-file progress, and a more useful final batch summary.

### Downloads

- **[Windows installer](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/ZletBatchConverter-v0.0.2-Setup-win-x64.exe)** — `ZletBatchConverter-v0.0.2-Setup-win-x64.exe`
- **[Portable ZIP](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/ZletBatchConverter-v0.0.2-win-x64.zip)** — `ZletBatchConverter-v0.0.2-win-x64.zip`
- **[SHA-256 checksums](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/SHA256SUMS.txt)**

Installer SHA-256:
`519855e75ed63385b914bf7cb37efa7a42717728ae6420e6291dcb27e8ed58d5`

Portable ZIP SHA-256:
`998854d1e64f605fd140ba60c52d6e22257265905fdb4e254a80cfe57e2643b0`

### Highlights

- **Much faster multi-file Office batches.** Word/Excel/PowerPoint worker sessions are reused where safe instead of starting a fresh worker for every file.
- **Safe Stop control.** Stop prevents new queued operations from starting and does not kill unrelated user Office processes.
- **Per-file progress and timing.** Active rows show observable stage-based progress and elapsed execution time; completed values remain visible.
- **Source file size in Preview.** File size is visible before processing and remains available afterward.
- **Clearer statuses.** Ready, in-progress, success, warning, error, and cancelled states remain text-labelled and visually distinct.
- **Copy conversion list.** Planned real conversions can be copied as relative `source → result` paths without exposing absolute local paths.
- **Better final summary.** Converted, copied, failed, conflict, unavailable, skipped, and unselected counts are shown separately with total elapsed time.
- **Improved rerun behavior.** Previously unselected ready rows can be selected and processed later without rebuilding Preview.
- **Failure diagnostics survive Stop/rerun.** ErrorCode and HRESULT details remain available without duplicate reporting.
- **Stronger Office ownership safety.** Forced cleanup requires exact app-owned process identity using PID + start time; processes are never killed by name alone.

### Supported operations

| Source | Result | Requirement |
|---|---|---|
| `.doc` | `.docx` | Microsoft Word installed |
| `.xls` | `.xlsx` | Microsoft Excel installed |
| `.ppt` | `.pptx` | Microsoft PowerPoint installed |
| `.docx`, `.xlsx`, `.pptx` | unchanged safe copy | Office not required |
| `.json` | `.txt` or `.md` | Office not required |

Microsoft Office is not included.

### Important notes

- Files are processed locally; document contents are not uploaded to a cloud service.
- Existing output files/directories are not silently overwritten.
- PPT conversion is refused while user PowerPoint is already running, to avoid interfering with an open presentation.
- The installer is currently unsigned, so Windows may show an Unknown publisher or SmartScreen warning.
- Complex, corrupted, password-protected, or unsupported legacy files may fail to convert.
- Conversion fidelity depends on the installed Microsoft Office version and document features.

### Verification

- GitHub-hosted release build completed successfully.
- Automated tests: **242 passed, 4 Office integration skipped, 0 failed**.
- Real Office/manual verification was completed separately before publication.
- Release assets were published with SHA-256 checksums above.

Full checklist: [`docs/manual-clean-machine-verification.md`](manual-clean-machine-verification.md)

---

## Русский

Zlet Batch Converter v0.0.2 — PRE-ALPHA релиз для Windows x64 с упором на ускорение пакетной Office-конвертации, безопасную остановку, понятный прогресс по файлам и более полезную итоговую сводку.

### Скачать

- **[Установщик Windows](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/ZletBatchConverter-v0.0.2-Setup-win-x64.exe)** — `ZletBatchConverter-v0.0.2-Setup-win-x64.exe`
- **[Portable ZIP](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/ZletBatchConverter-v0.0.2-win-x64.zip)** — `ZletBatchConverter-v0.0.2-win-x64.zip`
- **[SHA-256](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/SHA256SUMS.txt)**

SHA-256 установщика:
`519855e75ed63385b914bf7cb37efa7a42717728ae6420e6291dcb27e8ed58d5`

SHA-256 portable ZIP:
`998854d1e64f605fd140ba60c52d6e22257265905fdb4e254a80cfe57e2643b0`

### Главное в v0.0.2

- **Пакетная Office-конвертация стала заметно быстрее.** Worker/session Word, Excel и PowerPoint переиспользуется там, где это безопасно, вместо запуска нового worker для каждого файла.
- **Безопасная кнопка Stop.** После остановки новые операции из очереди не запускаются, посторонние пользовательские Office-процессы не завершаются.
- **Прогресс и время по каждому файлу.** Активная строка показывает наблюдаемый stage-based progress и фактическое время выполнения; завершённые значения сохраняются.
- **Размер исходника в Preview.** Размер виден до обработки и остаётся после завершения.
- **Более понятные статусы.** Ready, in-progress, success, warning, error и cancelled визуально различимы и сохраняют явные текстовые подписи.
- **Копирование списка преобразований.** Реальные планируемые конверсии можно скопировать как относительные пути `исходник → результат` без раскрытия абсолютных локальных путей.
- **Улучшена итоговая сводка.** Converted, copied, failed, conflict, unavailable, skipped и unselected показываются отдельно вместе с общим временем выполнения.
- **Улучшен повторный запуск.** Ранее не выбранные готовые строки можно выбрать и обработать позже без нового Scan.
- **Диагностика ошибок не теряется после Stop/rerun.** ErrorCode и HRESULT сохраняются без дублирования.
- **Усилена безопасность владения Office-процессами.** Принудительное завершение возможно только для подтверждённого app-owned процесса по PID + времени запуска; завершения только по имени процесса нет.

### Поддерживаемые операции

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Office не нужен |
| `.json` | `.txt` или `.md` | Office не нужен |

Microsoft Office в комплект не входит.

### Важно

- Файлы обрабатываются локально, содержимое документов не загружается в облачный сервис.
- Существующие файлы/каталоги результата не перезаписываются молча.
- PPT-конвертация не запускается, пока у пользователя уже открыт PowerPoint, чтобы не вмешиваться в открытую презентацию.
- Установщик пока не подписан Authenticode, поэтому Windows может показать Unknown publisher или предупреждение SmartScreen.
- Сложные, повреждённые, защищённые паролем или неподдерживаемые legacy-файлы могут не преобразоваться.
- Результат зависит от установленной версии Microsoft Office и возможностей конкретного документа.

### Проверка

- Release build в GitHub Actions завершён успешно.
- Автоматические тесты: **242 passed, 4 Office integration skipped, 0 failed**.
- Реальная Office/manual проверка выполнена отдельно до публикации релиза.
- Для опубликованных assets зафиксированы SHA-256 выше.

Полный чек-лист: [`docs/manual-clean-machine-verification.md`](manual-clean-machine-verification.md)
