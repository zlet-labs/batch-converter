using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class PresentationTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-folder-converter-presentation-tests",
        Guid.NewGuid().ToString("N"));

    public PresentationTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(OperationStatus.Ready, "Готов")]
    [InlineData(OperationStatus.Skipped, "Пропущен")]
    [InlineData(OperationStatus.Converting, "В процессе")]
    [InlineData(OperationStatus.Succeeded, "Успешно")]
    [InlineData(OperationStatus.Conflict, "Конфликт")]
    [InlineData(OperationStatus.Failed, "Ошибка")]
    [InlineData(OperationStatus.EngineUnavailable, "Движок недоступен")]
    [InlineData(OperationStatus.Unsupported, "Не поддерживается")]
    public void OperationRowViewModel_localizes_statuses(
        OperationStatus status,
        string expected)
    {
        Assert.Equal(expected, OperationRowViewModel.LocalizeStatus(status));
    }

    [Fact]
    public void OperationRowViewModel_shows_relative_paths_and_russian_action()
    {
        var relative = Path.Combine("архив договоров", "old file.doc");
        var operation = new PlannedOperation(
            Path.Combine(_rootPath, relative),
            relative,
            SourceFormat.Doc,
            ConversionTarget.Docx,
            ".docx",
            Path.Combine(_rootPath, "_converted", "архив договоров", "old file.docx"),
            true,
            OperationStatus.Ready,
            "ready",
            Path.Combine(_rootPath, "_converted"));

        var row = new OperationRowViewModel(operation);

        Assert.Equal(relative, row.FilePath);
        Assert.Equal(Path.Combine("архив договоров", "old file.docx"), row.ResultPath);
        Assert.Equal("DOC → DOCX", row.ActionLabel);
    }

    [Fact]
    public async Task MainWindowViewModel_builds_default_rule_rows_for_found_formats()
    {
        Write("one.json", "{}");
        Write("two.docx", "synthetic");
        Write("manual.pdf", "%PDF-1.7");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Equal(3, viewModel.FormatRules.Count);
        Assert.Equal(ConversionTarget.Txt, RuleFor(viewModel, SourceFormat.Json).SelectedTarget.Target);
        Assert.Equal(ConversionTarget.Skip, RuleFor(viewModel, SourceFormat.Docx).SelectedTarget.Target);
        Assert.Equal(ConversionTarget.Skip, RuleFor(viewModel, SourceFormat.Pdf).SelectedTarget.Target);
        Assert.Equal(1, viewModel.ReadyCount);
        Assert.Equal(2, viewModel.SkippedCount);
    }

    [Fact]
    public async Task Changing_rule_rebuilds_preview_immediately()
    {
        Write("source.json", "{}");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();
        var jsonRule = RuleFor(viewModel, SourceFormat.Json);
        var markdown = jsonRule.Targets.Single(option =>
            option.Target == ConversionTarget.Markdown);

        jsonRule.SelectedTarget = markdown;

        var operation = Assert.Single(viewModel.Operations).Operation;
        Assert.Equal(ConversionTarget.Markdown, operation.Target);
        Assert.EndsWith(".md", operation.TargetPath);
        Assert.Equal("Правило изменено. Preview обновлён.", viewModel.StateMessage);
    }

    [Fact]
    public async Task Unknown_format_is_visible_and_skipped()
    {
        Write("source.custom", "synthetic");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Equal(SourceFormat.Unknown, Assert.Single(viewModel.FormatRules).SourceFormat);
        Assert.Equal(OperationStatus.Skipped, Assert.Single(viewModel.Operations).Operation.Status);
    }

    [Fact]
    public async Task Preview_filter_shows_only_skipped_operations()
    {
        Write("source.json", "{}");
        Write("manual.pdf", "%PDF-1.7");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(option =>
            option.Filter == PreviewFilter.Skip);

        Assert.Equal(
            OperationStatus.Skipped,
            Assert.Single(viewModel.VisibleOperations).Operation.Status);
    }

    [Fact]
    public async Task Scan_captures_original_root_before_await()
    {
        var otherRoot = Path.Combine(_rootPath, "other");
        Directory.CreateDirectory(otherRoot);
        var scanner = new CallbackScanner(_rootPath);
        var planner = new RecordingPlanner();
        var viewModel = new MainWindowViewModel(scanner, planner)
        {
            SelectedFolder = _rootPath
        };
        scanner.Callback = () => viewModel.SelectedFolder = otherRoot;

        await viewModel.ScanAsync();

        Assert.Equal(_rootPath, scanner.ReceivedRoot);
        Assert.Equal(_rootPath, planner.ReceivedRoot);
    }

    [Fact]
    public async Task ConvertAsync_converts_ready_json_and_exposes_final_report()
    {
        const string source = """{"name":"Тест 😀"}""";
        Write("users.json", source);
        Write("manual.pdf", "%PDF-1.7");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        Assert.True(viewModel.HasFinalReport);
        Assert.Equal(1, viewModel.FinalSucceeded);
        Assert.Equal(1, viewModel.FinalSkipped);
        Assert.Equal("Успешно", viewModel.Operations.Single(row =>
            row.Operation.SourceFormat == SourceFormat.Json).Status);
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "users.txt")));
        Assert.Equal(source, File.ReadAllText(Path.Combine(_rootPath, "users.json")));
    }

    [Fact]
    public async Task Repeated_scan_after_conversion_reports_conflict()
    {
        Write("source.json", "{}");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();
        await viewModel.ScanAsync();

        Assert.Equal(OperationStatus.Conflict, Assert.Single(viewModel.Operations).Operation.Status);
        Assert.False(viewModel.CanConvert);
    }

    [Fact]
    public async Task Folder_change_invalidates_existing_preview()
    {
        Write("source.json", "{}");
        var nextFolder = Path.Combine(_rootPath, "next");
        Directory.CreateDirectory(nextFolder);
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        viewModel.SelectedFolder = nextFolder;

        Assert.Empty(viewModel.Operations);
        Assert.Empty(viewModel.FormatRules);
        Assert.False(viewModel.CanConvert);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel()
    {
        var resolver = new DefaultConversionAdapterResolver();
        return new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            new ConversionProcessor(resolver))
        {
            SelectedFolder = _rootPath,
            IncludeSubfolders = true
        };
    }

    private static RuleRowViewModel RuleFor(
        MainWindowViewModel viewModel,
        SourceFormat source) =>
        viewModel.FormatRules.Single(rule => rule.SourceFormat == source);

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private sealed class CallbackScanner(string root) : IFolderScanner
    {
        public Action? Callback { get; set; }
        public string? ReceivedRoot { get; private set; }

        public Task<ScanResult> ScanAsync(
            string rootPath,
            bool includeSubfolders,
            CancellationToken cancellationToken)
        {
            ReceivedRoot = rootPath;
            Callback?.Invoke();
            return Task.FromResult(new ScanResult(root, [], []));
        }
    }

    private sealed class RecordingPlanner : IConversionPlanner
    {
        public string? ReceivedRoot { get; private set; }

        public IReadOnlyList<PlannedOperation> CreatePlan(
            ScanResult scanResult,
            string rootPath,
            RuleSet ruleSet)
        {
            ReceivedRoot = rootPath;
            return [];
        }
    }
}
