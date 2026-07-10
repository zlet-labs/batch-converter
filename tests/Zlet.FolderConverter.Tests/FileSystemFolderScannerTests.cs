using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class FileSystemFolderScannerTests : IDisposable
{
    private readonly string _rootPath;

    public FileSystemFolderScannerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "zlet-folder-converter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task ScanAsync_detects_doc_xls_ppt_and_counts_them()
    {
        WriteSyntheticFile("one.doc");
        WriteSyntheticFile("two.xls");
        WriteSyntheticFile("three.ppt");

        var result = await ScanAsync(includeSubfolders: false);

        Assert.Equal(3, result.Files.Count);
        Assert.Equal(1, result.DocCount);
        Assert.Equal(1, result.XlsCount);
        Assert.Equal(1, result.PptCount);
    }

    [Fact]
    public async Task ScanAsync_excludes_temporary_office_files()
    {
        WriteSyntheticFile("~$draft.doc");
        WriteSyntheticFile("normal.doc");

        var result = await ScanAsync(includeSubfolders: false);

        Assert.Single(result.Files);
        Assert.Equal("normal.doc", result.Files[0].RelativePath);
        Assert.True(FileSystemFolderScanner.IsOfficeTemporaryFile(Path.Combine(_rootPath, "~$draft.doc")));
    }

    [Fact]
    public async Task ScanAsync_excludes_converted_directories()
    {
        WriteSyntheticFile(Path.Combine("_converted", "old.doc"));
        WriteSyntheticFile("source.doc");

        var result = await ScanAsync(includeSubfolders: true);

        Assert.Single(result.Files);
        Assert.Equal("source.doc", result.Files[0].RelativePath);
        Assert.True(FileSystemFolderScanner.IsConvertedDirectory(Path.Combine(_rootPath, "_converted")));
    }

    [Fact]
    public async Task ScanAsync_respects_include_subfolders_false()
    {
        WriteSyntheticFile("root.doc");
        WriteSyntheticFile(Path.Combine("nested", "child.xls"));

        var result = await ScanAsync(includeSubfolders: false);

        Assert.Single(result.Files);
        Assert.Equal("root.doc", result.Files[0].RelativePath);
    }

    [Fact]
    public async Task ScanAsync_respects_include_subfolders_true()
    {
        WriteSyntheticFile("root.doc");
        WriteSyntheticFile(Path.Combine("nested", "child.xls"));

        var result = await ScanAsync(includeSubfolders: true);

        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Files, file => file.RelativePath == Path.Combine("nested", "child.xls"));
    }

    [Fact]
    public async Task ScanAsync_preserves_relative_paths_with_spaces_cyrillic_and_unicode()
    {
        var relativePath = Path.Combine("договоры с пробелами", "тестовый файл Ω.doc");
        WriteSyntheticFile(relativePath);

        var result = await ScanAsync(includeSubfolders: true);

        var file = Assert.Single(result.Files);
        Assert.Equal(relativePath, file.RelativePath);
        Assert.Equal(DocumentFormat.Doc, file.Format);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private Task<ScanResult> ScanAsync(bool includeSubfolders)
    {
        var scanner = new FileSystemFolderScanner();
        return scanner.ScanAsync(_rootPath, includeSubfolders, CancellationToken.None);
    }

    private void WriteSyntheticFile(string relativePath)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "synthetic test fixture");
    }
}
