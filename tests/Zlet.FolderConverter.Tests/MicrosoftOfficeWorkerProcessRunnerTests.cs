using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;
using System.Text.Json;

namespace Zlet.FolderConverter.Tests;

public sealed class MicrosoftOfficeWorkerProcessRunnerTests : IDisposable
{
    private readonly string _workerPath = Path.Combine(
        Path.GetTempPath(),
        $"zlet-worker-placeholder-{Guid.NewGuid():N}.exe");

    [Fact]
    public async Task Timeout_terminates_only_the_worker_session()
    {
        await File.WriteAllTextAsync(_workerPath, "placeholder");
        var process = new NeverEndingWorkerProcess();
        var officeTerminator = new RecordingOfficeTerminator();
        var runner = new MicrosoftOfficeWorkerProcessRunner(
            new MicrosoftOfficeWorkerOptions
            {
                WorkerExecutablePath = _workerPath,
                Timeout = TimeSpan.FromMilliseconds(30)
            },
            new FakeLauncher(process),
            officeTerminator);

        var result = await runner.RunAsync(
            new OfficeWorkerRequest(
                OfficeApplicationKind.Word,
                @"C:\source.doc",
                @"C:\result.docx"),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Equal("worker_timeout", result.ErrorCode);
        Assert.True(process.KillCalled);
        Assert.False(officeTerminator.WasCalled);
    }

    [Fact]
    public async Task Timeout_terminates_only_the_exact_owned_office_process()
    {
        await File.WriteAllTextAsync(_workerPath, "placeholder");
        var started = JsonSerializer.Serialize(
            new OfficeWorkerMessage(
                OfficeWorkerMessageType.Started,
                OfficeProcessId: 4242,
                OfficeProcessStartTimeUtcTicks: 638900000000000000,
                OfficeProcessOwned: true),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var process = new NeverEndingWorkerProcess(started);
        var officeTerminator = new RecordingOfficeTerminator();
        var runner = new MicrosoftOfficeWorkerProcessRunner(
            new MicrosoftOfficeWorkerOptions
            {
                WorkerExecutablePath = _workerPath,
                Timeout = TimeSpan.FromMilliseconds(30)
            },
            new FakeLauncher(process),
            officeTerminator);

        var result = await runner.RunAsync(
            new OfficeWorkerRequest(
                OfficeApplicationKind.Excel,
                @"C:\source.xls",
                @"C:\result.xlsx"),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.True(process.KillCalled);
        Assert.Equal(OfficeApplicationKind.Excel, officeTerminator.Application);
        Assert.Equal(
            new OfficeProcessOwnership(4242, 638900000000000000),
            officeTerminator.Ownership);
    }

    public void Dispose()
    {
        if (File.Exists(_workerPath))
        {
            File.Delete(_workerPath);
        }
    }

    private sealed class FakeLauncher(IOfficeWorkerProcess process)
        : IOfficeWorkerProcessLauncher
    {
        public IOfficeWorkerProcess Start(string executablePath) => process;
    }

    private sealed class NeverEndingWorkerProcess
        : IOfficeWorkerProcess
    {
        private readonly TaskCompletionSource _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public NeverEndingWorkerProcess(string standardOutput = "")
        {
            StandardOutput = new StringReader(standardOutput);
        }

        public TextWriter StandardInput { get; } = new StringWriter();
        public TextReader StandardOutput { get; }
        public TextReader StandardError { get; } = new StringReader(string.Empty);
        public int ExitCode => 0;
        public bool KillCalled { get; private set; }

        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task;

        public void Kill()
        {
            KillCalled = true;
            _exit.TrySetResult();
        }

        public void Dispose()
        {
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
}
