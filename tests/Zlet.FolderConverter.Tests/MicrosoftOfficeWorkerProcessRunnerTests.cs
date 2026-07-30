using System.Diagnostics;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class MicrosoftOfficeWorkerProcessRunnerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _workerPath = Path.Combine(
        Path.GetTempPath(),
        $"zlet-worker-placeholder-{Guid.NewGuid():N}.exe");

    public MicrosoftOfficeWorkerProcessRunnerTests() =>
        File.WriteAllText(_workerPath, "placeholder");

    [Fact]
    public async Task Timeout_terminates_worker_without_unowned_office_process()
    {
        var process = new ControlledWorkerProcess(new DeferredTextReader());
        var officeTerminator = new RecordingOfficeTerminator();
        var runner = CreateRunner(process, officeTerminator);

        var result = await runner.RunAsync(
            Request(OfficeApplicationKind.Word),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Equal("worker_timeout", result.ErrorCode);
        Assert.True(process.KillCalled);
        Assert.False(officeTerminator.WasCalled);
    }

    [Fact]
    public async Task Timeout_reads_late_stdout_and_terminates_owned_powerpoint()
    {
        var ownership = new OfficeProcessOwnership(
            4242,
            638900000000000000);
        var process = new ControlledWorkerProcess(
            new DeferredTextReader(StartedLine(ownership, owned: true)));
        var officeTerminator = new RecordingOfficeTerminator();
        var runner = CreateRunner(process, officeTerminator);

        var result = await runner.RunAsync(
            Request(OfficeApplicationKind.PowerPoint),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.True(process.KillCalled);
        Assert.Equal(OfficeApplicationKind.PowerPoint, officeTerminator.Application);
        Assert.Equal(ownership, officeTerminator.Ownership);
    }

    [Fact]
    public async Task Timeout_never_terminates_existing_user_powerpoint()
    {
        var process = new ControlledWorkerProcess(
            new DeferredTextReader(
                StartedLine(
                    new OfficeProcessOwnership(4242, 638900000000000000),
                    owned: false)));
        var officeTerminator = new RecordingOfficeTerminator();
        var runner = CreateRunner(process, officeTerminator);

        var result = await runner.RunAsync(
            Request(OfficeApplicationKind.PowerPoint),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.True(process.KillCalled);
        Assert.False(officeTerminator.WasCalled);
    }

    [Fact]
    public async Task Cancellation_terminates_worker_without_hanging()
    {
        var process = new ControlledWorkerProcess(new DeferredTextReader());
        var runner = CreateRunner(
            process,
            new RecordingOfficeTerminator(),
            timeout: TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(30));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(
                Request(OfficeApplicationKind.Excel),
                cancellation.Token));

        Assert.True(process.KillCalled);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Terminator_does_not_kill_unknown_pid()
    {
        var terminator = new OwnedOfficeProcessTerminator(
            new FakeProcessLookup(null));

        var terminated = terminator.TryTerminate(
            OfficeApplicationKind.PowerPoint,
            new OfficeProcessOwnership(9999, 638900000000000000));

        Assert.False(terminated);
    }

    [Fact]
    public void Terminator_does_not_kill_reused_pid_with_new_start_time()
    {
        var process = new FakeProcessHandle(
            "POWERPNT",
            638900000000000001);
        var terminator = new OwnedOfficeProcessTerminator(
            new FakeProcessLookup(process));

        var terminated = terminator.TryTerminate(
            OfficeApplicationKind.PowerPoint,
            new OfficeProcessOwnership(4242, 638900000000000000));

        Assert.False(terminated);
        Assert.False(process.KillCalled);
    }

    [Fact]
    public void Terminator_does_not_kill_pid_with_unexpected_process_name()
    {
        var process = new FakeProcessHandle(
            "OTHER",
            638900000000000000);
        var terminator = new OwnedOfficeProcessTerminator(
            new FakeProcessLookup(process));

        var terminated = terminator.TryTerminate(
            OfficeApplicationKind.PowerPoint,
            new OfficeProcessOwnership(4242, 638900000000000000));

        Assert.False(terminated);
        Assert.False(process.KillCalled);
    }

    [Fact]
    public void Terminator_kills_only_exact_process_identity()
    {
        var process = new FakeProcessHandle(
            "POWERPNT",
            638900000000000000);
        var terminator = new OwnedOfficeProcessTerminator(
            new FakeProcessLookup(process));

        var terminated = terminator.TryTerminate(
            OfficeApplicationKind.PowerPoint,
            new OfficeProcessOwnership(4242, 638900000000000000));

        Assert.True(terminated);
        Assert.True(process.KillCalled);
    }

    public void Dispose()
    {
        if (File.Exists(_workerPath))
        {
            File.Delete(_workerPath);
        }
    }

    private MicrosoftOfficeWorkerProcessRunner CreateRunner(
        IOfficeWorkerProcess process,
        IOwnedOfficeProcessTerminator terminator,
        TimeSpan? timeout = null) =>
        new(
            new MicrosoftOfficeWorkerOptions
            {
                WorkerExecutablePath = _workerPath,
                Timeout = timeout ?? TimeSpan.FromMilliseconds(30),
                ShutdownTimeout = TimeSpan.FromMilliseconds(250)
            },
            new FakeLauncher(process),
            terminator);

    private static OfficeWorkerRequest Request(OfficeApplicationKind application) =>
        new(
            application,
            application == OfficeApplicationKind.Word
                ? @"C:\source.doc"
                : application == OfficeApplicationKind.Excel
                    ? @"C:\source.xls"
                    : @"C:\source.ppt",
            application == OfficeApplicationKind.Word
                ? @"C:\result.docx"
                : application == OfficeApplicationKind.Excel
                    ? @"C:\result.xlsx"
                    : @"C:\result.pptx");

    private static string StartedLine(
        OfficeProcessOwnership ownership,
        bool owned) =>
        JsonSerializer.Serialize(
            new OfficeWorkerMessage(
                OfficeWorkerMessageType.Started,
                OfficeProcessId: ownership.ProcessId,
                OfficeProcessStartTimeUtcTicks: ownership.StartTimeUtcTicks,
                OfficeProcessOwned: owned),
            JsonOptions);

    private sealed class FakeLauncher(IOfficeWorkerProcess process)
        : IOfficeWorkerProcessLauncher
    {
        public IOfficeWorkerProcess Start(string executablePath) => process;
    }

    private sealed class ControlledWorkerProcess(
        DeferredTextReader standardOutput)
        : IOfficeWorkerProcess
    {
        private readonly TaskCompletionSource _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TextWriter StandardInput { get; } = new StringWriter();
        public TextReader StandardOutput => standardOutput;
        public TextReader StandardError { get; } = new StringReader(string.Empty);
        public int ExitCode => 0;
        public bool KillCalled { get; private set; }

        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task;

        public void Kill()
        {
            KillCalled = true;
            standardOutput.Release();
            _exit.TrySetResult();
        }

        public void Dispose()
        {
        }
    }

    private sealed class DeferredTextReader(string? line = null) : TextReader
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _read;

        public void Release() => _release.TrySetResult();

        public override async Task<string?> ReadLineAsync()
        {
            await _release.Task;
            if (_read)
            {
                return null;
            }

            _read = true;
            return line;
        }
    }

    private sealed class RecordingOfficeTerminator : IOwnedOfficeProcessTerminator
    {
        public bool WasCalled => Ownership is not null;
        public OfficeApplicationKind? Application { get; private set; }
        public OfficeProcessOwnership? Ownership { get; private set; }

        public bool TryTerminate(
            OfficeApplicationKind application,
            OfficeProcessOwnership ownership)
        {
            Application = application;
            Ownership = ownership;
            return true;
        }
    }

    private sealed class FakeProcessLookup(IOfficeProcessHandle? process)
        : IOfficeProcessLookup
    {
        public IOfficeProcessHandle? TryGet(int processId) => process;
    }

    private sealed class FakeProcessHandle(
        string processName,
        long startTimeUtcTicks)
        : IOfficeProcessHandle
    {
        public string ProcessName => processName;
        public long StartTimeUtcTicks => startTimeUtcTicks;
        public bool KillCalled { get; private set; }

        public void Kill() => KillCalled = true;

        public void Dispose()
        {
        }
    }
}
