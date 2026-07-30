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
    [InlineData(ConversionTarget.Txt, ".txt")]
    [InlineData(ConversionTarget.Markdown, ".md")]
    public async Task ConvertAsync_writes_utf8_and_preserves_source(
        ConversionTarget target,
        string extension)
    {
        const string source = """{"имя":"Анна 😀","nested":{"value":null},"items":[1,true,"```"]}""";
        var sourcePath = Write("nested/source.json", source);
        var operation = CreateOperation(sourcePath, Path.Combine("nested", $"source{extension}"), target);
        var validator = new TrackingValidator();

        var result = await new JsonConversionAdapter(validator)
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        var output = await File.ReadAllTextAsync(operation.TargetPath, Encoding.UTF8);
        Assert.Contains("Анна", output);
        Assert.Equal(source, await File.ReadAllTextAsync(sourcePath));
        Assert.True(validator.CallCount >= 2);
        if (target == ConversionTarget.Markdown)
        {
            Assert.StartsWith("# source.json", output);
            Assert.Contains("````json", output);
        }
        else
        {
            using var parsed = JsonDocument.Parse(output);
            Assert.Equal("Анна 😀", parsed.RootElement.GetProperty("имя").GetString());
        }
    }

    [Fact]
    public async Task ConvertAsync_invalid_json_fails_without_partial_output()
    {
        var sourcePath = Write("broken.json", """{"value": }""");
        var operation = CreateOperation(sourcePath, "broken.txt", ConversionTarget.Txt);

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.StartsWith("Некорректный JSON:", result.Message);
        Assert.False(File.Exists(operation.TargetPath));
        Assert.Equal("""{"value": }""", await File.ReadAllTextAsync(sourcePath));
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("prefix")]
    public async Task ConvertAsync_rejects_target_outside_output_root(string scenario)
    {
        var sourcePath = Write("source.json", "{}");
        var outputRoot = Path.Combine(_rootPath, "_converted");
        var targetPath = scenario == "outside"
            ? Path.Combine(outputRoot, "..", "outside.txt")
            : Path.Combine(_rootPath, "_converted-other", "prefix.txt");
        var operation = CreateOperation(sourcePath, "source.txt", ConversionTarget.Txt) with
        {
            TargetPath = targetPath
        };

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("Недопустимый путь результата.", result.Message);
        Assert.False(File.Exists(Path.GetFullPath(targetPath)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConvertAsync_protects_file_and_directory_conflicts(bool directoryConflict)
    {
        var sourcePath = Write("source.json", "{}");
        var operation = CreateOperation(sourcePath, "source.txt", ConversionTarget.Txt);
        Directory.CreateDirectory(Path.GetDirectoryName(operation.TargetPath)!);
        if (directoryConflict)
        {
            Directory.CreateDirectory(operation.TargetPath);
        }
        else
        {
            await File.WriteAllTextAsync(operation.TargetPath, "existing");
        }

        var result = await new JsonConversionAdapter(new OutputResultValidator())
            .ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        if (!directoryConflict)
        {
            Assert.Equal("existing", await File.ReadAllTextAsync(operation.TargetPath));
        }
    }

    [Fact]
    public async Task Processor_continues_after_invalid_json()
    {
        var bad = CreateOperation(Write("bad.json", "{"), "bad.txt", ConversionTarget.Txt);
        var good = CreateOperation(Write("good.json", """{"ok":true}"""), "good.txt", ConversionTarget.Txt);
        var resolver = new DefaultConversionAdapterResolver();

        var summary = await new ConversionProcessor(resolver)
            .ProcessAsync([bad, good], progress: null, CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Succeeded);
        Assert.True(File.Exists(good.TargetPath));
        Assert.False(File.Exists(bad.TargetPath));
    }

    [Fact]
    public async Task Processor_honors_cancellation()
    {
        var operation = CreateOperation(
            Write("source.json", "{}"),
            "source.txt",
            ConversionTarget.Txt);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ConversionProcessor(new DefaultConversionAdapterResolver())
                .ProcessAsync([operation], progress: null, cancellation.Token));
        Assert.False(File.Exists(operation.TargetPath));
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

    private PlannedOperation CreateOperation(
        string sourcePath,
        string targetRelativePath,
        ConversionTarget target) =>
        new(
            sourcePath,
            Path.GetRelativePath(_rootPath, sourcePath),
            SourceFormat.Json,
            target,
            target.ToExtension(),
            Path.Combine(_rootPath, "_converted", targetRelativePath),
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_rootPath, "_converted"));

    private sealed class TrackingValidator : IOutputResultValidator
    {
        private readonly OutputResultValidator _inner = new();
        public int CallCount { get; private set; }

        public OutputValidationResult Validate(string targetPath, ConversionTarget target)
        {
            CallCount++;
            return _inner.Validate(targetPath, target);
        }
    }
}
