using System.ComponentModel;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class LibreOfficeConversionAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-lo-adapter-tests",
        Guid.NewGuid().ToString("N"));
    private readonly string _temporaryPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-lo-adapter-work",
        Guid.NewGuid().ToString("N"));

    public LibreOfficeConversionAdapterTests()
    {
        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(_temporaryPath);
    }

    [Fact]
    public async Task ConvertAsync_success_moves_validated_output_and_preserves_source()
    {
        var operation = CreateOperation();
        var sourceContent = File.ReadAllText(operation.SourcePath);
        var runner = new FakeRunner(request =>
        {
            var output = ExpectedOutput(request);
            OutputResultValidatorTests.CreateZip(
                output,
                "[Content_Types].xml",
                "word/document.xml");
            return new LibreOfficeProcessResult(0, "converted", "");
        });

        var result = await CreateAdapter(runner)
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        Assert.Equal(sourceContent, File.ReadAllText(operation.SourcePath));
        Assert.Empty(Directory.EnumerateDirectories(_temporaryPath));
    }

    [Theory]
    [InlineData(FailureMode.ExitCode, "process_exit_failure")]
    [InlineData(FailureMode.Timeout, "process_timeout")]
    [InlineData(FailureMode.MissingOutput, "output_missing")]
    [InlineData(FailureMode.WrongOutput, "output_unreadable")]
    [InlineData(FailureMode.StartFailure, "process_start_failure")]
    public async Task ConvertAsync_handles_process_and_output_failures(
        FailureMode mode,
        string expectedCode)
    {
        var operation = CreateOperation();
        var runner = new FakeRunner(request =>
        {
            if (mode == FailureMode.StartFailure)
            {
                throw new Win32Exception("synthetic start failure");
            }

            if (mode == FailureMode.WrongOutput)
            {
                File.WriteAllText(ExpectedOutput(request), "not an OOXML package");
            }

            return mode switch
            {
                FailureMode.ExitCode => new LibreOfficeProcessResult(12, "", "synthetic"),
                FailureMode.Timeout => new LibreOfficeProcessResult(null, "", "", TimedOut: true),
                _ => new LibreOfficeProcessResult(0, "", "")
            };
        });

        var result = await CreateAdapter(runner)
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal(expectedCode, result.Diagnostic?.ErrorCode);
        Assert.False(File.Exists(operation.TargetPath));
        Assert.Equal("synthetic source", File.ReadAllText(operation.SourcePath));
    }

    [Fact]
    public async Task ConvertAsync_propagates_cancellation_and_cleans_temporary_files()
    {
        var operation = CreateOperation();
        var runner = new FakeRunner(_ => throw new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateAdapter(runner).ConvertAsync(operation, CancellationToken.None));

        Assert.False(File.Exists(operation.TargetPath));
        Assert.Empty(Directory.EnumerateDirectories(_temporaryPath));
    }

    [Fact]
    public async Task ConvertAsync_returns_engine_unavailable_without_starting_process()
    {
        var operation = CreateOperation();
        var runner = new FakeRunner(_ => throw new InvalidOperationException("must not run"));
        var adapter = new LibreOfficeConversionAdapter(
            new FakeRuntimeLocator(available: false),
            runner,
            new OutputResultValidator(),
            Options());

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.EngineUnavailable, result.Status);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task ConvertAsync_protects_late_target_conflict()
    {
        var operation = CreateOperation();
        var runner = new FakeRunner(request =>
        {
            OutputResultValidatorTests.CreateZip(
                ExpectedOutput(request),
                "[Content_Types].xml",
                "word/document.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
            File.WriteAllText(operation.TargetPath, "existing");
            return new LibreOfficeProcessResult(0, "", "");
        });

        var result = await CreateAdapter(runner)
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("existing", File.ReadAllText(operation.TargetPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }

        if (Directory.Exists(_temporaryPath))
        {
            Directory.Delete(_temporaryPath, recursive: true);
        }
    }

    private LibreOfficeConversionAdapter CreateAdapter(ILibreOfficeProcessRunner runner) =>
        new(
            new FakeRuntimeLocator(available: true),
            runner,
            new OutputResultValidator(),
            Options());

    private LibreOfficeConversionOptions Options() => new()
    {
        Timeout = TimeSpan.FromMilliseconds(250),
        TemporaryRootPath = _temporaryPath
    };

    private PlannedOperation CreateOperation()
    {
        var source = Path.Combine(_rootPath, "nested", "документ с пробелами.doc");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "synthetic source");
        return new PlannedOperation(
            source,
            Path.Combine("nested", "документ с пробелами.doc"),
            SourceFormat.Doc,
            ConversionTarget.Docx,
            ".docx",
            Path.Combine(_rootPath, "_converted", "nested", "документ с пробелами.docx"),
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_rootPath, "_converted"));
    }

    private static string ExpectedOutput(LibreOfficeProcessRequest request) =>
        Path.Combine(
            request.OutputDirectory,
            Path.GetFileNameWithoutExtension(request.SourcePath) + request.Target.ToExtension());

    public enum FailureMode
    {
        ExitCode,
        Timeout,
        MissingOutput,
        WrongOutput,
        StartFailure
    }

    private sealed class FakeRuntimeLocator(bool available) : ILibreOfficeRuntimeLocator
    {
        public LibreOfficeRuntimeLocation Locate() =>
            new(available, available ? @"C:\synthetic\soffice.exe" : string.Empty);
    }

    private sealed class FakeRunner(
        Func<LibreOfficeProcessRequest, LibreOfficeProcessResult> handler) : ILibreOfficeProcessRunner
    {
        public int CallCount { get; private set; }

        public Task<LibreOfficeProcessResult> RunAsync(
            LibreOfficeProcessRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(handler(request));
        }
    }
}
