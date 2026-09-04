using System.IO.Compression;
using System.Text;
using Zlet.FolderConverter.App;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;
using Zlet.FolderConverter.OfficeWorker;

namespace Zlet.FolderConverter.Tests;

public sealed class NotebookFormatsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "zl056-tests", Guid.NewGuid().ToString("N"));
    public NotebookFormatsTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(SourceFormat.Xls, ConversionTarget.Csv)]
    [InlineData(SourceFormat.Xls, ConversionTarget.Tsv)]
    [InlineData(SourceFormat.Xlsx, ConversionTarget.Csv)]
    [InlineData(SourceFormat.Xlsx, ConversionTarget.Tsv)]
    public void Discovery_and_planning_preserve_all_sheet_states(SourceFormat format, ConversionTarget target)
    {
        var file = Workbook(format);
        var inspector = new Inspector();
        var planner = new ConversionPlanner(new Resolver(), inspector);
        var scan = new ScanResult(_root, [file], []);
        var rules = RuleSet.CreateDefault().WithRule(format, target);
        var plan = planner.CreatePlan(scan, _root, Path.Combine(_root, "output"), rules);
        Assert.Equal(4, plan.Count);
        Assert.Equal(1, inspector.Calls);
        Assert.True(plan[0].DefaultSelected);
        Assert.False(plan[1].DefaultSelected);
        Assert.False(plan[2].DefaultSelected);
        Assert.Equal(WorksheetVisibility.VeryHidden, plan[2].WorksheetVisibility);
        Assert.Equal(OperationStatus.Skipped, plan[3].Status);
        Assert.True(plan[3].WorksheetIsEmpty);
        Assert.All(plan, operation => Assert.EndsWith(target.ToExtension(), operation.ResultRelativePath));
        Assert.Equal(plan.Select(p => p.TargetPath), planner.CreatePlan(scan, _root, Path.Combine(_root, "output"), rules).Select(p => p.TargetPath));
        Assert.Equal(1, inspector.Calls);
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".csv")]
    [InlineData(".tsv")]
    [InlineData(".epub")]
    [InlineData(".avif")]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    [InlineData(".heic")]
    [InlineData(".heif")]
    [InlineData(".ico")]
    [InlineData(".jp2")]
    [InlineData(".jpe")]
    [InlineData(".jpeg")]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".tif")]
    [InlineData(".tiff")]
    [InlineData(".webp")]
    public async Task Safe_copy_is_identical_and_conflict_protected_without_Office(string extension)
    {
        var relative = Path.Combine("nested", "original" + extension);
        var source = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nПривет\t😀\0content");
        File.WriteAllBytes(source, bytes);
        var format = DocumentFormatDetector.Detect(source);
        var adapter = new SafeFileCopyAdapter(new OutputResultValidator());
        var resolver = new DefaultConversionAdapterResolver([adapter]);
        var plan = new ConversionPlanner(resolver, new Inspector(false)).CreatePlan(
            new ScanResult(_root, [new(source, relative, format)], []), _root,
            Path.Combine(_root, "output"), RuleSet.CreateDefault());
        var operation = Assert.Single(plan);
        var result = await adapter.ConvertAsync(operation, CancellationToken.None);
        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.Equal(bytes, File.ReadAllBytes(operation.TargetPath));
        Assert.Equal(OperationStatus.Conflict, (await adapter.ConvertAsync(operation, CancellationToken.None)).Status);
        Assert.Equal(bytes, File.ReadAllBytes(operation.TargetPath));
    }

    [Fact]
    public void Tsv_transcoding_preserves_tabs_unicode_and_newlines_and_never_overwrites()
    {
        var source = Path.Combine(_root, "unicode.txt");
        var target = Path.Combine(_root, "result.tsv");
        const string text = "Имя\tValue\r\n😀\t42\r\n";
        File.WriteAllText(source, text, Encoding.Unicode);
        ExcelWorksheetAutomation.TranscodeTabSeparated(source, target);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(text), File.ReadAllBytes(target));
        Assert.Throws<IOException>(() => ExcelWorksheetAutomation.TranscodeTabSeparated(source, target));
    }

    [Fact]
    public async Task Worksheet_selection_results_counts_and_language_switch_remain_independent()
    {
        var operations = SheetPlan();
        var l = LocalizationService.CreateStandalone("en-US");
        var vm = ViewModel(operations, l);
        await vm.ScanAsync();
        Assert.Equal(1, vm.FoundCount);
        Assert.Equal(1, vm.SelectedReadyCount);
        vm.Operations[1].IsSelected = true;
        vm.OutputPath = Path.Combine(_root, "changed"); // rebuild selection using sheet identity
        Assert.True(vm.Operations[1].IsSelected);
        Assert.False(vm.Operations[2].IsSelected);
        await vm.ConvertAsync();
        Assert.Equal(2, vm.FinalConverted);
        Assert.Equal(1, vm.FoundCount);
        Assert.Equal(OperationStatus.Succeeded, vm.Operations[0].Operation.Status);
        Assert.Equal(OperationStatus.Succeeded, vm.Operations[1].Operation.Status);
        Assert.True(vm.Operations[2].IsNotSelected);
        Assert.Contains("Sheets found: 4", vm.WorksheetSummaryText);
        var rows = vm.Operations.ToArray();
        l.Apply("ru-RU");
        Assert.Equal(rows, vm.Operations);
        Assert.Contains("Листов найдено: 4", vm.WorksheetSummaryText);
    }

    [Fact]
    public async Task Folder_report_has_suffix_privacy_details_and_does_not_affect_counters()
    {
        var vm = ViewModel(SheetPlan(), LocalizationService.CreateStandalone("en-US"));
        await vm.ScanAsync();
        await vm.ConvertAsync();
        var now = DateTimeOffset.Now;
        await ConversionReportWriter.WriteAsync(vm, now, now);
        await ConversionReportWriter.WriteAsync(vm, now, now);
        var report = File.ReadAllText(Path.Combine(vm.OutputPath, "ZletConverter-report.txt"));
        Assert.True(File.Exists(Path.Combine(vm.OutputPath, "ZletConverter-report-2.txt")));
        Assert.StartsWith("Zlet Converter", report);
        Assert.Contains("Source files: 1", report);
        Assert.Contains("Sheets found: 4", report);
        Assert.Contains("book__Visible.csv", report);
        Assert.DoesNotContain(_root, report);
        Assert.Equal(1, vm.FinalConverted);
        var bad = vm.Operations[0].Operation with { RelativePath = @"C:\Users\private\secret.txt", ResultRelativePath = "../secret" };
        vm.Operations[0].CompleteExecution(new(bad, OperationStatus.Failed, "token=secret " + _root,
            new ConversionDiagnostic("secret:token", HResult: unchecked((int)0x80004005))), TimeProvider.System, 0);
        report = ConversionReportWriter.BuildReport(vm, now, now);
        Assert.DoesNotContain("secret", report);
        Assert.DoesNotContain(_root, report);
        Assert.Contains("80004005", report);
    }

    [Fact]
    public async Task Report_failure_is_persistent_and_localized_and_existing_zip_is_untouched()
    {
        var l = LocalizationService.CreateStandalone("en-US");
        var vm = ViewModel(SheetPlan(), l);
        vm.SelectedOutputMode = OutputMode.Zip;
        var zip = Path.Combine(_root, "existing.zip");
        vm.OutputPath = zip;
        File.WriteAllText(zip, "unrelated");
        await ConversionReportWriter.WriteAsync(vm, DateTimeOffset.Now, DateTimeOffset.Now);
        Assert.Equal("unrelated", File.ReadAllText(zip));
        Assert.Contains("Could not", vm.ReportStatusText);
        l.Apply("ru-RU");
        Assert.Contains("Не удалось", vm.ReportStatusText);
    }

    [Fact]
    public async Task Report_only_zip_has_root_entry()
    {
        var vm = ViewModel([], LocalizationService.CreateStandalone("en-US"));
        vm.SelectedOutputMode = OutputMode.Zip;
        vm.OutputPath = Path.Combine(_root, "report.zip");
        await ConversionReportWriter.WriteAsync(vm, DateTimeOffset.Now, DateTimeOffset.Now);
        using var archive = ZipFile.OpenRead(vm.OutputPath);
        Assert.Equal("ZletConverter-report.txt", Assert.Single(archive.Entries).FullName);
        Assert.Equal(0, vm.FinalSucceeded);
    }

    [Theory]
    [InlineData(SourceFormat.Xls)]
    [InlineData(SourceFormat.Xlsx)]
    public async Task Excel_unavailable_disables_exports_but_not_xlsx_copy(SourceFormat format)
    {
        var runner = new ForbiddenRunner();
        var detector = new NoOffice();
        var inspector = new MicrosoftExcelWorkbookInspector(detector, runner);
        Assert.False(inspector.IsAvailable);
        Assert.Equal("office_application_missing", (await inspector.InspectAsync("unused", default)).ErrorCode);
        var resolver = new DefaultConversionAdapterResolver(detector, runner);
        var file = Workbook(format);
        var plan = new ConversionPlanner(resolver, inspector).CreatePlan(new(_root, [file], []), _root,
            Path.Combine(_root, "output"), RuleSet.CreateDefault().WithRule(format, ConversionTarget.Csv));
        Assert.Equal(OperationStatus.EngineUnavailable, Assert.Single(plan).Status);
        Assert.True(resolver.Resolve(SourceFormat.Xlsx, ConversionTarget.Copy)!.IsAvailable);
    }

    [Fact]
    public async Task Failed_sheet_does_not_replace_successful_siblings()
    {
        var operations = SheetPlan();
        var resolver = new DefaultConversionAdapterResolver([new SheetFailureAdapter()]);
        var vm = new MainWindowViewModel(new Scanner(new(_root, [Workbook(SourceFormat.Xlsx)], [])),
            new Planner(operations), new ConversionProcessor(resolver), localization: LocalizationService.CreateStandalone("en-US"))
            { SelectedFolder = _root, OutputPath = Path.Combine(_root, "output") };
        await vm.ScanAsync();
        vm.SelectAll();
        await vm.ConvertAsync();
        Assert.Equal(2, vm.FinalConverted);
        Assert.Equal(1, vm.FinalFailed);
        Assert.Equal(OperationStatus.Succeeded, vm.Operations[0].Operation.Status);
        Assert.Equal(OperationStatus.Failed, vm.Operations[1].Operation.Status);
        Assert.Equal(OperationStatus.Succeeded, vm.Operations[2].Operation.Status);
        Assert.Contains("Completed with errors", File.ReadAllText(Path.Combine(vm.OutputPath, "ZletConverter-report.txt")));
    }

    [Fact]
    public void Workbooks_with_same_stem_have_deterministic_distinct_outputs()
    {
        var scan = new ScanResult(_root, [Workbook(SourceFormat.Xls), Workbook(SourceFormat.Xlsx)], []);
        var planner = new ConversionPlanner(new Resolver(), new Inspector());
        var rules = RuleSet.CreateDefault().WithRule(SourceFormat.Xls, ConversionTarget.Csv).WithRule(SourceFormat.Xlsx, ConversionTarget.Csv);
        var plan = planner.CreatePlan(scan, _root, Path.Combine(_root, "output"), rules);
        Assert.Equal(plan.Count, plan.Select(o => o.TargetPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(plan.Select(o => o.TargetPath), planner.CreatePlan(scan, _root, Path.Combine(_root, "output"), rules).Select(o => o.TargetPath));
    }

    private sealed class SheetFailureAdapter : IConversionAdapter
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "";
        public bool CanConvert(SourceFormat source, ConversionTarget target) => true;
        public Task<ConversionResult> ConvertAsync(PlannedOperation operation, CancellationToken token) =>
            Task.FromResult(new ConversionResult(operation, operation.WorksheetName == "Hidden" ? OperationStatus.Failed : OperationStatus.Succeeded,
                "", operation.WorksheetName == "Hidden" ? new ConversionDiagnostic("office_com_failure") : null));
    }

    [Theory]
    [InlineData(OutputMode.Folder)]
    [InlineData(OutputMode.Zip)]
    public async Task Stop_report_preserves_completed_sibling_and_unselected_sheet(OutputMode mode)
    {
        var operations = SheetPlan();
        var processor = new StopProcessor();
        var vm = new MainWindowViewModel(new Scanner(new(_root, [Workbook(SourceFormat.Xlsx)], [])),
            new Planner(operations), processor, localization: LocalizationService.CreateStandalone("en-US"))
            { SelectedFolder = _root, OutputPath = Path.Combine(_root, "output") };
        if (mode == OutputMode.Zip)
        {
            vm.SelectedOutputMode = mode;
            vm.OutputPath = Path.Combine(_root, "stopped.zip");
        }
        await vm.ScanAsync();
        vm.Operations[1].IsSelected = true;
        var task = vm.ConvertAsync();
        Assert.True(vm.StopConversion());
        await task;
        Assert.True(vm.WasStoppedByUser);
        Assert.Equal(1, vm.FinalConverted);
        Assert.Equal(OperationStatus.Succeeded, vm.Operations[0].Operation.Status);
        Assert.Equal(OperationStatus.Cancelled, vm.Operations[1].Operation.Status);
        Assert.True(vm.Operations[2].IsNotSelected);
        var report = ConversionReportWriter.BuildReport(vm, DateTimeOffset.Now, DateTimeOffset.Now);
        Assert.Contains("Batch stopped by user", report);
        Assert.Contains("Worksheets selected: 2", report);
        Assert.DoesNotContain(_root, report);
        Assert.Contains("saved", vm.ReportStatusText);
        if (mode == OutputMode.Zip)
        {
            await ConversionReportWriter.WriteAsync(vm, DateTimeOffset.Now, DateTimeOffset.Now);
            using var archive = ZipFile.OpenRead(vm.OutputPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "book__Visible.csv");
            Assert.Contains(archive.Entries, entry => entry.FullName == "ZletConverter-report.txt");
            Assert.Contains(archive.Entries, entry => entry.FullName == "ZletConverter-report-2.txt");
            Assert.Equal(1, vm.FinalConverted);
        }
    }

    [Fact]
    public async Task Folder_report_failure_is_visible()
    {
        var vm = ViewModel([], LocalizationService.CreateStandalone("en-US"));
        File.WriteAllText(vm.OutputPath, "existing file");
        await ConversionReportWriter.WriteAsync(vm, DateTimeOffset.Now, DateTimeOffset.Now);
        Assert.Contains("Could not", vm.ReportStatusText);
        Assert.Equal("existing file", File.ReadAllText(vm.OutputPath));
    }

    [Theory]
    [InlineData(SourceFormat.Xls, ConversionTarget.Csv)]
    [InlineData(SourceFormat.Xlsx, ConversionTarget.Csv)]
    [InlineData(SourceFormat.Xls, ConversionTarget.Tsv)]
    [InlineData(SourceFormat.Xlsx, ConversionTarget.Tsv)]
    public async Task Worksheet_worker_request_and_utf8_output_follow_safe_publication(SourceFormat format, ConversionTarget target)
    {
        var file = Workbook(format);
        var runner = new Utf8Runner();
        var adapter = new MicrosoftOfficeConversionAdapter(OfficeApplicationKind.Excel, new ExcelAvailable(), runner, new OutputResultValidator());
        var resolver = new DefaultConversionAdapterResolver([adapter]);
        var plan = new ConversionPlanner(resolver, new Inspector()).CreatePlan(new(_root, [file], []), _root,
            Path.Combine(_root, "output"), RuleSet.CreateDefault().WithRule(format, target));
        var before = File.ReadAllBytes(file.SourcePath);
        var result = await adapter.ConvertAsync(plan[0], CancellationToken.None);
        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.NotNull(runner.Request);
        Assert.Equal("Visible", runner.Request.WorksheetName);
        Assert.Equal(target, runner.Request.Target);
        var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(plan[0].TargetPath));
        Assert.Contains("Привет 😀", text);
        Assert.Contains(target == ConversionTarget.Csv ? "," : "\t", text);
        Assert.Equal(before, File.ReadAllBytes(file.SourcePath));
    }

    private sealed class ExcelAvailable : IMicrosoftOfficeCapabilityDetector
    {
        public IReadOnlyList<OfficeApplicationAvailability> Detect() =>
            Enum.GetValues<OfficeApplicationKind>().Select(application => new OfficeApplicationAvailability(application, application == OfficeApplicationKind.Excel)).ToArray();
    }
    private sealed class Utf8Runner : IMicrosoftOfficeWorkerRunner
    {
        public OfficeWorkerRequest? Request { get; private set; }
        public bool IsAvailable => true;
        public Task<OfficeWorkerExecutionResult> RunAsync(OfficeWorkerRequest request, CancellationToken token)
        {
            Request = request;
            File.WriteAllText(request.OutputPath, "Привет 😀" + (request.Target == ConversionTarget.Csv ? "," : "\t") + "42\r\n", new UTF8Encoding(false));
            return Task.FromResult(new OfficeWorkerExecutionResult(true));
        }
    }

    private sealed class StopProcessor : IConversionProcessor
    {
        public async Task<ConversionSummary> ProcessAsync(IReadOnlyList<PlannedOperation> operations,
            IProgress<ConversionProgress>? progress, CancellationToken token)
        {
            var first = operations[0];
            Directory.CreateDirectory(Path.GetDirectoryName(first.TargetPath)!);
            File.WriteAllText(first.TargetPath, "name,value\nТест,42", new UTF8Encoding(false));
            progress?.Report(new(0, operations.Count, first.RelativePath, OperationStatus.Converting, WorksheetName: first.WorksheetName));
            progress?.Report(new(1, operations.Count, first.RelativePath, OperationStatus.Succeeded, new(first, OperationStatus.Succeeded, "")));
            var second = operations[1];
            progress?.Report(new(1, operations.Count, second.RelativePath, OperationStatus.Converting, WorksheetName: second.WorksheetName));
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        }
    }
    private sealed class NoOffice : IMicrosoftOfficeCapabilityDetector
    {
        public IReadOnlyList<OfficeApplicationAvailability> Detect() =>
            Enum.GetValues<OfficeApplicationKind>().Select(application => new OfficeApplicationAvailability(application, false)).ToArray();
    }
    private sealed class ForbiddenRunner : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable => true;
        public Task<OfficeWorkerExecutionResult> RunAsync(OfficeWorkerRequest request, CancellationToken token) =>
            throw new InvalidOperationException("Office must not run.");
    }

    private ScannedFile Workbook(SourceFormat format)
    {
        var relative = "book." + format.ToString().ToLowerInvariant();
        var source = Path.Combine(_root, relative);
        File.WriteAllText(source, "synthetic workbook; inspector injected");
        return new(source, relative, format);
    }
    private PlannedOperation[] SheetPlan()
    {
        var file = Workbook(SourceFormat.Xlsx);
        return new ConversionPlanner(new Resolver(), new Inspector()).CreatePlan(
            new(_root, [file], []), _root, Path.Combine(_root, "output"),
            RuleSet.CreateDefault().WithRule(SourceFormat.Xlsx, ConversionTarget.Csv)).ToArray();
    }
    private MainWindowViewModel ViewModel(PlannedOperation[] operations, LocalizationService l) => new(
        new Scanner(new(_root, operations.Select(o => new ScannedFile(o.SourcePath, o.RelativePath, o.SourceFormat)).Distinct().ToArray(), [])),
        new Planner(operations), new ConversionProcessor(new Resolver()), localization: l)
        { SelectedFolder = _root, OutputPath = Path.Combine(_root, "output") };
    private sealed class Inspector(bool available = true) : IExcelWorkbookInspector
    {
        public int Calls { get; private set; }
        public bool IsAvailable => available;
        public Task<ExcelWorkbookInspectionResult> InspectAsync(string path, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(new ExcelWorkbookInspectionResult(true,
                [new("Visible", 1, WorksheetVisibility.Visible, false), new("Hidden", 2, WorksheetVisibility.Hidden, false),
                 new("VeryHidden", 3, WorksheetVisibility.VeryHidden, false), new("Empty", 4, WorksheetVisibility.Visible, true)]));
        }
    }
    private sealed class Resolver : IConversionAdapterResolver
    {
        public IConversionAdapter? Resolve(SourceFormat source, ConversionTarget target) => new Adapter();
    }
    private sealed class Adapter : IConversionAdapter
    {
        public bool IsAvailable => true;
        public string AvailabilityMessage => "";
        public bool CanConvert(SourceFormat source, ConversionTarget target) => true;
        public Task<ConversionResult> ConvertAsync(PlannedOperation operation, CancellationToken token) =>
            Task.FromResult(new ConversionResult(operation, OperationStatus.Succeeded, ""));
    }
    private sealed class Scanner(ScanResult scan) : IFolderScanner
    {
        public Task<ScanResult> ScanAsync(string root, bool nested, CancellationToken token) => Task.FromResult(scan);
    }
    private sealed class Planner(PlannedOperation[] operations) : IConversionPlanner
    {
        public IReadOnlyList<PlannedOperation> CreatePlan(ScanResult scan, string root, RuleSet rules) => operations;
        public IReadOnlyList<PlannedOperation> CreatePlan(ScanResult scan, string root, string output, RuleSet rules) =>
            operations.Select(operation => operation with
            {
                OutputRootPath = output,
                TargetPath = Path.Combine(output, operation.ResultRelativePath)
            }).ToArray();
    }
    public void Dispose() => Directory.Delete(_root, true);
}
