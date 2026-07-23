# Microsoft Office COM conversion notes

Zlet Batch Converter v0.0.0 uses late-bound Microsoft Office COM automation.
No `Microsoft.Office.Interop.*` package is referenced.

## Process boundary

`Zlet.FolderConverter.OfficeWorker` is a separate .NET executable in the same
solution. It reads one JSON request from redirected stdin, runs on an STA main
thread, emits JSON lifecycle/result messages to stdout, and exits after one
operation. The WPF process applies a timeout and remains responsive.

The worker obtains the application HWND and resolves its PID through
`GetWindowThreadProcessId`. Before activation it records existing PIDs for the
specific Office application. A PID is eligible for timeout termination only
when it was absent from that baseline and its process name and start timestamp
still match. The implementation never kills all `WINWORD`, `EXCEL`, or
`POWERPNT` processes.

## Word

- `Visible = false`
- `DisplayAlerts = 0`
- `AutomationSecurity = 3` (`msoAutomationSecurityForceDisable`)
- `Documents.Open`: `ReadOnly`, no Recent, hidden, no conversion/encoding dialog
- `Document.SaveAs2`: `FileFormat = 16` (`wdFormatDocumentDefault`, DOCX)

References:

- https://learn.microsoft.com/en-us/office/vba/api/word.documents.open
- https://learn.microsoft.com/en-us/office/vba/api/word.saveas2
- https://learn.microsoft.com/en-us/office/vba/api/word.wdsaveformat

## Excel

- `Visible = false`
- `DisplayAlerts = false`
- `AutomationSecurity = 3`
- `AskToUpdateLinks = false`
- `Workbooks.Open`: `UpdateLinks = 0`, read-only, no MRU, normal load
- `Workbook.SaveAs`: `FileFormat = 51` (`xlOpenXMLWorkbook`, XLSX)

References:

- https://learn.microsoft.com/en-us/office/vba/api/excel.workbooks.open
- https://learn.microsoft.com/en-us/office/vba/api/excel.workbook.saveas
- https://learn.microsoft.com/en-us/office/vba/api/excel.xlfileformat

## PowerPoint

- `Visible = 0` (`msoFalse`)
- `DisplayAlerts = 1` (`ppAlertsNone`)
- `AutomationSecurity = 3`
- `Presentations.Open`: read-only and without a window
- `Presentation.SaveAs`: `FileFormat = 24`
  (`ppSaveAsOpenXMLPresentation`, PPTX)

References:

- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.presentations.open
- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.presentation.saveas
- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.ppsaveasfiletype
- https://learn.microsoft.com/en-us/office/vba/api/powerpoint.ppalertlevel
- https://learn.microsoft.com/en-us/office/vba/api/office.msoautomationsecurity

## Cleanup

Every path closes the document/workbook/presentation without saving the source,
calls `Quit`, and releases COM objects in reverse order with
`Marshal.FinalReleaseComObject`. `GC.Collect()` is not used for COM lifetime.
