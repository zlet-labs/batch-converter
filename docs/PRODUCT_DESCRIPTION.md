# Zlet Converter — Product Description

[English](#english) · [Русский](#русский)

## English

### Short description

**Zlet Converter is a local Windows utility for batch-converting legacy Microsoft Office files and safely processing modern Office files without cloud uploads, including practical preparation of supported document collections for Gemini Notebook and other modern document workflows.**

### Repository / catalog description

Zlet Converter helps process many files in folders and subfolders while keeping the work local on the user's PC. It converts supported legacy Office formats such as DOC, XLS and PPT using the installed Microsoft Office applications, safely copies modern DOCX/XLSX/PPTX files, preserves relative folder structure, and avoids silently overwriting existing results.

The app is designed for simple self-serve use: choose a folder, review the planned actions, select what to process, choose the output location, run the batch, and inspect the result summary. No account, backend or cloud upload is required.

### Gemini Notebook use case

One practical use case is preparing older local document collections for **Gemini Notebook**. Google currently documents DOCX, PPTX, TXT and Markdown as supported Gemini Notebook source types, so Zlet Converter can help prepare DOC → DOCX, PPT → PPTX and JSON → TXT/Markdown, while already-modern DOCX/PPTX files can be copied safely without changing their contents.

Zlet Converter does not connect to Gemini Notebook and does not upload files to it. Conversion remains local; the user decides whether and when to upload the results. XLS → XLSX remains a general converter feature and is not presented here as a Gemini Notebook-specific upload path.

Gemini Notebook source support: https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=en

### Key points

- Windows x64 desktop utility.
- Local processing only; files are not uploaded.
- DOC → DOCX through installed Microsoft Word.
- XLS → XLSX through installed Microsoft Excel.
- PPT → PPTX through installed Microsoft PowerPoint.
- Safe unchanged copy for DOCX/XLSX/PPTX.
- JSON → TXT or Markdown conversion.
- Useful for preparing supported DOC/PPT/JSON collections for Gemini Notebook and similar document workflows.
- Folder and subfolder scanning with relative structure preservation.
- Preview and operation selection before processing.
- Conflict protection: existing result files are not silently overwritten.
- Per-file status, progress, source size and elapsed time.
- Safe batch cancellation that does not terminate unrelated user Office processes.
- PRE-ALPHA software; complex, corrupted, password-protected or unsupported documents may fail.

### One-line GitHub About text

`Local Windows file converter for DOC/XLS/PPT. Helps prepare DOC/PPT sources for Gemini Notebook; local processing, no cloud uploads.`

## Русский

### Короткое описание

**Zlet Converter — локальная Windows-утилита для пакетного преобразования старых форматов Microsoft Office и безопасной обработки современных Office-файлов без загрузки в облако, в том числе для подготовки поддерживаемых коллекций документов к Gemini Notebook и другим современным document workflows.**

### Описание продукта

Zlet Converter помогает обрабатывать сразу много файлов в папках и подпапках, оставляя всю работу на компьютере пользователя. Утилита преобразует поддерживаемые старые форматы Office, такие как DOC, XLS и PPT, через установленный Microsoft Office, безопасно копирует современные DOCX/XLSX/PPTX, сохраняет относительную структуру каталогов и не перезаписывает существующие результаты молча.

Основной сценарий простой: выбрать папку, посмотреть план операций, отметить нужные файлы, выбрать место сохранения, запустить пакетную обработку и проверить итоговую сводку. Аккаунт, backend и загрузка документов в облако не требуются.

### Сценарий Gemini Notebook

Один из практичных сценариев — подготовка старых локальных коллекций документов для **Gemini Notebook**. По текущей справке Google Gemini Notebook поддерживает DOCX, PPTX, TXT и Markdown как типы источников, поэтому Zlet Converter может подготовить DOC → DOCX, PPT → PPTX и JSON → TXT/Markdown, а уже современные DOCX/PPTX безопасно скопировать без изменения содержимого.

Zlet Converter не подключается к Gemini Notebook и не загружает туда файлы. Преобразование остаётся локальным; пользователь сам решает, загружать ли результат и когда. XLS → XLSX остаётся общей возможностью конвертера и здесь не заявляется как отдельный путь загрузки в Gemini Notebook.

Поддерживаемые источники Gemini Notebook: https://support.google.com/gemininotebook/answer/16215270?co=GENIE.Platform%3DDesktop&hl=ru

### Основные возможности

- Windows x64 desktop-утилита.
- Полностью локальная обработка; файлы никуда не загружаются.
- DOC → DOCX через установленный Microsoft Word.
- XLS → XLSX через установленный Microsoft Excel.
- PPT → PPTX через установленный Microsoft PowerPoint.
- Безопасное копирование DOCX/XLSX/PPTX без изменений.
- JSON → TXT или Markdown.
- Подходит для подготовки поддерживаемых DOC/PPT/JSON-коллекций к Gemini Notebook и похожим document workflows.
- Сканирование папок и подпапок с сохранением относительной структуры.
- Preview и выбор операций до запуска.
- Защита от конфликтов: существующие результаты не перезаписываются молча.
- Статус, прогресс, размер исходника и время выполнения по каждому файлу.
- Безопасная остановка batch без завершения посторонних пользовательских процессов Office.
- PRE-ALPHA: сложные, повреждённые, защищённые паролем или неподдерживаемые документы могут не преобразоваться.

### Короткое описание для GitHub About

`Локальный Windows-конвертер DOC/XLS/PPT. Помогает готовить DOC/PPT для Gemini Notebook; обработка локально, без загрузки в облако.`
