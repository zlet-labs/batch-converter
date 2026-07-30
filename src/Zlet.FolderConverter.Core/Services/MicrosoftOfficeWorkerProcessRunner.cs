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

    public async Task<OfficeWorkerExecutionResult> RunAsync(
        OfficeWorkerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAvailable)
        {
            return new(false, "worker_missing");
        }

        IOfficeWorkerProcess? process = null;
        WorkerMessageState? state = null;
        Task? outputTask = null;
        Task<bool>? errorTask = null;
        Task? waitTask = null;
        var shutdownPerformed = false;
        try
        {
            process = _launcher.Start(_options.WorkerExecutablePath);
            state = new WorkerMessageState();
            outputTask = ReadMessagesAsync(process.StandardOutput, state);
            errorTask = ReadHasContentAsync(process.StandardError);
            waitTask = process.WaitForExitAsync(CancellationToken.None);

            var requestJson = JsonSerializer.Serialize(request, JsonOptions);
            await process.StandardInput.WriteLineAsync(
                requestJson.AsMemory(),
                cancellationToken);
            process.StandardInput.Close();

            var delayTask = Task.Delay(_options.Timeout, cancellationToken);
            var completed = await Task.WhenAny(waitTask, delayTask);
            if (completed != waitTask)
            {
                await ShutdownWorkerAsync(
                    process,
                    waitTask,
                    outputTask,
                    errorTask,
                    state,
                    request.Application);
                shutdownPerformed = true;
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return new(
                    false,
                    "worker_timeout",
                    TimedOut: true,
                    HasStandardOutput: state.HasOutput,
                    HasStandardError: errorTask.IsCompletedSuccessfully && errorTask.Result);
            }

            await waitTask;
            await outputTask;
            var hasError = await errorTask;
            var result = state.GetResult();
            if (result is null)
            {
                return new(
                    false,
                    "worker_result_missing",
                    process.ExitCode,
                    HasStandardOutput: state.HasOutput,
                    HasStandardError: hasError);
            }

            return new(
                result.Success,
                result.ErrorCode,
                process.ExitCode,
                HasStandardOutput: state.HasOutput,
                HasStandardError: hasError,
                HResult: result.HResult);
        }
        catch (OperationCanceledException)
        {
            if (!shutdownPerformed && process is not null && state is not null)
            {
                await ShutdownWorkerAsync(
                    process,
                    waitTask,
                    outputTask,
                    errorTask,
                    state,
                    request.Application);
            }

            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or InvalidOperationException
                                           or System.ComponentModel.Win32Exception
                                           or JsonException)
        {
            if (!shutdownPerformed && process is not null && state is not null)
            {
                await ShutdownWorkerAsync(
                    process,
                    waitTask,
                    outputTask,
                    errorTask,
                    state,
                    request.Application);
            }

            return new(false, "worker_start_failure");
        }
        finally
        {
            process?.Dispose();
        }
    }

    private async Task ShutdownWorkerAsync(
        IOfficeWorkerProcess process,
        Task? waitTask,
        Task? outputTask,
        Task<bool>? errorTask,
        WorkerMessageState state,
        OfficeApplicationKind application)
    {
        process.Kill();

        var pending = new[] { waitTask, outputTask, errorTask }
            .Where(task => task is not null)
            .Select(task => IgnoreFailureAsync(task!))
            .ToArray();
        if (pending.Length > 0)
        {
            var drainTask = Task.WhenAll(pending);
            await Task.WhenAny(drainTask, Task.Delay(_options.ShutdownTimeout));
        }

        var ownership = state.GetOwnership();
        if (ownership is not null)
        {
            _officeProcessTerminator.TryTerminate(application, ownership);
        }
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

    private static async Task ReadMessagesAsync(
        TextReader reader,
        WorkerMessageState state)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            state.MarkOutput();
            OfficeWorkerMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<OfficeWorkerMessage>(line, JsonOptions);
            }
            catch (JsonException)
            {
                state.MarkInvalidOutput();
                continue;
            }

            if (message is not null)
            {
                state.Accept(message);
            }
        }
    }

    private static async Task<bool> ReadHasContentAsync(TextReader reader)
    {
        var content = await reader.ReadToEndAsync();
        return !string.IsNullOrWhiteSpace(content);
    }

    private sealed class WorkerMessageState
    {
        private readonly object _gate = new();
        private OfficeProcessOwnership? _ownership;
        private OfficeWorkerMessage? _result;
        private bool _hasOutput;

        public bool HasOutput
        {
            get
            {
                lock (_gate)
                {
                    return _hasOutput;
                }
            }
        }

        public void MarkOutput()
        {
            lock (_gate)
            {
                _hasOutput = true;
            }
        }

        public void MarkInvalidOutput()
        {
            lock (_gate)
            {
                _result ??= new OfficeWorkerMessage(
                    OfficeWorkerMessageType.Result,
                    false,
                    "worker_protocol_invalid");
            }
        }

        public void Accept(OfficeWorkerMessage message)
        {
            lock (_gate)
            {
                if (message.MessageType == OfficeWorkerMessageType.Started
                    && message.OfficeProcessOwned
                    && message.OfficeProcessId is > 0
                    && message.OfficeProcessStartTimeUtcTicks is > 0)
                {
                    _ownership = new OfficeProcessOwnership(
                        message.OfficeProcessId.Value,
                        message.OfficeProcessStartTimeUtcTicks.Value);
                }
                else if (message.MessageType == OfficeWorkerMessageType.Result)
                {
                    _result = message;
                }
            }
        }

        public OfficeProcessOwnership? GetOwnership()
        {
            lock (_gate)
            {
                return _ownership;
            }
        }

        public OfficeWorkerMessage? GetResult()
        {
            lock (_gate)
            {
                return _result;
            }
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
