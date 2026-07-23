using System.IO.Compression;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class OutputDestinationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "zlet-output-destination-tests",
        Guid.NewGuid().ToString("N"));

    public OutputDestinationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Folder_destination_rejects_source_and_parent_but_allows_child_and_external()
    {
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);

        Assert.Equal("output_equals_source",
            OutputPathGuard.ValidateFolderDestination(source, source).ErrorCode);
        Assert.Equal("output_is_source_parent",
            OutputPathGuard.ValidateFolderDestination(source, _root).ErrorCode);
        Assert.True(OutputPathGuard.ValidateFolderDestination(
            source, Path.Combine(source, "results")).IsValid);
        Assert.True(OutputPathGuard.ValidateFolderDestination(
            source, Path.Combine(_root, "external")).IsValid);
    }

    [Fact]
    public void Zip_destination_rejects_existing_target_and_staging_path()
    {
        var source = Path.Combine(_root, "source");
        var staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(staging);
        var existing = Path.Combine(_root, "existing.zip");
        File.WriteAllText(existing, "do not overwrite");

        Assert.Equal("zip_target_conflict",
            OutputPathGuard.ValidateZipDestination(source, existing, staging).ErrorCode);
        Assert.Equal("zip_inside_staging",
            OutputPathGuard.ValidateZipDestination(
                source, Path.Combine(staging, "result.zip"), staging).ErrorCode);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/drive.txt")]
    [InlineData("safe/../../escape.txt")]
    public void Zip_entry_validation_rejects_unsafe_paths(string entryName)
    {
        Assert.False(ResultZipPublisher.IsSafeEntryName(entryName));
    }

    [Fact]
    public async Task Zip_publisher_includes_only_successes_and_preserves_relative_paths()
    {
        var staging = Path.Combine(_root, "staging");
        var sourceFile = Path.Combine(_root, "source", "nested", "source.json");
        var successPath = Path.Combine(staging, "nested", "result.txt");
        var failedPath = Path.Combine(staging, "failed.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(successPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "{\"unchanged\":true}");
        var sourceHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourceFile)));
        File.WriteAllText(successPath, "success");
        File.WriteAllText(failedPath, "failed");
        var success = Result(successPath, OperationStatus.Succeeded);
        var failed = Result(failedPath, OperationStatus.Failed);
        var summary = new ConversionSummary(1, 0, 1, 0, 0, 0, [success, failed]);
        var zipPath = Path.Combine(_root, "result.zip");

        var published = await new ResultZipPublisher().PublishAsync(
            staging, zipPath, summary, CancellationToken.None);

        Assert.True(published.Created);
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal("nested/result.txt", entry.FullName);
        Assert.DoesNotContain(archive.Entries, item => item.FullName == "failed.txt");
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
        Assert.Equal(sourceHash, Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourceFile))));
    }

    [Fact]
    public async Task Zip_publisher_does_not_create_empty_archive()
    {
        var zipPath = Path.Combine(_root, "empty.zip");
        var summary = new ConversionSummary(0, 0, 1, 0, 0, 0, []);

        var result = await new ResultZipPublisher().PublishAsync(
            Path.Combine(_root, "staging"),
            zipPath,
            summary,
            CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal("no_successful_outputs", result.ErrorCode);
        Assert.False(File.Exists(zipPath));
    }

    [Fact]
    public async Task Zip_publisher_rejects_duplicate_entries()
    {
        var staging = Path.Combine(_root, "staging");
        var output = Path.Combine(staging, "same.txt");
        Directory.CreateDirectory(staging);
        File.WriteAllText(output, "value");
        var result = Result(output, OperationStatus.Succeeded);
        var summary = new ConversionSummary(2, 0, 0, 0, 0, 0, [result, result]);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ResultZipPublisher().PublishAsync(
                staging,
                Path.Combine(_root, "duplicate.zip"),
                summary,
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ConversionResult Result(string targetPath, OperationStatus status)
    {
        var relative = Path.GetRelativePath(Path.Combine(_root, "staging"), targetPath);
        var operation = new PlannedOperation(
            Path.Combine(_root, "source", relative),
            relative,
            SourceFormat.Json,
            ConversionTarget.Txt,
            ".txt",
            targetPath,
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_root, "staging"));
        return new ConversionResult(operation, status, status.ToString());
    }
}
