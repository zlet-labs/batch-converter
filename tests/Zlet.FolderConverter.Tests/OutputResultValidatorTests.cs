using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class OutputResultValidatorTests : IDisposable
{
    private readonly string _rootPath;
    private readonly OutputResultValidator _validator = new();

    public OutputResultValidatorTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "zlet-folder-converter-output-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void IsSuccessfulOutput_returns_false_when_output_is_missing()
    {
        var targetPath = Path.Combine(_rootPath, "missing.docx");

        Assert.False(_validator.IsSuccessfulOutput(targetPath));
    }

    [Fact]
    public void IsSuccessfulOutput_returns_false_when_output_is_empty()
    {
        var targetPath = Path.Combine(_rootPath, "empty.docx");
        File.WriteAllBytes(targetPath, []);

        Assert.False(_validator.IsSuccessfulOutput(targetPath));
    }

    [Fact]
    public void IsSuccessfulOutput_returns_true_when_output_exists_and_has_non_zero_size()
    {
        var targetPath = Path.Combine(_rootPath, "result.docx");
        File.WriteAllText(targetPath, "synthetic output");

        Assert.True(_validator.IsSuccessfulOutput(targetPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
