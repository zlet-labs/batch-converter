using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class ConversionPlannerTests : IDisposable
{
    private readonly string _rootPath;

    public ConversionPlannerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "zlet-folder-converter-planner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Theory]
    [InlineData(DocumentFormat.Doc, "source.doc", ".docx")]
    [InlineData(DocumentFormat.Xls, "source.xls", ".xlsx")]
    [InlineData(DocumentFormat.Ppt, "source.ppt", ".pptx")]
    public void CreatePlan_generates_target_paths_for_all_mappings(
        DocumentFormat format,
        string relativePath,
        string expectedExtension)
    {
        var plan = CreatePlan(
            new ScannedFile(Path.Combine(_rootPath, relativePath), relativePath, format),
            new DefaultConversionAdapterResolver());

        var operation = Assert.Single(plan);
        Assert.Equal(expectedExtension, operation.TargetExtension);
        Assert.Equal(
            Path.Combine(_rootPath, "_converted", Path.ChangeExtension(relativePath, expectedExtension)),
            operation.TargetPath);
    }

    [Fact]
    public void CreatePlan_preserves_nested_paths_with_spaces_and_cyrillic()
    {
        var relativePath = Path.Combine("архив договоров", "old file.doc");
        var plan = CreatePlan(
            new ScannedFile(Path.Combine(_rootPath, relativePath), relativePath, DocumentFormat.Doc),
            new DefaultConversionAdapterResolver());

        var operation = Assert.Single(plan);
        Assert.Equal(
            Path.Combine(_rootPath, "_converted", "архив договоров", "old file.docx"),
            operation.TargetPath);
    }

    [Fact]
    public void CreatePlan_marks_unsupported_when_no_confirmed_adapter_exists()
    {
        var plan = CreatePlan(
            new ScannedFile(Path.Combine(_rootPath, "source.doc"), "source.doc", DocumentFormat.Doc),
            new DefaultConversionAdapterResolver());

        var operation = Assert.Single(plan);
        Assert.Equal(OperationStatus.Unsupported, operation.Status);
        Assert.False(operation.AdapterAvailable);
    }

    [Fact]
    public void CreatePlan_marks_ready_when_adapter_is_available()
    {
        var resolver = new DefaultConversionAdapterResolver(
            [new AvailableTestAdapter(DocumentFormat.Doc, ".docx")]);

        var plan = CreatePlan(
            new ScannedFile(Path.Combine(_rootPath, "source.doc"), "source.doc", DocumentFormat.Doc),
            resolver);

        var operation = Assert.Single(plan);
        Assert.Equal(OperationStatus.Ready, operation.Status);
        Assert.True(operation.AdapterAvailable);
    }

    [Fact]
    public void CreatePlan_marks_conflict_when_target_exists()
    {
        var targetPath = Path.Combine(_rootPath, "_converted", "source.docx");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "existing target");

        var plan = CreatePlan(
            new ScannedFile(Path.Combine(_rootPath, "source.doc"), "source.doc", DocumentFormat.Doc),
            new DefaultConversionAdapterResolver(
                [new AvailableTestAdapter(DocumentFormat.Doc, ".docx")]));

        var operation = Assert.Single(plan);
        Assert.Equal(OperationStatus.Conflict, operation.Status);
        Assert.True(operation.AdapterAvailable);
    }

    [Fact]
    public void CreatePlan_marks_conflict_when_target_is_a_directory()
    {
        var targetPath = Path.Combine(_rootPath, "_converted", "source.txt");
        Directory.CreateDirectory(targetPath);

        var plan = CreatePlan(
            new ScannedFile(Path.Combine(_rootPath, "source.json"), "source.json", DocumentFormat.Json),
            new DefaultConversionAdapterResolver());

        Assert.Equal(OperationStatus.Conflict, Assert.Single(plan).Status);
    }

    [Theory]
    [InlineData(OutputFormat.TXT, ".txt")]
    [InlineData(OutputFormat.Markdown, ".md")]
    public void CreatePlan_uses_selected_json_output_format(OutputFormat outputFormat, string extension)
    {
        var scanResult = new ScanResult(
            _rootPath,
            [new ScannedFile(Path.Combine(_rootPath, "nested", "users.json"), Path.Combine("nested", "users.json"), DocumentFormat.Json)],
            []);

        var operation = Assert.Single(new ConversionPlanner(new DefaultConversionAdapterResolver())
            .CreatePlan(scanResult, _rootPath, outputFormat));

        Assert.Equal(extension, operation.TargetExtension);
        Assert.Equal(Path.Combine(_rootPath, "_converted", "nested", $"users{extension}"), operation.TargetPath);
        Assert.Equal(OperationStatus.Ready, operation.Status);
    }

    [Fact]
    public void Resolver_returns_adapter_for_matching_format()
    {
        var adapter = new AvailableTestAdapter(DocumentFormat.Xls, ".xlsx");
        var resolver = new DefaultConversionAdapterResolver([adapter]);

        Assert.Same(adapter, resolver.Resolve(DocumentFormat.Xls));
        Assert.Null(resolver.Resolve(DocumentFormat.Ppt));
    }

    [Fact]
    public async Task Unsupported_adapter_returns_unsupported_result_without_throwing()
    {
        var operation = new PlannedOperation(
            "source.doc",
            "source.doc",
            DocumentFormat.Doc,
            ".docx",
            "target.docx",
            AdapterAvailable: false,
            OperationStatus.Unsupported,
            "unsupported");
        var adapter = new UnsupportedConversionAdapter(DocumentFormat.Doc, ".docx", "not supported");

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Unsupported, result.Status);
    }

    [Theory]
    [InlineData(OperationStatus.Ready)]
    [InlineData(OperationStatus.Unsupported)]
    [InlineData(OperationStatus.Conflict)]
    [InlineData(OperationStatus.Failed)]
    [InlineData(OperationStatus.Succeeded)]
    [InlineData(OperationStatus.Skipped)]
    public void OperationStatus_contains_required_statuses(OperationStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private IReadOnlyList<PlannedOperation> CreatePlan(
        ScannedFile scannedFile,
        IConversionAdapterResolver resolver)
    {
        var scanResult = new ScanResult(_rootPath, [scannedFile], []);
        return new ConversionPlanner(resolver).CreatePlan(scanResult, _rootPath);
    }

    private sealed class AvailableTestAdapter(
        DocumentFormat sourceFormat,
        string targetExtension) : IConversionAdapter
    {
        public DocumentFormat SourceFormat { get; } = sourceFormat;

        public string TargetExtension { get; } = targetExtension;

        public bool IsAvailable => true;

        public string AvailabilityMessage => "available for tests";

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.Succeeded,
                "converted by test adapter"));
        }
    }
}
