using System.Diagnostics;
using System.Runtime.InteropServices;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal sealed class ComOfficeAutomation : IOfficeAutomation, IDisposable
{
    private readonly IOfficeAutomationSessionFactory _sessionFactory;
    private readonly IOfficeProcessIdentityProvider _processIdentity;
    private IOfficeAutomationSession? _session;
    private OfficeApplicationKind? _sessionApplication;

    public ComOfficeAutomation()
        : this(
            new ComOfficeAutomationSessionFactory(),
            new SystemOfficeProcessIdentityProvider())
    {
    }

    internal ComOfficeAutomation(
        IOfficeAutomationSessionFactory sessionFactory,
        IOfficeProcessIdentityProvider processIdentity)
    {
        _sessionFactory = sessionFactory;
        _processIdentity = processIdentity;
    }

    public OfficeWorkerMessage ConvertWord(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report) =>
        Convert(OfficeApplicationKind.Word, request, report);

    public OfficeWorkerMessage ConvertExcel(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report) =>
        Convert(OfficeApplicationKind.Excel, request, report);

    public OfficeWorkerMessage ConvertPowerPoint(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report) =>
        Convert(OfficeApplicationKind.PowerPoint, request, report);

    private OfficeWorkerMessage Convert(
        OfficeApplicationKind application,
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        try
        {
            if (_session is null)
            {
                var baseline = _processIdentity.Capture(application);
                if (application == OfficeApplicationKind.PowerPoint && baseline.Count > 0)
                {
                    return Failure("powerpoint_already_running");
                }

                _session = _sessionFactory.Create(application);
                _sessionApplication = application;
                var started = _processIdentity.CreateStartedMessage(
                    application,
                    _session.WindowHandle,
                    baseline);
                report(started);
                _session.Configure();
            }
            else if (_sessionApplication != application)
            {
                return Failure("office_session_application_mismatch", sessionInvalid: true);
            }

            _session.OpenAndSave(request);
            return Success();
        }
        catch (COMException exception)
        {
            InvalidateSession();
            return Failure(
                "office_com_failure",
                exception.HResult,
                sessionInvalid: true);
        }
    }

    public void Dispose() => InvalidateSession();

    private void InvalidateSession()
    {
        if (_session is not null)
        {
            TryInvoke(_session.Cleanup);
        }

        _session = null;
        _sessionApplication = null;
    }

    private static OfficeWorkerMessage Success() =>
        new(OfficeWorkerMessageType.Result, true);

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

    private static void TryInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
        }
    }
}

internal interface IOfficeAutomationSessionFactory
{
    IOfficeAutomationSession Create(OfficeApplicationKind application);
}

internal interface IOfficeAutomationSession
{
    long WindowHandle { get; }
    void Configure();
    void OpenAndSave(OfficeWorkerRequest request);
    void Cleanup();
}

internal sealed class ComOfficeAutomationSessionFactory
    : IOfficeAutomationSessionFactory
{
    public IOfficeAutomationSession Create(OfficeApplicationKind application)
    {
        var progId = application switch
        {
            OfficeApplicationKind.Word => "Word.Application",
            OfficeApplicationKind.Excel => "Excel.Application",
            OfficeApplicationKind.PowerPoint => "PowerPoint.Application",
            _ => throw new COMException("The Office application is not supported.")
        };
        var type = Type.GetTypeFromProgID(progId, throwOnError: false)
            ?? throw new COMException("The Office application is not registered.");
        var instance = Activator.CreateInstance(type)
            ?? throw new COMException("The Office application could not be created.");
        return new ComOfficeAutomationSession(application, instance);
    }
}

internal sealed class ComOfficeAutomationSession(
    OfficeApplicationKind applicationKind,
    object application)
    : IOfficeAutomationSession
{
    private const int AutomationSecurityForceDisable = 3;
    private object? _collection;
    private object? _document;
    private bool _cleaned;

    public long WindowHandle
    {
        get
        {
            dynamic value = application;
            return applicationKind switch
            {
                OfficeApplicationKind.Word => 0,
                OfficeApplicationKind.Excel => Convert.ToInt64(value.Hwnd),
                OfficeApplicationKind.PowerPoint => 0,
                _ => 0
            };
        }
    }

    public void Configure()
    {
        dynamic value = application;
        switch (applicationKind)
        {
            case OfficeApplicationKind.Word:
                value.Visible = false;
                value.DisplayAlerts = 0;
                value.AutomationSecurity = AutomationSecurityForceDisable;
                break;
            case OfficeApplicationKind.Excel:
                value.Visible = false;
                value.DisplayAlerts = false;
                value.AutomationSecurity = AutomationSecurityForceDisable;
                value.AskToUpdateLinks = false;
                value.EnableEvents = false;
                break;
            case OfficeApplicationKind.PowerPoint:
                value.DisplayAlerts = 1;
                value.AutomationSecurity = AutomationSecurityForceDisable;
                break;
            default:
                throw new COMException("The Office application is not supported.");
        }
    }

    public void OpenAndSave(OfficeWorkerRequest request)
    {
        try
        {
            switch (applicationKind)
            {
                case OfficeApplicationKind.Word:
                    ConvertWord(request);
                    break;
                case OfficeApplicationKind.Excel:
                    ConvertExcel(request);
                    break;
                case OfficeApplicationKind.PowerPoint:
                    ConvertPowerPoint(request);
                    break;
                default:
                    throw new COMException("The Office application is not supported.");
            }
        }
        finally
        {
            CloseAndReleaseDocument();
        }
    }

    public void Cleanup()
    {
        if (_cleaned)
        {
            return;
        }

        _cleaned = true;
        CloseAndReleaseDocument();
        TryQuitApplication();
        ReleaseComObject(application);
    }

    private void ConvertWord(OfficeWorkerRequest request)
    {
        dynamic word = application;
        _collection = word.Documents;
        dynamic documents = _collection;
        _document = documents.Open(
            FileName: request.SourcePath,
            ConfirmConversions: false,
            ReadOnly: true,
            AddToRecentFiles: false,
            Revert: false,
            Visible: false,
            OpenAndRepair: false,
            NoEncodingDialog: true);
        dynamic document = _document;
        document.SaveAs2(
            FileName: request.OutputPath,
            FileFormat: 16,
            AddToRecentFiles: false);
    }

    private void ConvertExcel(OfficeWorkerRequest request)
    {
        dynamic excel = application;
        _collection = excel.Workbooks;
        dynamic workbooks = _collection;
        _document = workbooks.Open(
            Filename: request.SourcePath,
            UpdateLinks: 0,
            ReadOnly: true,
            IgnoreReadOnlyRecommended: true,
            Notify: false,
            AddToMru: false,
            Local: true,
            CorruptLoad: 0);
        dynamic workbook = _document;
        workbook.SaveAs(
            Filename: request.OutputPath,
            FileFormat: 51,
            AccessMode: 1,
            ConflictResolution: 2,
            AddToMru: false,
            Local: true);
    }

    private void ConvertPowerPoint(OfficeWorkerRequest request)
    {
        dynamic powerPoint = application;
        _collection = powerPoint.Presentations;
        dynamic presentations = _collection;
        _document = presentations.Open(
            FileName: request.SourcePath,
            ReadOnly: -1,
            Untitled: 0,
            WithWindow: 0);
        dynamic presentation = _document;
        presentation.SaveAs(
            FileName: request.OutputPath,
            FileFormat: 24,
            EmbedTrueTypeFonts: 0);
    }

    private void TryCloseDocument()
    {
        TryInvoke(() =>
        {
            if (_document is null)
            {
                return;
            }

            dynamic document = _document;
            if (applicationKind == OfficeApplicationKind.Word)
            {
                document.Close(SaveChanges: 0);
            }
            else if (applicationKind == OfficeApplicationKind.Excel)
            {
                document.Close(SaveChanges: false);
            }
            else
            {
                document.Close();
            }
        });
    }

    private void CloseAndReleaseDocument()
    {
        TryCloseDocument();
        ReleaseComObject(_document);
        ReleaseComObject(_collection);
        _document = null;
        _collection = null;
    }

    private void TryQuitApplication()
    {
        TryInvoke(() =>
        {
            dynamic value = application;
            if (applicationKind == OfficeApplicationKind.Word)
            {
                value.Quit(SaveChanges: 0);
            }
            else
            {
                value.Quit();
            }
        });
    }

    private static void TryInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is COMException
                                           or InvalidComObjectException
                                           or InvalidOperationException
                                           or ArgumentException)
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
}

internal interface IOfficeProcessIdentityProvider
{
    IReadOnlySet<int> Capture(OfficeApplicationKind application);

    OfficeWorkerMessage CreateStartedMessage(
        OfficeApplicationKind application,
        long windowHandle,
        IReadOnlySet<int> baseline);
}

internal sealed class SystemOfficeProcessIdentityProvider
    : IOfficeProcessIdentityProvider
{
    public IReadOnlySet<int> Capture(OfficeApplicationKind application) =>
        Process.GetProcessesByName(ToProcessName(application))
            .Select(process =>
            {
                using (process)
                {
                    return process.Id;
                }
            })
            .ToHashSet();

    public OfficeWorkerMessage CreateStartedMessage(
        OfficeApplicationKind application,
        long windowHandle,
        IReadOnlySet<int> baseline)
    {
        var processId = TryGetProcessId(windowHandle);
        if (processId == 0)
        {
            var candidates = Process.GetProcessesByName(ToProcessName(application))
                .Select(process =>
                {
                    using (process)
                    {
                        return process.Id;
                    }
                })
                .Where(id => !baseline.Contains(id))
                .Take(2)
                .ToArray();
            if (candidates.Length != 1)
            {
                return new OfficeWorkerMessage(OfficeWorkerMessageType.Started);
            }

            processId = (uint)candidates[0];
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

    private static uint TryGetProcessId(long windowHandle)
    {
        if (windowHandle == 0
            || GetWindowThreadProcessId((nint)windowHandle, out var processId) == 0)
        {
            return 0;
        }

        return processId;
    }

    private static string ToProcessName(OfficeApplicationKind application) =>
        application switch
        {
            OfficeApplicationKind.Word => "WINWORD",
            OfficeApplicationKind.Excel => "EXCEL",
            OfficeApplicationKind.PowerPoint => "POWERPNT",
            _ => string.Empty
        };

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        nint windowHandle,
        out uint processId);
}
