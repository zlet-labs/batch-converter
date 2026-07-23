# Zlet Folder Converter

Локальное Windows-приложение для массового преобразования смешанных файлов по
правилам. Пользователь выбирает папку, назначает одно действие каждому
найденному формату, проверяет preview и запускает batch. Результаты сохраняются
в `<выбранная папка>\_converted` с сохранением структуры подпапок.

Файлы не отправляются в интернет. Оригиналы не перезаписываются, не удаляются,
не перемещаются и намеренно не изменяются. Существующий target-файл или
директория считается конфликтом.

## Правила

| Исходный формат | Допустимые действия | По умолчанию |
| --- | --- | --- |
| JSON | TXT, Markdown, Не трогать | TXT |
| DOC | DOCX, PDF, Не трогать | DOCX |
| XLS | XLSX, PDF, Не трогать | XLSX |
| PPT | PPTX, PDF, Не трогать | PPTX |
| DOCX | PDF, Не трогать | Не трогать |
| XLSX | PDF, Не трогать | Не трогать |
| PPTX | PDF, Не трогать | Не трогать |
| ODT | DOCX, PDF, Не трогать | Не трогать |
| ODS | XLSX, PDF, Не трогать | Не трогать |
| ODP | PPTX, PDF, Не трогать | Не трогать |
| PDF, изображения, архивы, неизвестные | Не трогать | Не трогать |

JSON обрабатывает встроенный `JsonConversionAdapter`. Для Office и
OpenDocument используется локальный bundled LibreOffice runtime за абстракцией
движка. Microsoft Office COM, облачная конвертация, скачивание runtime во время
работы, аналитика и телеметрия не используются.

LibreOffice запускается скрыто в headless-режиме с отдельным временным профилем
и рабочей папкой для каждой операции. OOXML проверяется как ZIP с обязательными
базовыми частями, PDF — по сигнатуре. Совместимость и визуальная точность зависят
от LibreOffice и исходного документа; 100% сохранение форматирования не
гарантируется.

XLSX → CSV по листам не входит в ZL-041.

## Portable package

Это self-contained .NET-приложение, но не один физический EXE:

```text
ZletFolderConverter/
  ZletFolderConverter.exe
  runtime/
    libreoffice/
  licenses/
  THIRD_PARTY_NOTICES.md
  README_PORTABLE.txt
```

Пользователю не требуется отдельно устанавливать .NET Runtime, Microsoft Office
или LibreOffice. Основной объём ZIP занимает выбранный runtime; точный размер
нужно измерять на фактическом release artifact.

LibreOffice binaries не коммитятся в Git. Сборка требует явный путь:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 `
  -LibreOfficePath "C:\path\to\LibreOffice"
```

Скрипт прекращает работу, если не подтверждены runtime, версия или лицензионный
документ из выбранного пакета. Публичный artifact запрещён до проверки
конкретной сборки LibreOffice и сгенерированных notices.

## Сборка и тесты

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Opt-in integration tests запускаются только с реальным runtime:

```powershell
$env:ZLET_LIBREOFFICE_PATH = "C:\path\to\LibreOffice"
dotnet test FolderConverter.sln -c Release --filter Category=LibreOfficeIntegration
```

Для локальной разработки можно использовать игнорируемый
`ZletFolderConverter.local.json` по пустому примеру в репозитории. Реальный
локальный путь нельзя коммитить, логировать или включать в export.

## Защита данных

- корневая `_converted` исключается из scan, вложенная
  `archive\_converted` остаётся пользовательской папкой;
- временные Office-файлы `~$*` пропускаются;
- reparse point, junction и symlink directory не обходятся;
- path traversal и target за пределами корневой `_converted` отклоняются;
- ошибка одного файла не останавливает batch;
- содержимое документов, command line, пароли, токены и ключи не логируются.
