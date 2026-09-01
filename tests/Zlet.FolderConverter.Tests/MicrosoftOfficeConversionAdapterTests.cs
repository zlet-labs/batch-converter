using System.Security.Cryptography;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class MicrosoftOfficeConversionAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-office-adapter-tests",
        Guid.NewGuid().ToString("N"));

    public MicrosoftOfficeConversionAdapterTests() => Directory.CreateDirectory(_rootPath);

    [Fact]
    public async Task Successful_worker_output_is_validated_and_source_hash_is_unchanged()
    {
        var sourcePath = Path.Combine(_rootPath, "legacy.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy fixture");
        var sourceHash = Hash(sourcePath);
        var temporaryRoot = Path.Combine(_rootPath, "temporary");
        var adapter = new MicrosoftOfficeConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector(
                [OfficeApplicationKind.Word]),
            new ValidOutputWorkerRunner(),
            new OutputResultValidator(),
            temporaryRoot);
        var operation = CreateOperation(sourcePath);

        var result = await adapter.ConvertAsync(
            operation,
            CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(File.Exists(operation.TargetPath));
        Assert.Equal(sourceHash, Hash(sourcePath));
        Assert.True(!Directory.Exists(temporaryRoot)
                    || !Directory.EnumerateFileSystemEntries(temporaryRoot).Any());
    }

    [Fact]
    public async Task Changed_source_is_detected_before_result_is_published()
    {
        var sourcePath = Path.Combine(_rootPath, "changed.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy fixture");
        var adapter = new MicrosoftOfficeConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector(
                [OfficeApplicationKind.Word]),
            new SourceChangingWorkerRunner(),
            new OutputResultValidator());
        var operation = CreateOperation(sourcePath);

        var result = await adapter.ConvertAsync(
            operation,
            CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("source_changed", result.Diagnostic?.ErrorCode);
        Assert.False(File.Exists(operation.TargetPath));
    }

    [Fact]
    public async Task Running_powerpoint_has_distinct_user_message()
    {
        var sourcePath = Path.Combine(_rootPath, "legacy.ppt");
        await File.WriteAllTextAsync(sourcePath, "legacy fixture");
        var adapter = new MicrosoftOfficeConversionAdapter(
            OfficeApplicationKind.PowerPoint,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector(
                [OfficeApplicationKind.PowerPoint]),
            new ErrorWorkerRunner("powerpoint_already_running"),
            new OutputResultValidator());
        var operation = CreateOperation(
            sourcePath,
            SourceFormat.Ppt,
            ConversionTarget.Pptx);

        var result = await adapter.ConvertAsync(
            operation,
            CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("powerpoint_already_running", result.Diagnostic?.ErrorCode);
        Assert.Equal(
            "PowerPoint уже запущен. Закройте его и повторите преобразование.",
            result.Message);
    }

    [Fact]
    public async Task Powerpoint_com_start_failure_explains_how_to_recover()
    {
        var sourcePath = Path.Combine(_rootPath, "legacy.ppt");
        await File.WriteAllTextAsync(sourcePath, "legacy fixture");
        var adapter = new MicrosoftOfficeConversionAdapter(
            OfficeApplicationKind.PowerPoint,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector(
                [OfficeApplicationKind.PowerPoint]),
            new ErrorWorkerRunner(
                "office_com_failure",
                hResult: unchecked((int)0x80080005)),
            new OutputResultValidator());
        var operation = CreateOperation(
            sourcePath,
            SourceFormat.Ppt,
            ConversionTarget.Pptx);

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("office_com_failure", result.Diagnostic?.ErrorCode);
        Assert.Equal(unchecked((int)0x80080005), result.Diagnostic?.HResult);
        Assert.Contains("PowerPoint не запустился", result.Message);
        Assert.Contains("Откройте PowerPoint вручную", result.Message);
        Assert.Contains("HRESULT 0x80080005", result.Message);
    }

    [Fact]
    public async Task Powerpoint_ownership_loss_explains_user_content_was_protected()
    {
        var sourcePath = Path.Combine(_rootPath, "ownership-lost.ppt");
        await File.WriteAllTextAsync(sourcePath, "legacy fixture");
        var adapter = new MicrosoftOfficeConversionAdapter(
            OfficeApplicationKind.PowerPoint,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector(
                [OfficeApplicationKind.PowerPoint]),
            new ErrorWorkerRunner("powerpoint_session_ownership_lost"),
            new OutputResultValidator());
        var operation = CreateOperation(
            sourcePath,
            SourceFormat.Ppt,
            ConversionTarget.Pptx);

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("powerpoint_session_ownership_lost", result.Diagnostic?.ErrorCode);
        Assert.Contains("не закрыть пользовательскую презентацию", result.Message);
    }

    [Fact]
    public async Task Worker_failure_removes_partial_temporary_output_and_preserves_source()
    {
        var sourcePath = Path.Combine(_rootPath, "partial.doc");
        await File.WriteAllTextAsync(sourcePath, "legacy fixture");
        var sourceHash = Hash(sourcePath);
        var temporaryRoot = Path.Combine(_rootPath, "temporary-partial");
        var adapter = new MicrosoftOfficeConversionAdapter(
            OfficeApplicationKind.Word,
            new MicrosoftOfficeCapabilityTests.FakeCapabilityDetector(
                [OfficeApplicationKind.Word]),
            new PartialOutputErrorWorkerRunner(),
            new OutputResultValidator(),
            temporaryRoot);
        var operation = CreateOperation(sourcePath);

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.False(File.Exists(operation.TargetPath));
        Assert.Equal(sourceHash, Hash(sourcePath));
        Assert.True(!Directory.Exists(temporaryRoot)
                    || !Directory.EnumerateFileSystemEntries(temporaryRoot).Any());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private PlannedOperation CreateOperation(
        string sourcePath,
        SourceFormat sourceFormat = SourceFormat.Doc,
        ConversionTarget target = ConversionTarget.Docx)
    {
        var relativePath = Path.GetFileName(sourcePath);
        return new PlannedOperation(
            sourcePath,
            relativePath,
            sourceFormat,
            target,
            target.ToExtension(),
            Path.Combine(
                _rootPath,
                "_converted",
                Path.GetFileNameWithoutExtension(sourcePath) + target.ToExtension()),
            true,
            OperationStatus.Ready,
            "Готово к преобразованию.",
            Path.Combine(_rootPath, "_converted"),
            _rootPath);
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class ValidOutputWorkerRunner : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => true;

        public Task<OfficeWorkerExecutionResult> RunAsync(
            OfficeWorkerRequest request,
            CancellationToken cancellationToken)
        {
            OutputResultValidatorTests.CreateZip(
                request.OutputPath,
                "[Content_Types].xml",
                "word/document.xml");
            return Task.FromResult(new OfficeWorkerExecutionResult(true));
        }
    }

    private sealed class SourceChangingWorkerRunner : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => true;

        public async Task<OfficeWorkerExecutionResult> RunAsync(
            OfficeWorkerRequest request,
            CancellationToken cancellationToken)
        {
            OutputResultValidatorTests.CreateZip(
                request.OutputPath,
                "[Content_Types].xml",
                "word/document.xml");
            await File.AppendAllTextAsync(
                request.SourcePath,
                "changed",
                cancellationToken);
            return new OfficeWorkerExecutionResult(true);
        }
    }

    private sealed class ErrorWorkerRunner(string errorCode, int? hResult = null)
        : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => true;

        public Task<OfficeWorkerExecutionResult> RunAsync(
            OfficeWorkerRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new OfficeWorkerExecutionResult(
                false,
                errorCode,
                HResult: hResult));
    }

    private sealed class PartialOutputErrorWorkerRunner : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => true;

        public async Task<OfficeWorkerExecutionResult> RunAsync(
            OfficeWorkerRequest request,
            CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(
                request.OutputPath,
                "partial",
                cancellationToken);
            return new OfficeWorkerExecutionResult(false, "office_com_failure");
        }
    }
}
