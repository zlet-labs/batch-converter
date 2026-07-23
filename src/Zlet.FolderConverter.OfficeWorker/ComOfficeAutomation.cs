using System.Diagnostics;
using System.Runtime.InteropServices;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal sealed class ComOfficeAutomation : IOfficeAutomation
{
    private const int AutomationSecurityForceDisable = 3;

    public OfficeWorkerMessage ConvertWord(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        object? application = null;
        object? documents = null;
        object? document = null;
        try
        {
            var baseline = OfficeProcessIdentity.Capture("WINWORD");
            application = CreateApplication("Word.Application");
            dynamic word = application;
            word.Visible = false;
            word.DisplayAlerts = 0;
            word.AutomationSecurity = AutomationSecurityForceDisable;
            report(OfficeProcessIdentity.CreateStartedMessage(
                OfficeApplicationKind.Word,
                Convert.ToInt64(word.Hwnd),
                baseline));

            documents = word.Documents;
            dynamic wordDocuments = documents;
            document = wordDocuments.Open(
                FileName: request.SourcePath,
                ConfirmConversions: false,
                ReadOnly: true,
                AddToRecentFiles: false,
                Revert: false,
                Visible: false,
                OpenAndRepair: false,
                NoEncodingDialog: true);
            dynamic wordDocument = document;
            wordDocument.SaveAs2(
                FileName: request.OutputPath,
                FileFormat: 16,
                AddToRecentFiles: false);
            return Success();
        }
        catch (COMException exception)
        {
            return Failure(exception);
        }
        finally
        {
            TryInvoke(() =>
            {
                if (document is not null)
                {
                    dynamic value = document;
                    value.Close(SaveChanges: 0);
                }
            });
            TryInvoke(() =>
            {
                if (application is not null)
                {
                    dynamic value = application;
                    value.Quit(SaveChanges: 0);
                }
            });
            ReleaseComObject(document);
            ReleaseComObject(documents);
            ReleaseComObject(application);
        }
    }

    public OfficeWorkerMessage ConvertExcel(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        object? application = null;
        object? workbooks = null;
        object? workbook = null;
        try
        {
            var baseline = OfficeProcessIdentity.Capture("EXCEL");
            application = CreateApplication("Excel.Application");
            dynamic excel = application;
            excel.Visible = false;
            excel.DisplayAlerts = false;
            excel.AutomationSecurity = AutomationSecurityForceDisable;
            excel.AskToUpdateLinks = false;
            excel.EnableEvents = false;
            report(OfficeProcessIdentity.CreateStartedMessage(
                OfficeApplicationKind.Excel,
                Convert.ToInt64(excel.Hwnd),
                baseline));

            workbooks = excel.Workbooks;
            dynamic excelWorkbooks = workbooks;
            workbook = excelWorkbooks.Open(
                Filename: request.SourcePath,
                UpdateLinks: 0,
                ReadOnly: true,
                IgnoreReadOnlyRecommended: true,
                Notify: false,
                AddToMru: false,
                Local: true,
                CorruptLoad: 0);
            dynamic excelWorkbook = workbook;
            excelWorkbook.SaveAs(
                Filename: request.OutputPath,
                FileFormat: 51,
                AccessMode: 1,
                ConflictResolution: 2,
                AddToMru: false,
                Local: true);
            return Success();
        }
        catch (COMException exception)
        {
            return Failure(exception);
        }
        finally
        {
            TryInvoke(() =>
            {
                if (workbook is not null)
                {
                    dynamic value = workbook;
                    value.Close(SaveChanges: false);
                }
            });
            TryInvoke(() =>
            {
                if (application is not null)
                {
                    dynamic value = application;
                    value.Quit();
                }
            });
            ReleaseComObject(workbook);
            ReleaseComObject(workbooks);
            ReleaseComObject(application);
        }
    }

    public OfficeWorkerMessage ConvertPowerPoint(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        object? application = null;
        object? presentations = null;
        object? presentation = null;
        try
        {
            var baseline = OfficeProcessIdentity.Capture("POWERPNT");
            application = CreateApplication("PowerPoint.Application");
            dynamic powerPoint = application;
            powerPoint.Visible = 0;
            powerPoint.DisplayAlerts = 1;
            powerPoint.AutomationSecurity = AutomationSecurityForceDisable;
            report(OfficeProcessIdentity.CreateStartedMessage(
                OfficeApplicationKind.PowerPoint,
                Convert.ToInt64(powerPoint.HWND),
                baseline));

            presentations = powerPoint.Presentations;
            dynamic powerPointPresentations = presentations;
            presentation = powerPointPresentations.Open(
                FileName: request.SourcePath,
                ReadOnly: -1,
                Untitled: 0,
                WithWindow: 0);
            dynamic powerPointPresentation = presentation;
            powerPointPresentation.SaveAs(
                FileName: request.OutputPath,
                FileFormat: 24,
                EmbedFonts: 0);
            return Success();
        }
        catch (COMException exception)
        {
            return Failure(exception);
        }
        finally
        {
            TryInvoke(() =>
            {
                if (presentation is not null)
                {
                    dynamic value = presentation;
                    value.Close();
                }
            });
            TryInvoke(() =>
            {
                if (application is not null)
                {
                    dynamic value = application;
                    value.Quit();
                }
            });
            ReleaseComObject(presentation);
            ReleaseComObject(presentations);
            ReleaseComObject(application);
        }
    }

    private static object CreateApplication(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: false)
            ?? throw new COMException("The Office application is not registered.");
        return Activator.CreateInstance(type)
            ?? throw new COMException("The Office application could not be created.");
    }

    private static OfficeWorkerMessage Success() =>
        new(OfficeWorkerMessageType.Result, true);

    private static OfficeWorkerMessage Failure(COMException exception) =>
        new(
            OfficeWorkerMessageType.Result,
            false,
            "office_com_failure",
            HResult: exception.HResult);

    private static void TryInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is COMException
                                           or InvalidComObjectException
                                           or InvalidOperationException)
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
        catch (InvalidComObjectException)
        {
        }
    }
}

internal static class OfficeProcessIdentity
{
    public static IReadOnlySet<int> Capture(string processName) =>
        Process.GetProcessesByName(processName)
            .Select(process =>
            {
                using (process)
                {
                    return process.Id;
                }
            })
            .ToHashSet();

    public static OfficeWorkerMessage CreateStartedMessage(
        OfficeApplicationKind application,
        long windowHandle,
        IReadOnlySet<int> baseline)
    {
        if (windowHandle == 0
            || GetWindowThreadProcessId((nint)windowHandle, out var processId) == 0
            || processId == 0)
        {
            return new OfficeWorkerMessage(OfficeWorkerMessageType.Started);
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new OfficeWorkerMessage(
                OfficeWorkerMessageType.Started,
                OfficeProcessId: (int)processId,
                OfficeProcessStartTimeUtcTicks: process.StartTime.ToUniversalTime().Ticks,
                OfficeProcessOwned: !baseline.Contains((int)processId));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or System.ComponentModel.Win32Exception
                                           or NotSupportedException)
        {
            return new OfficeWorkerMessage(OfficeWorkerMessageType.Started);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        nint windowHandle,
        out uint processId);
}
