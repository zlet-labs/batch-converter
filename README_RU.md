# Zlet Batch Converter v0.0.0

Zlet Batch Converter — локальный Windows-прототип для пакетной обработки файлов
с сохранением относительной структуры подпапок. Файлы никуда не отправляются.

## Поддерживаемые операции

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Office не нужен |
| `.json` | `.txt` или `.md` | Office не нужен |

Word, Excel и PowerPoint проверяются отдельно. Отсутствие одного приложения
отключает только связанный с ним legacy-формат и не блокирует всю пачку.
Microsoft Office в комплект не входит.

PowerPoint использует общий COM-процесс. Для защиты пользовательских документов
PPT не преобразуется, пока уже запущен `POWERPNT`: закройте PowerPoint и
повторите попытку. Это не блокирует DOC, XLS и копирование готовых PPTX.

## Безопасность

- COM-автоматизация не выполняется в UI-процессе.
- Одна операция запускается в отдельном STA worker-процессе .NET.
- UI передаёт JSON через stdin без shell interpolation.
- Legacy-файл открывается только для чтения; макросы, диалоги и добавление в
  Recent/MRU отключены.
- Исходник обязан находиться внутри выбранной исходной папки.
- Reparse/symlink-файлы и reparse-папки пропускаются.
- SHA-256 исходника проверяется до и после обработки.
- Результат сначала создаётся во временной папке и проверяется как DOCX/XLSX/PPTX.
- Финальная публикация выполняется атомарным перемещением; существующий файл или
  каталог никогда не перезаписывается.
- Worker сообщает PID созданного Office-процесса сразу после COM activation,
  до изменения настроек.
- При timeout сначала завершается worker, затем кратко дочитывается lifecycle
  output и только после этого может завершаться Office PID, подтверждённый как
  новый экземпляр worker. Завершения процессов по имени нет.
- Ошибка одного файла не останавливает остальные.

Техническая диагностика содержит только коды ошибок и служебные признаки, без
содержимого документов, секретов и полных путей документов.

## Сборка и тесты

Нужны Windows x64 и .NET 8 SDK:

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Реальные Office integration tests запускаются только явно и требуют legacy
фикстуры:

```powershell
$env:ZLET_OFFICE_INTEGRATION = "1"
$env:ZLET_OFFICE_WORD_FIXTURE = "C:\fixtures\sample.doc"
$env:ZLET_OFFICE_EXCEL_FIXTURE = "C:\fixtures\sample.xls"
$env:ZLET_OFFICE_POWERPOINT_FIXTURE = "C:\fixtures\sample.ppt"
dotnet test FolderConverter.sln -c Release --filter Category=OfficeIntegration
```

Если приложение Office или соответствующая фикстура отсутствует, тест
пропускается, а не считается пройденным.

## Portable

```powershell
.\scripts\publish-portable.ps1
```

Скрипт создаёт self-contained .NET 8 пакет Windows x64 и ZIP:

`artifacts/portable/win-x64/ZletBatchConverter-v0.0.0-win-x64.zip`

В пакете нет Microsoft Office, Python, Java, исходников, тестов и локальных
конфигов. GitHub Release до отдельного ревью и ручной проверки публиковать
нельзя.

## Ограничения

Оригиналы не должны изменяться. Сложные, повреждённые, защищённые паролем или
использующие неподдержанные возможности документы могут не преобразоваться.
Результат зависит от установленной версии Office. Эта версия не обещает
конвертацию на компьютере без соответствующего приложения Microsoft Office.

Ручной чек-лист:
[docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md).
