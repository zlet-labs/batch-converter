# Zlet Folder Converter

Zlet Folder Converter - локальный Windows-прототип для сканирования папок со старыми офисными файлами и построения безопасного плана конвертации.

Текущий прототип не выполняет реальную конвертацию документов. Он находит `.doc`, `.xls` и `.ppt`, предлагает пути результата в `_converted`, защищает существующие target-файлы и честно показывает статус `Unsupported`, пока embedded .NET adapter не пройдёт лицензионную проверку и synthetic validation.

## Статус прототипа

- Сканирование папки: есть.
- Preview операций: есть.
- Проверка конфликтов target-файлов: есть.
- Portable Windows x64 packaging: есть.
- Реальная DOC/XLS/PPT конвертация: пока не поддерживается.

## Поддерживаемые mappings

В этом прототипе нет поддерживаемых mappings.

## Неподдерживаемые mappings

- `.doc` в `.docx`
- `.xls` в `.xlsx`
- `.ppt` в `.pptx`

Эти mappings останутся неподдерживаемыми, пока полностью embedded .NET adapter не пройдёт проверку лицензии и synthetic validation.

## Privacy и безопасность файлов

- Приложение работает локально.
- Файлы не отправляются на сервер или cloud API.
- Нет backend, analytics, telemetry или remote config.
- Microsoft Office и LibreOffice не требуются.
- Оригиналы не изменяются, не удаляются, не перемещаются и не перезаписываются.
- Существующие target-файлы считаются конфликтами и не перезаписываются.
- Планируемые результаты находятся в `<selected-folder>/_converted`.

## Требования Windows

- Windows 10 или Windows 11 x64.
- Для portable ZIP не нужно устанавливать .NET Runtime.
- Для разработки нужен .NET 8 SDK.

## Build

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
```

## Tests

```powershell
dotnet test FolderConverter.sln -c Release
```

## Portable ZIP

Основной формат распространения ранних версий - portable self-contained ZIP для Windows x64.

Пользователь:

1. Скачивает ZIP.
2. Распаковывает его в локальную папку.
3. Запускает `ZletFolderConverter.exe`.

Локальная сборка portable package:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

С версией:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 -Version "0.1.0-alpha"
```

Артефакты создаются в `artifacts/portable/win-x64` и не коммитятся.

## Текущие ограничения

- Conversion adapter пока не включён.
- Нет installer, auto-update, service, registry writes, file associations, scheduled tasks или shell extensions.
- Нет drag and drop.
- Clean-machine verification агентом не выполнялась.

## Исследование конвертации

См. [CONVERSION_RESEARCH.md](CONVERSION_RESEARCH.md).

## Security и reporting

Не используйте реальные чувствительные документы как test fixtures. Если нашли проблему безопасности, создайте GitHub issue без вложения приватных файлов или содержимого документов.

## Лицензия

MIT. См. [LICENSE](LICENSE).
