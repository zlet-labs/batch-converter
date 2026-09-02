# Zlet Batch Converter

![Version](https://img.shields.io/badge/version-v0.0.2-blue)
![Status](https://img.shields.io/badge/status-PRE--ALPHA-orange)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)
![Processing](https://img.shields.io/badge/processing-local%20only-success)

[English](README.md) · **Русский**

Zlet Batch Converter — небольшая локальная утилита для Windows, которая пакетно обрабатывает файлы в папках и подпапках. Она преобразует поддерживаемые старые форматы Microsoft Office, безопасно копирует уже современные Office-файлы и выполняет обработку на компьютере пользователя.

**Без аккаунтов. Без загрузки файлов в облако. Содержимое документов никуда не отправляется.**

> **Текущая версия исходников: v0.0.2 PRE-ALPHA.** Проверенные бинарные сборки публикуются отдельно на странице GitHub Releases после ручной проверки. Если `v0.0.2` там ещё нет, исходники в репозитории новее последней опубликованной сборки.

## Что умеет

- Сканирует выбранную папку и подпапки.
- Показывает Preview до начала обработки.
- Позволяет выбрать, какие операции запускать.
- Преобразует поддерживаемые старые Office-файлы через установленный Microsoft Office.
- Безопасно копирует современные Office-файлы без преобразования.
- Сохраняет относительную структуру подпапок.
- Никогда молча не перезаписывает существующий файл или каталог результата.
- Показывает статус, прогресс, размер исходника и время выполнения по каждому файлу.
- Позволяет остановить активную пачку без завершения посторонних пользовательских Office-процессов.
- Позволяет скопировать список планируемых преобразований только с относительными путями.

## Поддерживаемые форматы

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Microsoft Office не нужен |
| `.json` | `.txt` или `.md` | Microsoft Office не нужен |

Word, Excel и PowerPoint определяются независимо. Если одно приложение отсутствует, недоступен только связанный с ним legacy-формат; остальные операции в пачке могут выполняться дальше.

Microsoft Office **не входит в комплект** Zlet Batch Converter.

### Ограничение безопасности PowerPoint

PowerPoint использует общий COM-процесс. Чтобы не вмешиваться в уже открытую пользователем презентацию, преобразование PPT не запускается, пока работает `POWERPNT`. Закройте PowerPoint и повторите преобразование.

Это ограничение не блокирует DOC/XLS и копирование готовых PPTX без изменений.

## Быстрый старт

1. Скачайте проверенную сборку со страницы [GitHub Releases](https://github.com/zlet-labs/batch-converter/releases) или соберите текущие исходники самостоятельно.
2. Запустите `ZletBatchConverter.exe`.
3. Выберите исходную папку.
4. Выполните сканирование и проверьте запланированные действия.
5. Отметьте нужные операции.
6. Выберите место/режим сохранения результата и запустите обработку.
7. Проверьте итоговую сводку и созданные файлы.

В готовых сборках .NET поставляется self-contained, отдельно устанавливать .NET Runtime не требуется.

## Загрузка и релизы

Опубликованные сборки находятся на странице [Releases](https://github.com/zlet-labs/batch-converter/releases).

В релиз могут входить:

- `ZletBatchConverter-v<version>-Setup-win-x64.exe` — установщик Windows;
- `ZletBatchConverter-v<version>-win-x64.zip` — portable-архив.

Используйте только файлы, реально прикреплённые к опубликованному GitHub Release. Номер версии в исходниках сам по себе не означает, что соответствующие бинарники уже опубликованы.

Установщик пока не подписан Authenticode, поэтому Windows может показать предупреждение Unknown publisher или SmartScreen.

## Безопасность и приватность

Конвертер специально сделан локальным и осторожным по отношению к пользовательским файлам.

- Файлы обрабатываются локально и не загружаются наружу.
- UI-процесс не выполняет Office COM automation напрямую.
- Office-конвертация идёт через изолированный STA worker-процесс.
- Legacy-файлы открываются read-only.
- Макросы, диалоги и добавление в Recent/MRU отключены для Office-сессий, которыми управляет worker.
- Исходник должен оставаться внутри выбранной исходной папки.
- Reparse-point файлы и папки пропускаются.
- SHA-256 исходника проверяется до и после обработки.
- Результат сначала создаётся во временном/staging расположении и проверяется перед финальным перемещением.
- Существующие файлы и каталоги результата не перезаписываются.
- Office-процессы никогда не завершаются только по имени процесса.
- Принудительное завершение допускается только для app-owned процесса с подтверждёнными PID и временем старта.
- Ошибка одного файла не останавливает остальные выбранные операции автоматически.

Техническая диагностика может содержать коды ошибок и служебные данные процесса. Она не должна содержать содержимое документов, секреты или полные локальные пути документов.

## Сборка из исходников

Требования:

- Windows x64;
- .NET 8 SDK.

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

## Сборка portable-пакета

```powershell
.\scripts\publish-portable.ps1
```

Для v0.0.2 ожидаемое имя ZIP:

```text
artifacts/portable/win-x64/ZletBatchConverter-v0.0.2-win-x64.zip
```

Portable-пакет содержит self-contained .NET 8 приложение и worker. Microsoft Office, Python, Java, исходники и тестовые фикстуры в него не входят.

## Сборка установщика Windows

Для создания installer используется Inno Setup 6:

```powershell
.\scripts\build-installer.ps1
```

Скрипт сначала собирает portable payload, затем создаёт установщик Windows x64. В конце он выводит SHA-256 установщика и статус Authenticode.

## Office integration tests

Реальные интеграционные тесты Office запускаются только явно, потому что требуют установленный Microsoft Office и настоящие legacy-файлы:

```powershell
$env:ZLET_OFFICE_INTEGRATION = "1"
$env:ZLET_OFFICE_WORD_FIXTURE = "C:\fixtures\sample.doc"
$env:ZLET_OFFICE_WORD_BATCH_FIXTURE_DIR = "C:\fixtures\word-batch"
$env:ZLET_OFFICE_EXCEL_FIXTURE = "C:\fixtures\sample.xls"
$env:ZLET_OFFICE_POWERPOINT_FIXTURE = "C:\fixtures\sample.ppt"
dotnet test FolderConverter.sln -c Release --filter Category=OfficeIntegration
```

Если нужное приложение Office или фикстура отсутствует, соответствующий integration test пропускается, а не считается успешно пройденным.

## Ограничения

Zlet Batch Converter всё ещё находится в статусе **PRE-ALPHA**.

Сложные, повреждённые, защищённые паролем или использующие неподдерживаемые возможности legacy-документы могут не преобразоваться. Точность результата зависит от установленной версии Microsoft Office и особенностей конкретного документа. Проект не обещает успешную legacy-конвертацию на компьютере без соответствующего приложения Office.

Исходные файлы не должны намеренно изменяться, но для важных данных при тестировании pre-release ПО рекомендуется иметь резервную копию.

## Проверка перед релизом

Перед публикацией бинарных сборок используется ручной clean-machine checklist:

[docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md)

## Проект

Zlet Batch Converter — проект **Zlet Labs**: небольшие практичные self-serve инструменты без лишней SaaS-инфраструктуры.

- Issues: [GitHub Issues](https://github.com/zlet-labs/batch-converter/issues)
- Releases: [GitHub Releases](https://github.com/zlet-labs/batch-converter/releases)
- Лицензия: [MIT](LICENSE)
- English README: [README.md](README.md)
