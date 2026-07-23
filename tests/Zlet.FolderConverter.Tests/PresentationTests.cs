using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class PresentationTests : IDisposable
{
    private readonly string _rootPath;

    public PresentationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "zlet-folder-converter-presentation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Theory]
    [InlineData(OperationStatus.Unsupported, "Не поддерживается")]
    [InlineData(OperationStatus.Conflict, "Конфликт")]
    [InlineData(OperationStatus.Ready, "Готово к обработке")]
    [InlineData(OperationStatus.Failed, "Ошибка")]
    [InlineData(OperationStatus.Succeeded, "Преобразовано")]
    [InlineData(OperationStatus.Skipped, "Пропущено")]
    public void OperationRowViewModel_localizes_statuses(
        OperationStatus status,
        string expected)
    {
        Assert.Equal(expected, OperationRowViewModel.LocalizeStatus(status));
    }

    [Fact]
    public void OperationRowViewModel_uses_short_future_relative_path()
    {
        var operation = CreateOperation(
            Path.Combine("архив договоров", "old file.doc"),
            OperationStatus.Unsupported);

        var row = new OperationRowViewModel(operation);

        Assert.Equal(Path.Combine("_converted", "архив договоров", "old file.docx"), row.FutureRelativePath);
        Assert.Equal(operation.TargetPath, row.TargetPath);
    }

    [Fact]
    public void OperationRowViewModel_does_not_show_technical_message_in_main_row()
    {
        var operation = CreateOperation("source.doc", OperationStatus.Unsupported);

        var row = new OperationRowViewModel(operation);

        Assert.Equal("Конвертация недоступна.", row.Message);
        Assert.False(row.HasTechnicalMessage);
    }

    [Fact]
    public async Task MainWindowViewModel_does_not_add_unsupported_rows_to_error_log()
    {
        WriteSyntheticFile("source.doc");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Single(viewModel.Operations);
        Assert.Equal("Не поддерживается", viewModel.Operations[0].Status);
        Assert.Empty(viewModel.ErrorMessages);
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public async Task MainWindowViewModel_sets_empty_state_for_empty_folder()
    {
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Equal("JSON, DOC, XLS или PPT не найдены. Выберите другую папку или включите подпапки.", viewModel.EmptyStateMessage);
        Assert.Equal("Проверка завершена. Найдено файлов: 0.", viewModel.StateMessage);
    }

    [Fact]
    public async Task MainWindowViewModel_sets_completed_state_and_counts()
    {
        WriteSyntheticFile("one.doc");
        WriteSyntheticFile("two.xls");
        WriteSyntheticFile("three.ppt");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Equal("Проверка завершена. Найдено файлов: 3.", viewModel.StateMessage);
        Assert.Equal(1, viewModel.DocCount);
        Assert.Equal(1, viewModel.XlsCount);
        Assert.Equal(1, viewModel.PptCount);
        Assert.Equal("1 файл", viewModel.FormatCards[1].CountText);
    }

    [Fact]
    public async Task MainWindowViewModel_marks_conflict_state_without_error_log()
    {
        WriteSyntheticFile("source.doc");
        WriteSyntheticFile(Path.Combine("_converted", "source.docx"));
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        var row = Assert.Single(viewModel.Operations);
        Assert.Equal("Конфликт", row.Status);
        Assert.Equal("Файл или папка результата уже существует.", row.Message);
        Assert.Empty(viewModel.ErrorMessages);
    }

    [Fact]
    public async Task MainWindowViewModel_repeated_scan_clears_old_rows()
    {
        WriteSyntheticFile("source.doc");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();
        Assert.Single(viewModel.Operations);

        File.Delete(Path.Combine(_rootPath, "source.doc"));
        await viewModel.ScanAsync();

        Assert.Empty(viewModel.Operations);
        Assert.Equal("JSON, DOC, XLS или PPT не найдены. Выберите другую папку или включите подпапки.", viewModel.EmptyStateMessage);
    }

    [Fact]
    public async Task MainWindowViewModel_uses_original_root_for_scan_and_plan_when_selection_changes()
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
    public async Task MainWindowViewModel_converts_ready_json_and_disables_repeat_without_rescan()
    {
        var sourcePath = Path.Combine(_rootPath, "users.json");
        File.WriteAllText(sourcePath, """{"name":"Тест 😀"}""");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        Assert.True(viewModel.CanConvert);
        await viewModel.ConvertAsync();

        Assert.False(viewModel.CanConvert);
        Assert.Contains("Успешно: 1", viewModel.SummaryMessage);
        Assert.Equal("Преобразовано", Assert.Single(viewModel.Operations).Status);
        Assert.True(File.Exists(Path.Combine(_rootPath, "_converted", "users.txt")));
        Assert.Equal("""{"name":"Тест 😀"}""", File.ReadAllText(sourcePath));
    }

    [Fact]
    public async Task MainWindowViewModel_repeated_scan_after_conversion_shows_conflict()
    {
        File.WriteAllText(Path.Combine(_rootPath, "source.json"), "{}");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();
        await viewModel.ConvertAsync();
        await viewModel.ScanAsync();

        var row = Assert.Single(viewModel.Operations);
        Assert.Equal("Конфликт", row.Status);
        Assert.False(viewModel.CanConvert);
    }

    [Fact]
    public async Task MainWindowViewModel_output_format_change_invalidates_preview()
    {
        File.WriteAllText(Path.Combine(_rootPath, "source.json"), "{}");
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();
        Assert.True(viewModel.CanConvert);

        viewModel.SelectedOutputFormat = OutputFormat.Markdown;

        Assert.Empty(viewModel.Operations);
        Assert.False(viewModel.CanConvert);
        Assert.Equal("Требуется повторная проверка.", viewModel.StateMessage);
    }

    [Fact]
    public async Task MainWindowViewModel_folder_change_invalidates_preview()
    {
        File.WriteAllText(Path.Combine(_rootPath, "source.json"), "{}");
        var nextFolder = Path.Combine(_rootPath, "next");
        Directory.CreateDirectory(nextFolder);
        var viewModel = CreateViewModel();
        await viewModel.ScanAsync();

        viewModel.SelectedFolder = nextFolder;

        Assert.Empty(viewModel.Operations);
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
        return new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(new DefaultConversionAdapterResolver()))
        {
            SelectedFolder = _rootPath,
            IncludeSubfolders = true
        };
    }

    private PlannedOperation CreateOperation(
        string relativePath,
        OperationStatus status)
    {
        return new PlannedOperation(
            Path.Combine(_rootPath, relativePath),
            relativePath,
            DocumentFormat.Doc,
            ".docx",
            Path.Combine(_rootPath, "_converted", Path.ChangeExtension(relativePath, ".docx")),
            false,
            status,
            "DOC to DOCX is unsupported until an embedded converter passes license and synthetic validation.");
    }

    private void WriteSyntheticFile(string relativePath)
    {
        var path = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "synthetic test fixture");
    }

    private sealed class CallbackScanner(string root) : IFolderScanner
    {
        public Action? Callback { get; set; }
        public string? ReceivedRoot { get; private set; }

        public Task<ScanResult> ScanAsync(string rootPath, bool includeSubfolders, CancellationToken cancellationToken)
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
            OutputFormat outputFormat = OutputFormat.TXT)
        {
            ReceivedRoot = rootPath;
            return [];
        }
    }
}
