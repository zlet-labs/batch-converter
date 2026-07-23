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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private PlannedOperation CreateOperation(string sourcePath)
    {
        var relativePath = Path.GetFileName(sourcePath);
        return new PlannedOperation(
            sourcePath,
            relativePath,
            SourceFormat.Doc,
            ConversionTarget.Docx,
            ".docx",
            Path.Combine(
                _rootPath,
                "_converted",
                Path.GetFileNameWithoutExtension(sourcePath) + ".docx"),
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
}
