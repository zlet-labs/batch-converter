# Microsoft Office COM conversion notes

Zlet Batch Converter v0.0.0 uses late-bound Microsoft Office COM automation.
No `Microsoft.Office.Interop.*` package is referenced.

## Process boundary

`Zlet.FolderConverter.OfficeWorker` is a separate .NET executable in the same
solution. It reads one JSON request from redirected stdin, runs on an STA main
thread, emits JSON lifecycle/result messages to stdout, and exits after one
operation. The WPF process applies a timeout and remains responsive.

Before activation the worker records existing PIDs for the specific Office
application. Immediately after COM activation it resolves the PID from the
application HWND when that property is available. Word builds that do not
publish `Application.Hwnd` use the single new `WINWORD` PID relative to the
baseline. The worker then emits `Started` before changing any Office setting.
On timeout/cancellation the WPF process terminates the worker, drains
stdout/stderr for a bounded interval, and then rechecks ownership. A PID is
eligible for termination only when it was absent from the baseline and its
process name and start timestamp still match. The implementation never kills
all `WINWORD`, `EXCEL`, or `POWERPNT` processes.

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

- If any `POWERPNT` process exists in the pre-activation baseline, the worker
  returns `powerpoint_already_running` without creating COM automation,
  changing settings, opening a presentation, or calling `Quit`.
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
