# Zlet Folder Converter

Локальное Windows-приложение для подготовки JSON-файлов к загрузке в NotebookLM. Оно сканирует выбранную папку, показывает план операций и сохраняет отдельный результат для каждого исходника в `<выбранная папка>\_converted`. Исходные файлы не изменяются и не удаляются.

## Поддерживается

- JSON → TXT;
- JSON → Markdown;
- пакетная обработка;
- обработка подпапок с сохранением структуры;
- полностью локальная работа;
- portable Windows x64 без требования установленного .NET Runtime;
- защита существующих файлов и директорий от перезаписи;
- продолжение batch после ошибки отдельного файла.

## Не поддерживается

- DOC → DOCX;
- XLS → XLSX;
- PPT → PPTX;
- XLSX → CSV;
- PDF/OCR;
- объединение JSON в один документ;
- cloud conversion.

Офисные файлы отображаются в preview со статусом «Не поддерживается». Приложение не использует Office COM, LibreOffice, внешние CLI и сетевые сервисы.

## Сборка и проверка

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1
```

Portable ZIP создаётся в `artifacts\portable\win-x64`.

## Использование

1. Выберите папку.
2. Настройте подпапки и формат `TXT` или `Markdown`.
3. Нажмите «Проверить файлы» и изучите preview.
4. Нажмите «Преобразовать файлы».
5. Откройте путь `_converted`, показанный в итоговом отчёте.

Невалидный JSON получает статус «Ошибка», но остальные файлы продолжают обрабатываться. Существующие результаты не перезаписываются.
