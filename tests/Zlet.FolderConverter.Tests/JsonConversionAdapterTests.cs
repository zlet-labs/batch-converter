using System.Text;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class JsonConversionAdapterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-json-adapter-tests",
        Guid.NewGuid().ToString("N"));

    public JsonConversionAdapterTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    public async Task ConvertAsync_writes_nonempty_utf8_preserves_content_order_and_source(string extension)
    {
        const string source = """{"имя":"Анна 😀","nested":{"value":null},"items":[1,true,"```"]}""";
        var sourcePath = Write("nested/source.json", source);
        var operation = CreateOperation(sourcePath, Path.Combine("nested", $"source{extension}"), extension);
        var validator = new TrackingValidator();

        var result = await new JsonConversionAdapter(validator).ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        var output = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.Contains("Анна", output);
        Assert.True(output.IndexOf("\"имя\"", StringComparison.Ordinal) < output.IndexOf("\"nested\"", StringComparison.Ordinal));
        Assert.True(new FileInfo(operation.TargetPath).Length > 0);
        Assert.Equal(source, await File.ReadAllTextAsync(sourcePath));
        Assert.True(validator.CallCount >= 2);
        if (extension == ".md")
        {
            Assert.StartsWith("# source.json", output);
            Assert.Contains("````json", output);
            var jsonStart = output.IndexOf(Environment.NewLine, output.IndexOf("````json", StringComparison.Ordinal), StringComparison.Ordinal)
                + Environment.NewLine.Length;
            var jsonEnd = output.LastIndexOf(Environment.NewLine + "````", StringComparison.Ordinal);
            using var parsed = JsonDocument.Parse(output[jsonStart..jsonEnd]);
            Assert.Equal("Анна 😀", parsed.RootElement.GetProperty("имя").GetString());
        }
        else
        {
            using var parsed = JsonDocument.Parse(output);
            Assert.Equal("Анна 😀", parsed.RootElement.GetProperty("имя").GetString());
        }
    }

    [Fact]
    public async Task ConvertAsync_invalid_json_fails_without_output()
    {
        var sourcePath = Write("broken.json", """{"value": }""");
        var operation = CreateOperation(sourcePath, "broken.txt", ".txt");

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.StartsWith("Некорректный JSON:", result.Message);
        Assert.False(File.Exists(operation.TargetPath));
        Assert.Equal("""{"value": }""", await File.ReadAllTextAsync(sourcePath));
    }

    [Fact]
    public async Task Processor_continues_after_invalid_json()
    {
        var bad = CreateOperation(Write("bad.json", "{"), "bad.txt", ".txt");
        var good = CreateOperation(Write("good.json", """{"ok":true}"""), "good.txt", ".txt");
        var resolver = new DefaultConversionAdapterResolver();

        var summary = await new ConversionProcessor(resolver)
            .ProcessAsync([bad, good], CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Succeeded);
        Assert.True(File.Exists(good.TargetPath));
        Assert.False(File.Exists(bad.TargetPath));
    }

    [Fact]
    public async Task ConvertAsync_protects_file_and_directory_conflicts()
    {
        var sourcePath = Write("source.json", "{}");
        var fileOperation = CreateOperation(sourcePath, "file.txt", ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(fileOperation.TargetPath)!);
        await File.WriteAllTextAsync(fileOperation.TargetPath, "existing");
        var directoryOperation = CreateOperation(sourcePath, "directory.txt", ".txt");
        Directory.CreateDirectory(directoryOperation.TargetPath);
        var adapter = new JsonConversionAdapter(new OutputResultValidator());

        var fileResult = await adapter.ConvertAsync(fileOperation, CancellationToken.None);
        var directoryResult = await adapter.ConvertAsync(directoryOperation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, fileResult.Status);
        Assert.Equal(OperationStatus.Conflict, directoryResult.Status);
        Assert.Equal("existing", await File.ReadAllTextAsync(fileOperation.TargetPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private PlannedOperation CreateOperation(string sourcePath, string targetRelativePath, string extension) =>
        new(
            sourcePath,
            Path.GetRelativePath(_rootPath, sourcePath),
            DocumentFormat.Json,
            extension,
            Path.Combine(_rootPath, "_converted", targetRelativePath),
            true,
            OperationStatus.Ready,
            "ready");

    private sealed class TrackingValidator : IOutputResultValidator
    {
        private readonly OutputResultValidator _inner = new();
        public int CallCount { get; private set; }
        public bool IsSuccessfulOutput(string targetPath)
        {
            CallCount++;
            return _inner.IsSuccessfulOutput(targetPath);
        }
    }
}
