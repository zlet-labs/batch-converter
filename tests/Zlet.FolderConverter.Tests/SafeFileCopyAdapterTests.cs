using System.Security.Cryptography;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class SafeFileCopyAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-safe-copy-tests",
        Guid.NewGuid().ToString("N"));

    public SafeFileCopyAdapterTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(SourceFormat.Docx, ".docx", "word/document.xml")]
    [InlineData(SourceFormat.Xlsx, ".xlsx", "xl/workbook.xml")]
    [InlineData(SourceFormat.Pptx, ".pptx", "ppt/presentation.xml")]
    public async Task Modern_office_files_are_copied_without_changes(
        SourceFormat sourceFormat,
        string extension,
        string requiredPart)
    {
        var sourcePath = Path.Combine(_rootPath, "nested", $"source{extension}");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        OutputResultValidatorTests.CreateZip(
            sourcePath,
            "[Content_Types].xml",
            requiredPart);
        var sourceHash = Hash(sourcePath);
        var operation = CreateOperation(sourcePath, sourceFormat);

        var result = await new SafeFileCopyAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.Equal("Скопировано.", result.Message);
        Assert.Equal(sourceHash, Hash(sourcePath));
        Assert.Equal(sourceHash, Hash(operation.TargetPath));
    }

    [Fact]
    public async Task Existing_target_is_not_overwritten()
    {
        var sourcePath = CreateDocx("source.docx");
        var operation = CreateOperation(sourcePath, SourceFormat.Docx);
        Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
        File.WriteAllText(operation.TargetPath, "existing");

        var result = await new SafeFileCopyAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("existing", File.ReadAllText(operation.TargetPath));
    }

    [Fact]
    public async Task Invalid_file_does_not_stop_next_copy_and_temp_is_cleaned()
    {
        var temporaryRoot = Path.Combine(_rootPath, "temporary");
        var invalidPath = Path.Combine(_rootPath, "invalid.docx");
        File.WriteAllText(invalidPath, "not OOXML");
        var validPath = CreateDocx("valid.docx");
        var adapter = new SafeFileCopyAdapter(
            new OutputResultValidator(),
            temporaryRoot);
        var resolver = new DefaultConversionAdapterResolver([adapter]);

        var summary = await new ConversionProcessor(resolver).ProcessAsync(
            [
                CreateOperation(invalidPath, SourceFormat.Docx),
                CreateOperation(validPath, SourceFormat.Docx)
            ],
            progress: null,
            CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Succeeded);
        Assert.False(File.Exists(Path.Combine(_rootPath, "_converted", "invalid.docx")));
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "valid.docx")));
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

    private string CreateDocx(string relativePath)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        OutputResultValidatorTests.CreateZip(
            path,
            "[Content_Types].xml",
            "word/document.xml");
        return path;
    }

    private PlannedOperation CreateOperation(
        string sourcePath,
        SourceFormat sourceFormat)
    {
        var relativePath = Path.GetRelativePath(_rootPath, sourcePath);
        return new PlannedOperation(
            sourcePath,
            relativePath,
            sourceFormat,
            ConversionTarget.Copy,
            Path.GetExtension(sourcePath),
            Path.Combine(_rootPath, "_converted", relativePath),
            true,
            OperationStatus.Ready,
            "Будет скопирован без изменений.",
            Path.Combine(_rootPath, "_converted"),
            _rootPath);
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
