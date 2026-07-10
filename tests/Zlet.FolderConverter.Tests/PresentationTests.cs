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
    [InlineData(OperationStatus.Unsupported, "Пока не поддерживается")]
    [InlineData(OperationStatus.Conflict, "Конфликт")]
    [InlineData(OperationStatus.Ready, "Готово")]
    [InlineData(OperationStatus.Failed, "Ошибка")]
    [InlineData(OperationStatus.Succeeded, "Готово")]
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

        Assert.Equal("Конвертация DOC появится позже.", row.Message);
        Assert.False(row.HasTechnicalMessage);
    }

    [Fact]
    public async Task MainWindowViewModel_does_not_add_unsupported_rows_to_error_log()
    {
        WriteSyntheticFile("source.doc");
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Single(viewModel.Operations);
        Assert.Equal("Пока не поддерживается", viewModel.Operations[0].Status);
        Assert.Empty(viewModel.ErrorMessages);
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public async Task MainWindowViewModel_sets_empty_state_for_empty_folder()
    {
        var viewModel = CreateViewModel();

        await viewModel.ScanAsync();

        Assert.Equal("Подходящие файлы не найдены.", viewModel.EmptyStateMessage);
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
        Assert.Equal("1 файл", viewModel.FormatCards[0].CountText);
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
        Assert.Equal("Файл результата уже существует.", row.Message);
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
        Assert.Equal("Подходящие файлы не найдены.", viewModel.EmptyStateMessage);
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
}
