using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class FileSystemFolderScannerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-folder-converter-scan-tests",
        Guid.NewGuid().ToString("N"));

    public FileSystemFolderScannerTests() => Directory.CreateDirectory(_rootPath);

    [Fact]
    public async Task ScanAsync_finds_mixed_known_and_unknown_files()
    {
        Write("one.json");
        Write("two.docx");
        Write("three.pdf");
        Write("four.png");
        Write("five.bin");

        var result = await ScanAsync(includeSubfolders: false);

        Assert.Equal(5, result.Files.Count);
        Assert.Contains(result.Files, file => file.Format == SourceFormat.Json);
        Assert.Contains(result.Files, file => file.Format == SourceFormat.Docx);
        Assert.Contains(result.Files, file => file.Format == SourceFormat.Pdf);
        Assert.Contains(result.Files, file => file.Format == SourceFormat.Image);
        Assert.Contains(result.Files, file => file.Format == SourceFormat.Unknown);
    }

    [Fact]
    public async Task ScanAsync_excludes_temporary_office_files()
    {
        Write("~$draft.doc");
        Write("normal.doc");

        var result = await ScanAsync(includeSubfolders: false);

        Assert.Equal("normal.doc", Assert.Single(result.Files).RelativePath);
    }

    [Fact]
    public async Task ScanAsync_excludes_root_converted_but_includes_nested_converted()
    {
        Write(Path.Combine("_converted", "ignored.json"));
        Write(Path.Combine("archive", "_converted", "included.json"));

        var result = await ScanAsync(includeSubfolders: true);

        Assert.Equal(
            Path.Combine("archive", "_converted", "included.json"),
            Assert.Single(result.Files).RelativePath);
    }

    [Fact]
    public async Task ScanAsync_excludes_only_explicit_output_directory_and_zip_file()
    {
        var excludedDirectory = Path.Combine(_rootPath, "results");
        var excludedZip = Path.Combine(_rootPath, "batch-results.zip");
        Write(Path.Combine("results", "ignored.json"));
        Write(Path.Combine("results-copy", "included.json"));
        Write("batch-results.zip");
        Write("batch-results.zip.backup");

        var result = await new FileSystemFolderScanner().ScanAsync(
            _rootPath,
            includeSubfolders: true,
            excludedDirectory,
            excludedZip,
            CancellationToken.None);

        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Files, file =>
            file.RelativePath == Path.Combine("results-copy", "included.json"));
        Assert.Contains(result.Files, file => file.RelativePath == "batch-results.zip.backup");
    }

    [Fact]
    public async Task ScanAsync_does_not_follow_reparse_directory_when_supported()
    {
        var external = Path.Combine(
            Path.GetTempPath(),
            "zlet-folder-converter-link-target",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "linked.json"), "{}");
        var link = Path.Combine(_rootPath, "linked");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, external);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                                               or IOException
                                               or NotSupportedException)
            {
                return;
            }

            var result = await ScanAsync(includeSubfolders: true);

            Assert.DoesNotContain(result.Files, file =>
                file.RelativePath.Contains("linked", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(external))
            {
                Directory.Delete(external, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task ScanAsync_respects_include_subfolders(bool includeSubfolders, int expected)
    {
        Write("root.doc");
        Write(Path.Combine("nested", "child.xls"));

        var result = await ScanAsync(includeSubfolders);

        Assert.Equal(expected, result.Files.Count);
    }

    [Fact]
    public async Task ScanAsync_preserves_spaces_cyrillic_and_unicode()
    {
        var relativePath = Path.Combine("договоры с пробелами", "файл Ω.doc");
        Write(relativePath);

        var result = await ScanAsync(includeSubfolders: true);

        Assert.Equal(relativePath, Assert.Single(result.Files).RelativePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private Task<ScanResult> ScanAsync(bool includeSubfolders) =>
        new FileSystemFolderScanner().ScanAsync(
            _rootPath,
            includeSubfolders,
            CancellationToken.None);

    private void Write(string relativePath)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "synthetic fixture");
    }
}
