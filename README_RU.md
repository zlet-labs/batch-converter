# Zlet Batch Converter

`v0.0.0` pre-alpha — локальное Windows-приложение для массового преобразования
файлов по правилам. Репозиторий пока намеренно называется `folder-converter`;
его адрес остаётся `https://github.com/zlet-labs/folder-converter`.
Переименование возможно только отдельно после review и merge PR #3.

Путь исходной папки можно ввести или вставить вручную либо выбрать через
«Обзор». Сканирование запускается кнопкой или Enter. После scan пользователь
настраивает правила и выбирает отдельные готовые операции. Доступны «Выбрать
все», «Снять выбор», «Инвертировать» и фильтры, которые не сбрасывают выбор.

Результат сохраняется в выбранную папку или ZIP с сохранением относительной
структуры подпапок. Оригиналы не перезаписываются, не удаляются, не перемещаются
и намеренно не изменяются. Существующие targets и ZIP считаются конфликтами.

## Правила преобразования

| Исходный формат | Допустимые действия | По умолчанию |
| --- | --- | --- |
| JSON | TXT, Markdown, Не трогать | TXT |
| DOC | DOCX, PDF, Не трогать | DOCX |
| XLS | XLSX, PDF, Не трогать | XLSX |
| PPT | PPTX, PDF, Не трогать | PPTX |
| DOCX, XLSX, PPTX | PDF, Не трогать | Не трогать |
| ODT | DOCX, PDF, Не трогать | Не трогать |
| ODS | XLSX, PDF, Не трогать | Не трогать |
| ODP | PPTX, PDF, Не трогать | Не трогать |
| PDF, изображения, архивы, неизвестные | Не трогать | Не трогать |

JSON обрабатывается встроенным adapter. Office и OpenDocument преобразует
bundled LibreOffice в headless-режиме с изолированными временными профилями.
OOXML и PDF проходят структурную проверку. Pixel-perfect совместимость не
гарантируется. XLSX → CSV по листам не входит в ZL-041.

## Режимы результата

- Папка: default `<source>\_converted`; разрешена другая отдельная подпапка или
  внешний путь.
- ZIP-архив: default
  `<source>\ZletBatchConverter-v0.0.0-results.zip`; в архив входят только
  успешно созданные выбранные результаты. При частичном успехе ZIP создаётся,
  при нуле успешных файлов пустой ZIP не создаётся.

Выбранная output-папка или точный output ZIP исключаются из следующих scan.
Traversal, небезопасные ZIP entries, reparse escapes и перезапись запрещены.

## Portable package

```text
ZletBatchConverter-v0.0.0-win-x64/
  ZletBatchConverter.exe
  runtime/
    libreoffice/
  licenses/
  THIRD_PARTY_NOTICES.md
  README_PORTABLE.txt
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 `
  -LibreOfficePath "C:\path\to\LibreOffice"
```

Пакет self-contained и не требует отдельно установленного .NET Runtime,
Microsoft Office или LibreOffice. Binaries и generated artifacts не коммитятся.
Первый public release не публикуется до ручного review.

## Сборка и тесты

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

```powershell
$env:ZLET_LIBREOFFICE_PATH = "C:\path\to\LibreOffice"
dotnet test FolderConverter.sln -c Release --filter Category=LibreOfficeIntegration
```

Проверенный runtime: официальный LibreOffice 26.2.4 Windows x86-64 (версия
26.2.4.2). Проверяются 15 mappings: legacy Office → OOXML/PDF, modern Office →
PDF и OpenDocument → OOXML/PDF.

Файлы остаются локально: cloud conversion, telemetry, analytics и runtime
download отсутствуют. Фактические локальные пути нельзя коммитить или логировать.
