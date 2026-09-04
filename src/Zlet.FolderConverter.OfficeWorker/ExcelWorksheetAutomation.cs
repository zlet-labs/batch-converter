using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal sealed class ExcelWorksheetAutomation : IDisposable
{
    private bool _applicationOwned;
    private const int AutomationSecurityForceDisable = 3;
    private const int XlCsvUtf8 = 62;
    private const int XlUnicodeText = 42;
    private readonly IOfficeProcessIdentityProvider _processIdentity;
    private object? _application;

    public ExcelWorksheetAutomation()
        : this(new SystemOfficeProcessIdentityProvider())
    {
    }

    internal ExcelWorksheetAutomation(IOfficeProcessIdentityProvider processIdentity)
    {
        _processIdentity = processIdentity;
    }

    public OfficeWorkerMessage Execute(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        try
        {
            EnsureApplication(report);
            return request.Operation == OfficeWorkerOperation.InspectWorkbook
                ? InspectWorkbook(request)
                : ExportWorksheet(request);
        }
        catch (COMException exception)
        {
            ResetApplication();
            return Failure("office_com_failure", exception.HResult, sessionInvalid: true);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException
                                           or ArgumentException)
        {
            return Failure("excel_sheet_operation_failure", exception.HResult);
        }
    }

    public void Dispose() => ResetApplication();

    private void EnsureApplication(Action<OfficeWorkerMessage> report)
    {
        if (_application is not null)
        {
            return;
        }

        var baseline = _processIdentity.Capture(OfficeApplicationKind.Excel);
        var type = Type.GetTypeFromProgID("Excel.Application", throwOnError: false)
            ?? throw new COMException("Microsoft Excel is not registered.");
        _application = Activator.CreateInstance(type)
            ?? throw new COMException("Microsoft Excel could not be created.");

        dynamic excel = _application;
        var started = _processIdentity.CreateStartedMessage(
            OfficeApplicationKind.Excel,
            Convert.ToInt64(excel.Hwnd),
            baseline);
        report(started);
        _applicationOwned = started.OfficeProcessOwned && started.OfficeProcessId is > 0
            && started.OfficeProcessStartTimeUtcTicks is > 0;
        if (!_applicationOwned)
        {
            ReleaseComObject(_application);
            _application = null;
            throw new COMException("Excel session ownership could not be established.");
        }

        excel.Visible = false;
        excel.DisplayAlerts = false;
        excel.AutomationSecurity = AutomationSecurityForceDisable;
        excel.AskToUpdateLinks = false;
        excel.EnableEvents = false;
    }

    private OfficeWorkerMessage InspectWorkbook(OfficeWorkerRequest request)
    {
        object? workbooks = null;
        object? workbook = null;
        object? worksheets = null;
        try
        {
            dynamic excel = _application!;
            workbooks = excel.Workbooks;
            dynamic books = workbooks;
            workbook = books.Open(
                Filename: request.SourcePath,
                UpdateLinks: 0,
                ReadOnly: true,
                IgnoreReadOnlyRecommended: true,
                Notify: false,
                AddToMru: false,
                Local: true,
                CorruptLoad: 0);
            dynamic book = workbook;
            worksheets = book.Worksheets;
            dynamic sheets = worksheets;
            var count = Convert.ToInt32(sheets.Count);
            var result = new List<WorksheetInfo>(count);
            for (var index = 1; index <= count; index++)
            {
                object? worksheet = null;
                object? usedRange = null;
                try
                {
                    worksheet = sheets[index];
                    dynamic sheet = worksheet;
                    var visibility = ConvertVisibility(Convert.ToInt32(sheet.Visible));
                    usedRange = sheet.UsedRange;
                    dynamic range = usedRange;
                    object? value = range.Value2;
                    result.Add(new WorksheetInfo(
                        Convert.ToString(sheet.Name) ?? $"Sheet{index}",
                        index,
                        visibility,
                        IsEmptyValue(value)));
                }
                finally
                {
                    ReleaseComObject(usedRange);
                    ReleaseComObject(worksheet);
                }
            }

            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(result)));
            return new OfficeWorkerMessage(
                OfficeWorkerMessageType.Result,
                true,
                $"worksheets:{payload}",
                Worksheets: result);
        }
        finally
        {
            TryCloseWorkbook(workbook);
            ReleaseComObject(worksheets);
            ReleaseComObject(workbook);
            ReleaseComObject(workbooks);
        }
    }

    private OfficeWorkerMessage ExportWorksheet(OfficeWorkerRequest request)
    {
        object? workbooks = null;
        object? sourceWorkbook = null;
        object? worksheets = null;
        object? worksheet = null;
        object? exportWorkbook = null;
        object? sourceRange = null;
        object? exportSheets = null;
        object? exportSheet = null;
        object? exportRange = null;
        string? unicodeTempPath = null;
        try
        {
            dynamic excel = _application!;
            workbooks = excel.Workbooks;
            dynamic books = workbooks;
            sourceWorkbook = books.Open(
                Filename: request.SourcePath,
                UpdateLinks: 0,
                ReadOnly: true,
                IgnoreReadOnlyRecommended: true,
                Notify: false,
                AddToMru: false,
                Local: true,
                CorruptLoad: 0);
            dynamic source = sourceWorkbook;
            worksheets = source.Worksheets;
            dynamic sheets = worksheets;
            worksheet = sheets[request.WorksheetName];
            dynamic sheet = worksheet;
            sourceRange = sheet.UsedRange;
            dynamic values = sourceRange;
            object? computedValues = values.Value2;
            // Visibility changes are in memory only; the source is closed without saving.
            sheet.Visible = -1;
            sheet.Copy();
            exportWorkbook = excel.ActiveWorkbook;
            dynamic export = exportWorkbook;
            exportSheets = export.Worksheets;
            dynamic copiedSheets = exportSheets;
            exportSheet = copiedSheets[1];
            dynamic copiedSheet = exportSheet;
            exportRange = copiedSheet.UsedRange;
            dynamic copiedRange = exportRange;
            copiedRange.Value2 = computedValues;

            if (request.Target == ConversionTarget.Csv)
            {
                export.SaveAs(
                    Filename: request.OutputPath,
                    FileFormat: XlCsvUtf8,
                    AccessMode: 1,
                    ConflictResolution: 2,
                    AddToMru: false,
                    Local: false);
            }
            else if (request.Target == ConversionTarget.Tsv)
            {
                var directory = Path.GetDirectoryName(request.OutputPath)
                    ?? throw new InvalidDataException("Output directory is missing.");
                unicodeTempPath = Path.Combine(
                    directory,
                    $".{Path.GetFileName(request.OutputPath)}.{Guid.NewGuid():N}.unicode.txt");
                export.SaveAs(
                    Filename: unicodeTempPath,
                    FileFormat: XlUnicodeText,
                    AccessMode: 1,
                    ConflictResolution: 2,
                    AddToMru: false,
                    Local: true);
                TryCloseWorkbook(exportWorkbook);
                ReleaseComObject(exportWorkbook);
                exportWorkbook = null;

                TranscodeTabSeparated(unicodeTempPath, request.OutputPath);
            }
            else
            {
                return Failure("excel_sheet_target_unsupported");
            }

            return new OfficeWorkerMessage(OfficeWorkerMessageType.Result, true);
        }
        finally
        {
            ReleaseComObject(exportRange);
            ReleaseComObject(exportSheet);
            ReleaseComObject(exportSheets);
            ReleaseComObject(sourceRange);
            TryCloseWorkbook(exportWorkbook);
            TryCloseWorkbook(sourceWorkbook);
            ReleaseComObject(exportWorkbook);
            ReleaseComObject(worksheet);
            ReleaseComObject(worksheets);
            ReleaseComObject(sourceWorkbook);
            ReleaseComObject(workbooks);
            TryDeleteFile(unicodeTempPath);
        }
    }

    internal static void TranscodeTabSeparated(string source, string target)
    {
        using var input = new StreamReader(source, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        using var output = new StreamWriter(new FileStream(target, FileMode.CreateNew), new UTF8Encoding(false));
        var buffer = new char[8192];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, count);
    }

    private void ResetApplication()
    {
        if (_application is null)
        {
            return;
        }

        try
        {
            dynamic excel = _application;
            if (_applicationOwned) excel.Quit();
        }
        catch (Exception exception) when (exception is COMException
                                           or InvalidComObjectException
                                           or InvalidOperationException)
        {
        }
        finally
        {
            ReleaseComObject(_application);
            _application = null;
            _applicationOwned = false;
        }
    }

    private static WorksheetVisibility ConvertVisibility(int value) => value switch
    {
        -1 => WorksheetVisibility.Visible,
        2 => WorksheetVisibility.VeryHidden,
        _ => WorksheetVisibility.Hidden
    };

    private static bool IsEmptyValue(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrEmpty(text);
        }

        if (value is object[,] values)
        {
            foreach (var item in values)
            {
                if (item is null)
                {
                    continue;
                }
                if (item is string itemText && itemText.Length == 0)
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        return false;
    }

    private static void TryCloseWorkbook(object? workbook)
    {
        if (workbook is null)
        {
            return;
        }

        try
        {
            dynamic value = workbook;
            value.Close(SaveChanges: false);
        }
        catch (Exception exception) when (exception is COMException
                                           or InvalidComObjectException
                                           or InvalidOperationException)
        {
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null || !Marshal.IsComObject(value))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(value);
        }
        catch (Exception exception) when (exception is COMException
                                           or InvalidComObjectException
                                           or ArgumentException)
        {
        }
    }

    private static OfficeWorkerMessage Failure(
        string errorCode,
        int? hResult = null,
        bool sessionInvalid = false) =>
        new(
            OfficeWorkerMessageType.Result,
            false,
            errorCode,
            HResult: hResult,
            SessionInvalid: sessionInvalid);
}
