using System.Diagnostics;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed record MicrosoftOfficeWorkerOptions
{
    public string WorkerExecutablePath { get; init; } = Path.Combine(
        AppContext.BaseDirectory,
        "Zlet.FolderConverter.OfficeWorker.exe");

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed class MicrosoftOfficeWorkerProcessRunner : IMicrosoftOfficeWorkerRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MicrosoftOfficeWorkerOptions _options;
    private readonly IOfficeWorkerProcessLauncher _launcher;
    private readonly IOwnedOfficeProcessTerminator _officeProcessTerminator;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WorkerSession? _session;
    private bool _batchActive;

    public MicrosoftOfficeWorkerProcessRunner(MicrosoftOfficeWorkerOptions? options = null)
        : this(
            options ?? new MicrosoftOfficeWorkerOptions(),
            new OfficeWorkerProcessLauncher(),
            new OwnedOfficeProcessTerminator())
    {
    }

    internal MicrosoftOfficeWorkerProcessRunner(
        MicrosoftOfficeWorkerOptions options,
        IOfficeWorkerProcessLauncher launcher,
        IOwnedOfficeProcessTerminator officeProcessTerminator)
    {
        _options = options;
        _launcher = launcher;
        _officeProcessTerminator = officeProcessTerminator;
    }

    public bool IsAvailable => File.Exists(_options.WorkerExecutablePath);

    public async Task BeginBatchAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_batchActive)
            {
                throw new InvalidOperationException("An Office worker batch is already active.");
            }

            _batchActive = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EndBatchAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _batchActive = false;
            await ShutdownSessionAsync(force: false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OfficeWorkerExecutionResult> RunAsync(
        OfficeWorkerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAvailable)
        {
            return new(false, "worker_missing");
        }

        await _gate.WaitAsync(cancellationToken);
        var closeAfterRequest = !_batchActive;
        try
        {
            if (_session is not null && _session.Application != request.Application)
            {
                await ShutdownSessionAsync(force: false);
            }

            if (_session is null)
            {
                try
                {
                    var process = _launcher.Start(_options.WorkerExecutablePath);
                    _session = new WorkerSession(request.Application, process);
                }
                catch (Exception exception) when (exception is IOException
                                                   or InvalidOperationException
                                                   or System.ComponentModel.Win32Exception)
                {
                    return new(false, "worker_start_failure", SessionInvalid: true);
                }
            }

            return await ExecuteAsync(_session, request, cancellationToken);
        }
        finally
        {
            if (closeAfterRequest)
            {
                await ShutdownSessionAsync(force: false);
            }

            _gate.Release();
        }
    }

    private async Task<OfficeWorkerExecutionResult> ExecuteAsync(
        WorkerSession session,
        OfficeWorkerRequest request,
        CancellationToken cancellationToken)
    {
        Task<OfficeWorkerMessage?> responseTask;
        try
        {
            var requestJson = JsonSerializer.Serialize(request, JsonOptions);
            await session.Process.StandardInput.WriteLineAsync(
                requestJson.AsMemory(),
                cancellationToken);
            await session.Process.StandardInput.FlushAsync(cancellationToken);
            responseTask = ReadResponseAsync(session);
        }
        catch (OperationCanceledException)
        {
            await ShutdownSessionAsync(force: true);
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or InvalidOperationException
                                           or ObjectDisposedException)
        {
            await ShutdownSessionAsync(force: true);
            return new(false, "worker_protocol_failure", SessionInvalid: true);
        }

        var delayTask = Task.Delay(_options.Timeout, cancellationToken);
        var completed = await Task.WhenAny(responseTask, delayTask);
        if (completed != responseTask)
        {
            await ShutdownSessionAsync(force: true, responseTask);
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return new(
                false,
                "worker_timeout",
                TimedOut: true,
                HasStandardOutput: session.HasOutput,
                HasStandardError: session.HasStandardError,
                SessionInvalid: true);
        }

        OfficeWorkerMessage? result;
        try
        {
            result = await responseTask;
        }
        catch (Exception exception) when (exception is IOException
                                           or InvalidOperationException
                                           or ObjectDisposedException)
        {
            await ShutdownSessionAsync(force: true);
            return new(false, "worker_protocol_failure", SessionInvalid: true);
        }

        if (result is null)
        {
            var exitCode = session.WaitTask.IsCompletedSuccessfully
                ? (int?)session.Process.ExitCode
                : null;
            var hasOutput = session.HasOutput;
            var hasStandardError = session.HasStandardError;
            await ShutdownSessionAsync(force: true);
            return new(
                false,
                "worker_result_missing",
                exitCode,
                HasStandardOutput: hasOutput,
                HasStandardError: hasStandardError,
                SessionInvalid: true);
        }

        var executionResult = new OfficeWorkerExecutionResult(
            result.Success,
            result.ErrorCode,
            HasStandardOutput: session.HasOutput,
            HasStandardError: session.HasStandardError,
            HResult: result.HResult,
            SessionInvalid: result.SessionInvalid);
        if (result.SessionInvalid)
        {
            await ShutdownSessionAsync(force: true);
        }

        return executionResult;
    }

    private static async Task<OfficeWorkerMessage?> ReadResponseAsync(WorkerSession session)
    {
        while (await session.Process.StandardOutput.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            session.HasOutput = true;
            OfficeWorkerMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<OfficeWorkerMessage>(line, JsonOptions);
            }
            catch (JsonException)
            {
                return new OfficeWorkerMessage(
                    OfficeWorkerMessageType.Result,
                    false,
                    "worker_protocol_invalid",
                    SessionInvalid: true);
            }

            if (message?.MessageType == OfficeWorkerMessageType.Started
                && message.OfficeProcessOwned
                && message.OfficeProcessId is > 0
                && message.OfficeProcessStartTimeUtcTicks is > 0)
            {
                session.Ownership = new OfficeProcessOwnership(
                    message.OfficeProcessId.Value,
                    message.OfficeProcessStartTimeUtcTicks.Value);
            }
            else if (message?.MessageType == OfficeWorkerMessageType.Result)
            {
                return message;
            }
        }

        return null;
    }

    private async Task ShutdownSessionAsync(
        bool force,
        Task<OfficeWorkerMessage?>? responseTask = null)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        if (!force)
        {
            try
            {
                session.Process.StandardInput.Close();
            }
            catch (Exception exception) when (exception is IOException
                                               or InvalidOperationException
                                               or ObjectDisposedException)
            {
                force = true;
            }
        }

        if (!force)
        {
            var completed = await Task.WhenAny(
                session.WaitTask,
                Task.Delay(_options.ShutdownTimeout));
            force = completed != session.WaitTask;
        }

        if (force)
        {
            session.Process.Kill();
        }

        if (responseTask is not null)
        {
            await Task.WhenAny(
                IgnoreFailureAsync(responseTask),
                Task.Delay(_options.ShutdownTimeout));
        }

        await Task.WhenAny(
            Task.WhenAll(
                IgnoreFailureAsync(session.WaitTask),
                IgnoreFailureAsync(session.ErrorTask)),
            Task.Delay(_options.ShutdownTimeout));

        if (force && session.Ownership is not null)
        {
            _officeProcessTerminator.TryTerminate(session.Application, session.Ownership);
        }

        session.Dispose();
    }

    private static async Task IgnoreFailureAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
        }
    }

    private sealed class WorkerSession : IDisposable
    {
        public WorkerSession(
            OfficeApplicationKind application,
            IOfficeWorkerProcess process)
        {
            Application = application;
            Process = process;
            WaitTask = process.WaitForExitAsync(CancellationToken.None);
            ErrorTask = ReadStandardErrorAsync(process.StandardError, this);
        }

        public OfficeApplicationKind Application { get; }
        public IOfficeWorkerProcess Process { get; }
        public Task WaitTask { get; }
        public Task ErrorTask { get; }
        public OfficeProcessOwnership? Ownership { get; set; }
        public bool HasOutput { get; set; }
        public bool HasStandardError { get; private set; }

        public void Dispose() => Process.Dispose();

        private static async Task ReadStandardErrorAsync(
            TextReader reader,
            WorkerSession session)
        {
            var content = await reader.ReadToEndAsync();
            session.HasStandardError = !string.IsNullOrWhiteSpace(content);
        }
    }
}

internal sealed record OfficeProcessOwnership(
    int ProcessId,
    long StartTimeUtcTicks);

internal interface IOfficeWorkerProcessLauncher
{
    IOfficeWorkerProcess Start(string executablePath);
}

internal interface IOfficeWorkerProcess : IDisposable
{
    TextWriter StandardInput { get; }
    TextReader StandardOutput { get; }
    TextReader StandardError { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken cancellationToken);
    void Kill();
}

internal sealed class OfficeWorkerProcessLauncher : IOfficeWorkerProcessLauncher
{
    public IOfficeWorkerProcess Start(string executablePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Office worker process did not start.");
        return new OfficeWorkerProcess(process);
    }
}

internal sealed class OfficeWorkerProcess(Process process) : IOfficeWorkerProcess
{
    public TextWriter StandardInput => process.StandardInput;
    public TextReader StandardOutput => process.StandardOutput;
    public TextReader StandardError => process.StandardError;
    public int ExitCode => process.ExitCode;

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        process.WaitForExitAsync(cancellationToken);

    public void Kill()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: false);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or System.ComponentModel.Win32Exception
                                           or NotSupportedException)
        {
        }
    }

    public void Dispose() => process.Dispose();
}

internal interface IOwnedOfficeProcessTerminator
{
    bool TryTerminate(
        OfficeApplicationKind application,
        OfficeProcessOwnership ownership);
}

internal interface IOfficeProcessLookup
{
    IOfficeProcessHandle? TryGet(int processId);
}

internal interface IOfficeProcessHandle : IDisposable
{
    string ProcessName { get; }
    long StartTimeUtcTicks { get; }
    void Kill();
}

internal sealed class SystemOfficeProcessLookup : IOfficeProcessLookup
{
    public IOfficeProcessHandle? TryGet(int processId)
    {
        try
        {
            return new SystemOfficeProcessHandle(Process.GetProcessById(processId));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

internal sealed class SystemOfficeProcessHandle(Process process)
    : IOfficeProcessHandle
{
    public string ProcessName => process.ProcessName;
    public long StartTimeUtcTicks => process.StartTime.ToUniversalTime().Ticks;
    public void Kill() => process.Kill(entireProcessTree: false);
    public void Dispose() => process.Dispose();
}

internal sealed class OwnedOfficeProcessTerminator
    : IOwnedOfficeProcessTerminator
{
    private readonly IOfficeProcessLookup _processLookup;

    public OwnedOfficeProcessTerminator()
        : this(new SystemOfficeProcessLookup())
    {
    }

    internal OwnedOfficeProcessTerminator(IOfficeProcessLookup processLookup)
    {
        _processLookup = processLookup;
    }

    public bool TryTerminate(
        OfficeApplicationKind application,
        OfficeProcessOwnership ownership)
    {
        try
        {
            using var process = _processLookup.TryGet(ownership.ProcessId);
            if (process is null)
            {
                return false;
            }

            var expectedName = application switch
            {
                OfficeApplicationKind.Word => "WINWORD",
                OfficeApplicationKind.Excel => "EXCEL",
                OfficeApplicationKind.PowerPoint => "POWERPNT",
                _ => string.Empty
            };
            if (!process.ProcessName.Equals(expectedName, StringComparison.OrdinalIgnoreCase)
                || process.StartTimeUtcTicks != ownership.StartTimeUtcTicks)
            {
                return false;
            }

            process.Kill();
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or System.ComponentModel.Win32Exception
                                           or NotSupportedException)
        {
            return false;
        }
    }
}
