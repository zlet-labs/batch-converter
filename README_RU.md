# Zlet Batch Converter

<p align="center">
  <img src="docs/assets/zlet-batch-converter-hero.svg" alt="Zlet Batch Converter — локальный пакетный конвертер файлов для Windows от Zlet Labs" width="100%">
</p>

<p align="center">
  <a href="README.md">English</a> · <strong>Русский</strong>
</p>

<p align="center">
  <img alt="Версия v0.0.2" src="https://img.shields.io/badge/version-v0.0.2-2563eb">
  <img alt="PRE-ALPHA" src="https://img.shields.io/badge/status-PRE--ALPHA-f59e0b">
  <img alt="Windows x64" src="https://img.shields.io/badge/platform-Windows%20x64-0078D4">
  <img alt="Локальная обработка" src="https://img.shields.io/badge/processing-local%20only-16a34a">
  <img alt="MIT License" src="https://img.shields.io/badge/license-MIT-22c55e">
</p>

Zlet Batch Converter — небольшая локальная утилита для Windows, которая пакетно обрабатывает файлы в папках и подпапках. Она преобразует поддерживаемые старые форматы Microsoft Office, безопасно копирует современные Office-файлы, сохраняет относительную структуру папок и выполняет обработку на вашем компьютере.

> **v0.0.2 — PRE-ALPHA.** Установщик пока не подписан Authenticode, поэтому Windows может показать Unknown publisher или предупреждение SmartScreen. Microsoft Office в комплект не входит.

## Скачать v0.0.2

| Установщик Windows | Portable ZIP |
|---|---|
| **[⬇ Скачать установщик](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/ZletBatchConverter-v0.0.2-Setup-win-x64.exe)** | **[📦 Скачать portable](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/ZletBatchConverter-v0.0.2-win-x64.zip)** |
| `ZletBatchConverter-v0.0.2-Setup-win-x64.exe` | `ZletBatchConverter-v0.0.2-win-x64.zip` |

[Описание релиза](https://github.com/zlet-labs/batch-converter/releases/tag/v0.0.2) · [SHA-256](https://github.com/zlet-labs/batch-converter/releases/download/v0.0.2/SHA256SUMS.txt)

### Зачем использовать

| 🔒 Локально | ⚡ Пакетно | 🛡 Осторожно с файлами |
|---|---|---|
| Без аккаунтов и загрузки в облако | Папки и подпапки за один запуск | Существующие результаты не перезаписываются молча |
| Содержимое документов остаётся на ПК | Можно выбрать только нужные операции | Office-процессы не завершаются только по имени |

## Для Gemini Notebook и современных document/AI workflows

Подготовка старых коллекций документов для **Gemini Notebook** — один из практичных сценариев Zlet Batch Converter. По текущей справке Google Gemini Notebook поддерживает DOCX, PPTX, TXT и Markdown как типы источников, поэтому конвертер может помочь подготовить:

- `.doc` → `.docx`
- `.ppt` → `.pptx`
- `.json` → `.txt` или `.md`
- уже современные `.docx` и `.pptx` как безопасные копии без изменений

Поддерживаемые источники Gemini Notebook: [справка Google](https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=ru)

**Прямой интеграции с Gemini Notebook и автоматической загрузки нет.** Zlet Batch Converter обрабатывает файлы локально; вы сами решаете, загружать ли результат в Gemini Notebook или другой сервис и когда это делать.

## Поддерживаемые форматы

| Исходник | Результат | Требование |
|---|---|---|
| `.doc` | `.docx` | установлен Microsoft Word |
| `.xls` | `.xlsx` | установлен Microsoft Excel |
| `.ppt` | `.pptx` | установлен Microsoft PowerPoint |
| `.docx`, `.xlsx`, `.pptx` | безопасная копия без изменений | Office не нужен |
| `.json` | `.txt` или `.md` | Office не нужен |

Word, Excel и PowerPoint определяются независимо. Если одно приложение отсутствует, недоступна только связанная с ним legacy-конвертация; остальные операции в пачке могут продолжить работу.

> **Безопасность PowerPoint:** преобразование PPT не запускается, пока у пользователя уже открыт PowerPoint. Это защищает открытую презентацию от вмешательства. DOC/XLS и копирование готовых PPTX при этом остаются доступны.

## Быстрый старт

1. Скачайте установщик или portable ZIP выше.
2. Запустите `ZletBatchConverter.exe`.
3. Выберите исходную папку и выполните сканирование.
4. Проверьте Preview и отметьте нужные операции.
5. Выберите место/режим сохранения результата и запустите обработку.
6. Проверьте результаты по файлам и итоговую сводку пачки.

Готовые сборки self-contained для .NET 8, поэтому отдельно устанавливать .NET Runtime не требуется.

## Что получает пользователь

- Preview до начала обработки.
- Выбор отдельных операций перед запуском.
- Сохранение относительной структуры подпапок в результате.
- Статус, stage-based progress, размер исходника и время выполнения по каждому файлу.
- Безопасную кнопку Stop, которая не запускает следующие операции из очереди.
- Копирование списка преобразований только с относительными путями.
- Раздельные итоговые счётчики converted, copied, failed, conflict, unavailable, skipped и unselected.
- Более быструю пакетную Office-обработку за счёт безопасного переиспользования worker/session там, где это допустимо.

## Ограничения

Zlet Batch Converter всё ещё находится в статусе **PRE-ALPHA**. Сложные, повреждённые, защищённые паролем или использующие неподдерживаемые возможности legacy-документы могут не преобразоваться. Точность результата зависит от установленной версии Microsoft Office и особенностей конкретного документа.

Исходные файлы не должны намеренно изменяться, но для важных данных при тестировании pre-release ПО рекомендуется иметь резервную копию.

<details>
<summary><strong>Подробно о безопасности и приватности</strong></summary>

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

</details>

<details>
<summary><strong>Сборка из исходников и локальная упаковка</strong></summary>

Требования:

- Windows x64
- .NET 8 SDK

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

Сборка portable-пакета:

```powershell
.\scripts\publish-portable.ps1
```

Ожидаемый ZIP для v0.0.2:

```text
artifacts/portable/win-x64/ZletBatchConverter-v0.0.2-win-x64.zip
```

Сборка Windows installer через Inno Setup 6:

```powershell
.\scripts\build-installer.ps1
```

Скрипт установщика сначала собирает portable payload, затем создаёт Windows x64 installer и выводит его SHA-256 и статус Authenticode.

</details>

<details>
<summary><strong>Реальные Microsoft Office integration tests</strong></summary>

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

</details>

## Проект

Zlet Batch Converter — проект **Zlet Labs**: небольшие практичные self-serve инструменты без лишней SaaS-инфраструктуры.

[Zlet Labs](https://zlet.app/) · [GitHub Issues](https://github.com/zlet-labs/batch-converter/issues) · [Все релизы](https://github.com/zlet-labs/batch-converter/releases) · [MIT License](LICENSE)

Чек-лист ручной проверки релиза: [docs/manual-clean-machine-verification.md](docs/manual-clean-machine-verification.md)
