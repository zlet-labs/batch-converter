using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Zlet.FolderConverter.Tests;

public sealed class PresentationTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-folder-converter-presentation-tests",
        Guid.NewGuid().ToString("N"));

    public PresentationTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(OperationStatus.Ready, "Готово к преобразованию")]
    [InlineData(OperationStatus.Skipped, "Пропущен")]
    [InlineData(OperationStatus.Converting, "В процессе")]
    [InlineData(OperationStatus.Succeeded, "Преобразовано")]
    [InlineData(OperationStatus.Conflict, "Файл результата уже существует")]
    [InlineData(OperationStatus.Failed, "Ошибка")]
    [InlineData(OperationStatus.EngineUnavailable, "Требуется Microsoft Office")]
    [InlineData(OperationStatus.Unsupported, "Не поддерживается")]
    public void OperationRowViewModel_localizes_statuses(
        OperationStatus status,
        string expected)
    {
        Assert.Equal(expected, OperationRowViewModel.LocalizeStatus(status));
    }

    [Fact]
    public void OperationRowViewModel_shows_running_powerpoint_message()
    {
        const string message =
            "PowerPoint уже запущен. Закройте его и повторите преобразование.";
        var operation = new PlannedOperation(
            Path.Combine(_rootPath, "legacy.ppt"),
            "legacy.ppt",
            SourceFormat.Ppt,
            ConversionTarget.Pptx,
            ".pptx",
            Path.Combine(_rootPath, "_converted", "legacy.pptx"),
            true,
            OperationStatus.Ready,
            "Готово к преобразованию.",
            Path.Combine(_rootPath, "_converted"),
            _rootPath);
        var result = new ConversionResult(
            operation,
            OperationStatus.Failed,
            message,
            new ConversionDiagnostic("powerpoint_already_running"));

        var row = new OperationRowViewModel(operation, result);

        Assert.Equal(message, row.Status);
        Assert.Equal(message, row.Message);
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
        Assert.Equal(ConversionTarget.Copy, RuleFor(viewModel, SourceFormat.Docx).SelectedTarget.Target);
        Assert.Equal(ConversionTarget.Skip, RuleFor(viewModel, SourceFormat.Pdf).SelectedTarget.Target);
        Assert.Equal(2, viewModel.ReadyCount);
        Assert.Equal(1, viewModel.SkippedCount);
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

        var rule = Assert.Single(viewModel.FormatRules);
        Assert.Equal(SourceFormat.Unknown, rule.SourceFormat);
        Assert.Equal("CUSTOM: 1", rule.ExtensionBreakdown);
        Assert.True(rule.HasExtensionBreakdown);
        Assert.Equal(OperationStatus.Skipped, Assert.Single(viewModel.Operations).Operation.Status);
    }

    [Fact]
    public async Task Preview_summary_separates_unavailable_and_failed_operations()
    {
        var statuses = new[]
        {
            OperationStatus.Ready,
            OperationStatus.Converting,
            OperationStatus.Succeeded,
            OperationStatus.Skipped,
            OperationStatus.EngineUnavailable,
            OperationStatus.Unsupported,
            OperationStatus.Conflict,
            OperationStatus.Failed
        };
        var viewModel = CreateStatusViewModel(statuses);

        await viewModel.ScanAsync();

        Assert.Equal(8, viewModel.FoundCount);
        Assert.Equal(2, viewModel.ReadyCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.Equal(2, viewModel.UnavailableCount);
        Assert.Equal(1, viewModel.ConflictCount);
        Assert.Equal(1, viewModel.ErrorCount);
        Assert.True(viewModel.HasEngineUnavailable);
    }

    public static IEnumerable<object[]> PreviewFilterCases()
    {
        var all = new[]
        {
            OperationStatus.Ready,
            OperationStatus.Converting,
            OperationStatus.Succeeded,
            OperationStatus.Skipped,
            OperationStatus.EngineUnavailable,
            OperationStatus.Unsupported,
            OperationStatus.Conflict,
            OperationStatus.Failed
        };
        yield return [PreviewFilter.All, all];
        yield return
        [
            PreviewFilter.Convert,
            new[]
            {
                OperationStatus.Ready,
                OperationStatus.Converting,
                OperationStatus.Succeeded
            }
        ];
        yield return [PreviewFilter.Skip, new[] { OperationStatus.Skipped }];
        yield return
        [
            PreviewFilter.Unavailable,
            new[]
            {
                OperationStatus.EngineUnavailable,
                OperationStatus.Unsupported
            }
        ];
        yield return [PreviewFilter.Conflicts, new[] { OperationStatus.Conflict }];
        yield return [PreviewFilter.Errors, new[] { OperationStatus.Failed }];
    }

    [Theory]
    [MemberData(nameof(PreviewFilterCases))]
    public async Task Preview_filters_match_only_their_statuses(
        PreviewFilter filter,
        OperationStatus[] expected)
    {
        var allStatuses = (OperationStatus[])PreviewFilterCases().First()[1];
        var viewModel = CreateStatusViewModel(allStatuses);
        await viewModel.ScanAsync();

        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(option =>
            option.Filter == filter);

        Assert.Equal(expected, viewModel.VisibleOperations.Select(row =>
            row.Operation.Status));
    }

    [Theory]
    [InlineData(OperationStatus.EngineUnavailable, true)]
    [InlineData(OperationStatus.Unsupported, false)]
    [InlineData(OperationStatus.Ready, false)]
    public async Task Runtime_banner_is_visible_only_for_engine_unavailable(
        OperationStatus status,
        bool expected)
    {
        var viewModel = CreateStatusViewModel([status]);

        await viewModel.ScanAsync();

        Assert.Equal(expected, viewModel.HasEngineUnavailable);
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
        Assert.Equal(0, viewModel.FinalFailed);
        Assert.Equal(0, viewModel.FinalUnavailable);
        Assert.Equal(1, viewModel.FinalSkipped);
        Assert.Equal("Преобразовано", viewModel.Operations.Single(row =>
            row.Operation.SourceFormat == SourceFormat.Json).Status);
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "users.txt")));
        Assert.Equal(source, File.ReadAllText(Path.Combine(_rootPath, "users.json")));
    }

    [Fact]
    public async Task Mixed_batch_converts_json_without_counting_unavailable_doc_as_failure()
    {
        Write("data.json", """{"name":"Тест"}""");
        Write("legacy.doc", "synthetic");
        Write("manual.pdf", "%PDF-1.7");
        var resolver = new DefaultConversionAdapterResolver(
        [
            new JsonConversionAdapter(new OutputResultValidator()),
            new UnavailableAdapter(SourceFormat.Doc, ConversionTarget.Docx)
        ]);
        var viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            new ConversionProcessor(resolver))
        {
            SelectedFolder = _rootPath
        };

        await viewModel.ScanAsync();

        Assert.Equal(1, viewModel.ReadyCount);
        Assert.Equal(1, viewModel.UnavailableCount);
        Assert.Equal(1, viewModel.SkippedCount);
        Assert.Equal(0, viewModel.ErrorCount);
        Assert.Equal("Преобразовать 1 файл", viewModel.ConvertButtonText);

        await viewModel.ConvertAsync();

        Assert.Equal(1, viewModel.FinalSucceeded);
        Assert.Equal(1, viewModel.FinalUnavailable);
        Assert.Equal(1, viewModel.FinalSkipped);
        Assert.Equal(0, viewModel.FinalFailed);
        Assert.Equal(0, viewModel.ReadyCount);
        Assert.False(viewModel.CanConvert);
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "data.txt")));
        Assert.False(File.Exists(Path.Combine(_rootPath, "_converted", "legacy.docx")));
    }

    [Fact]
    public async Task Final_unavailable_combines_engine_unavailable_and_unsupported()
    {
        var operations = CreateStatusOperations(
        [
            OperationStatus.Ready,
            OperationStatus.EngineUnavailable,
            OperationStatus.Unsupported
        ]);
        var completed = operations.Select(operation =>
        {
            var status = operation.Status == OperationStatus.Ready
                ? OperationStatus.Succeeded
                : operation.Status;
            return new ConversionResult(operation, status, status.ToString());
        }).ToArray();
        var summary = new ConversionSummary(
            Succeeded: 1,
            Conflicts: 0,
            Failed: 0,
            Skipped: 0,
            EngineUnavailable: 1,
            Unsupported: 1,
            completed);
        var viewModel = CreateStatusViewModel(
            operations,
            new StaticProcessor(summary));

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        Assert.Equal(2, viewModel.FinalUnavailable);
        Assert.Equal(0, viewModel.FinalFailed);
    }

    [Theory]
    [InlineData(1, "Преобразовать 1 файл")]
    [InlineData(2, "Преобразовать 2 файла")]
    [InlineData(5, "Преобразовать 5 файлов")]
    [InlineData(11, "Преобразовать 11 файлов")]
    [InlineData(21, "Преобразовать 21 файл")]
    public async Task Convert_button_uses_russian_declension(
        int readyCount,
        string expected)
    {
        var viewModel = CreateStatusViewModel(
            Enumerable.Repeat(OperationStatus.Ready, readyCount).ToArray());

        await viewModel.ScanAsync();

        Assert.Equal(expected, viewModel.ConvertButtonText);
    }

    [Fact]
    public void Selected_folder_display_keeps_short_path_and_trims_long_path_from_left()
    {
        const string shortPath = @"C:\Проекты\Тест";
        const string longPath =
            @"C:\Очень длинная родительская папка\Ещё один каталог\PROJECT\Поддержка кастомизации текстов";

        Assert.Equal(shortPath, PathDisplayFormatter.Format(shortPath));
        var display = PathDisplayFormatter.Format(longPath);
        Assert.StartsWith("…\\", display);
        Assert.EndsWith(@"PROJECT\Поддержка кастомизации текстов", display);
        Assert.Contains("Поддержка кастомизации текстов", display);
    }

    [Fact]
    public void Selected_folder_display_preserves_unicode_and_handles_roots()
    {
        var unicodePath =
            @"C:\parent folder with a long name\ещё одна папка\Проект Ω 😀\Финальная папка";

        var exception = Record.Exception(() =>
        {
            Assert.Equal(@"C:\", PathDisplayFormatter.Format(@"C:\"));
            Assert.Equal(@"\\server\share", PathDisplayFormatter.Format(@"\\server\share"));
            Assert.Contains("Финальная папка", PathDisplayFormatter.Format(unicodePath));
            Assert.Contains("Ω", PathDisplayFormatter.Format(unicodePath));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Selected_folder_display_has_clear_empty_placeholder()
    {
        Assert.Equal(
            PathDisplayFormatter.EmptyPathPlaceholder,
            PathDisplayFormatter.Format(string.Empty));
    }

    [Fact]
    public void Other_extension_breakdown_groups_case_insensitively_and_sorts()
    {
        var files = new[]
        {
            Scanned("one.PDF"),
            Scanned("two.pdf"),
            Scanned("image.PNG"),
            Scanned("nested/second.png"),
            Scanned("readme.TXT"),
            Scanned("LICENSE")
        };

        Assert.Equal(
            "PDF: 2 · PNG: 2 · TXT: 1 · Без расширения: 1",
            ExtensionBreakdownFormatter.Format(files));
    }

    [Fact]
    public void Other_extension_breakdown_does_not_expose_names_or_paths()
    {
        var file = new ScannedFile(
            Path.Combine(_rootPath, "secret-client-name.PDF"),
            Path.Combine("private-folder", "secret-client-name.PDF"),
            SourceFormat.Unknown);

        var breakdown = ExtensionBreakdownFormatter.Format([file]);

        Assert.Equal("PDF: 1", breakdown);
        Assert.DoesNotContain("secret", breakdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", breakdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_rootPath, breakdown, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task Scan_selects_only_ready_operations_by_default()
    {
        var viewModel = CreateStatusViewModel(
            [OperationStatus.Ready, OperationStatus.Skipped, OperationStatus.Conflict,
                OperationStatus.EngineUnavailable]);

        await viewModel.ScanAsync();

        Assert.True(viewModel.Operations[0].IsSelected);
        Assert.All(viewModel.Operations.Skip(1), row => Assert.False(row.IsSelected));
        Assert.Equal(1, viewModel.SelectedReadyCount);
    }

    [Fact]
    public async Task Selection_commands_affect_all_ready_rows_and_filters_preserve_selection()
    {
        var viewModel = CreateStatusViewModel(
            [OperationStatus.Ready, OperationStatus.Ready, OperationStatus.Skipped]);
        await viewModel.ScanAsync();

        viewModel.ClearSelection();
        Assert.Equal("Выберите файлы", viewModel.ConvertButtonText);
        Assert.False(viewModel.CanConvert);

        viewModel.Operations[0].IsSelected = true;
        viewModel.SelectedPreviewFilter = viewModel.PreviewFilters.Single(option =>
            option.Filter == PreviewFilter.Skip);
        Assert.True(viewModel.Operations[0].IsSelected);

        viewModel.InvertSelection();
        Assert.False(viewModel.Operations[0].IsSelected);
        Assert.True(viewModel.Operations[1].IsSelected);

        viewModel.SelectAll();
        Assert.Equal(2, viewModel.SelectedReadyCount);
    }

    [Fact]
    public async Task Convert_passes_only_selected_ready_operations_to_processor()
    {
        var operations = CreateStatusOperations(
            [OperationStatus.Ready, OperationStatus.Ready, OperationStatus.Skipped]);
        var processor = new RecordingProcessor();
        var viewModel = CreateStatusViewModel(operations, processor);
        await viewModel.ScanAsync();
        viewModel.Operations[1].IsSelected = false;

        await viewModel.ConvertAsync();

        Assert.Single(processor.Received);
        Assert.Equal(operations[0].SourcePath, processor.Received[0].SourcePath);
        Assert.Equal(1, viewModel.FinalNotSelected);
        Assert.Equal("Не выбрано", viewModel.Operations[1].Status);
    }

    [Fact]
    public async Task Quoted_source_path_is_trimmed_and_scanned()
    {
        Write("source.json", "{}");
        var viewModel = CreateViewModel();
        viewModel.SelectedFolder = $"  \"{_rootPath}\"  ";

        await viewModel.ScanAsync();

        Assert.Equal(_rootPath, viewModel.SelectedFolder);
        Assert.False(viewModel.HasSourcePathError);
        Assert.Single(viewModel.Operations);
    }

    [Fact]
    public async Task Invalid_manual_source_path_shows_inline_error_and_does_not_scan()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedFolder = Path.Combine(_rootPath, "missing");

        await viewModel.ScanAsync();

        Assert.True(viewModel.HasSourcePathError);
        Assert.False(viewModel.CanScan);
        Assert.Empty(viewModel.Operations);
    }

    [Theory]
    [InlineData(@"\\server\share\folder", @"\\server\share\folder")]
    [InlineData("  \"\\\\server\\share\\папка\"  ", @"\\server\share\папка")]
    public void Source_path_normalization_preserves_unc_paths(string input, string expected)
    {
        Assert.Equal(expected, MainWindowViewModel.NormalizePathInput(input));
    }

    [Fact]
    public void Output_defaults_manual_edit_and_reset_are_mode_specific()
    {
        var viewModel = CreateViewModel();
        var manualFolder = Path.Combine(_rootPath, "custom-results");
        viewModel.OutputPath = manualFolder;

        viewModel.SelectedOutputMode = OutputMode.Zip;
        Assert.EndsWith("ZletBatchConverter-v0.0.0-results.zip", viewModel.OutputPath);
        var manualZip = Path.Combine(_rootPath, "manual.zip");
        viewModel.OutputPath = manualZip;

        viewModel.SelectedOutputMode = OutputMode.Folder;
        Assert.Equal(manualFolder, viewModel.OutputPath);
        viewModel.ResetOutputPath();
        Assert.Equal(Path.Combine(_rootPath, "_converted"), viewModel.OutputPath);

        viewModel.SelectedOutputMode = OutputMode.Zip;
        Assert.Equal(manualZip, viewModel.OutputPath);
    }

    [Fact]
    public async Task Partial_json_batch_creates_zip_with_only_success_and_preserves_sources()
    {
        var stagingParent = Path.Combine(
            Path.GetTempPath(), "ZletBatchConverter", "result-staging");
        var stagingBefore = ExistingDirectories(stagingParent);
        var validPath = Write(Path.Combine("nested", "valid.json"), "{\"value\":1}");
        var invalidPath = Write("invalid.json", "{invalid");
        var validHash = Hash(validPath);
        var invalidHash = Hash(invalidPath);
        var zipPath = Path.Combine(_rootPath, "result.zip");
        var viewModel = CreateViewModel();
        viewModel.SelectedOutputMode = OutputMode.Zip;
        viewModel.OutputPath = zipPath;

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();

        Assert.True(File.Exists(zipPath));
        using var archive = ZipFile.OpenRead(zipPath);
        Assert.Equal("nested/valid.txt", Assert.Single(archive.Entries).FullName);
        Assert.Equal(1, viewModel.FinalSucceeded);
        Assert.Equal(1, viewModel.FinalFailed);
        Assert.Equal(validHash, Hash(validPath));
        Assert.Equal(invalidHash, Hash(invalidPath));
        Assert.Equal(stagingBefore, ExistingDirectories(stagingParent));
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

    private MainWindowViewModel CreateStatusViewModel(OperationStatus[] statuses)
    {
        var operations = CreateStatusOperations(statuses);
        return CreateStatusViewModel(operations);
    }

    private PlannedOperation[] CreateStatusOperations(OperationStatus[] statuses)
    {
        return statuses.Select((status, index) =>
        {
            var relativePath = $"status-{index}.custom";
            return new PlannedOperation(
                Path.Combine(_rootPath, relativePath),
                relativePath,
                SourceFormat.Unknown,
                ConversionTarget.Skip,
                string.Empty,
                string.Empty,
                false,
                status,
                status.ToString());
        }).ToArray();
    }

    private MainWindowViewModel CreateStatusViewModel(
        PlannedOperation[] operations,
        IConversionProcessor? processor = null)
    {
        var scan = new ScanResult(
            _rootPath,
            operations.Select(operation => new ScannedFile(
                operation.SourcePath,
                operation.RelativePath,
                operation.SourceFormat)).ToArray(),
            []);

        return new MainWindowViewModel(
            new StaticScanner(scan),
            new StaticPlanner(operations),
            processor)
        {
            SelectedFolder = _rootPath
        };
    }

    private ScannedFile Scanned(string relativePath) =>
        new(
            Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            relativePath,
            SourceFormat.Unknown);

    private static RuleRowViewModel RuleFor(
        MainWindowViewModel viewModel,
        SourceFormat source) =>
        viewModel.FormatRules.Single(rule => rule.SourceFormat == source);

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string[] ExistingDirectories(string path) =>
        Directory.Exists(path)
            ? Directory.EnumerateDirectories(path)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

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

    private sealed class StaticScanner(ScanResult result) : IFolderScanner
    {
        public Task<ScanResult> ScanAsync(
            string rootPath,
            bool includeSubfolders,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class StaticPlanner(
        IReadOnlyList<PlannedOperation> operations) : IConversionPlanner
    {
        public IReadOnlyList<PlannedOperation> CreatePlan(
            ScanResult scanResult,
            string rootPath,
            RuleSet ruleSet) =>
            operations;
    }

    private sealed class UnavailableAdapter(
        SourceFormat source,
        ConversionTarget target) : IConversionAdapter
    {
        public bool IsAvailable => false;
        public string AvailabilityMessage => "unavailable";

        public bool CanConvert(
            SourceFormat sourceFormat,
            ConversionTarget conversionTarget) =>
            sourceFormat == source && conversionTarget == target;

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unavailable adapter must not run.");
    }

    private sealed class StaticProcessor(
        ConversionSummary summary) : IConversionProcessor
    {
        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(summary);
    }

    private sealed class RecordingProcessor : IConversionProcessor
    {
        public IReadOnlyList<PlannedOperation> Received { get; private set; } = [];

        public Task<ConversionSummary> ProcessAsync(
            IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            Received = operations;
            var results = operations.Select(operation =>
                new ConversionResult(operation, OperationStatus.Succeeded, "ok")).ToArray();
            return Task.FromResult(new ConversionSummary(
                results.Length, 0, 0, 0, 0, 0, results));
        }
    }
}
