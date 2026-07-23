using System.Windows;
using Forms = System.Windows.Forms;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(new DefaultConversionAdapterResolver()),
            new ConversionProcessor(new DefaultConversionAdapterResolver()));
        DataContext = _viewModel;
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Выберите папку с JSON-файлами",
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrWhiteSpace(_viewModel.SelectedFolder))
        {
            dialog.SelectedPath = _viewModel.SelectedFolder;
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _viewModel.SelectedFolder = dialog.SelectedPath;
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ScanAsync();
        }
        catch (OperationCanceledException)
        {
            _viewModel.StateMessage = "Проверка отменена.";
        }
        catch (Exception exception)
        {
            _viewModel.AddError($"Не удалось проверить папку: {exception.Message}");
            _viewModel.StateMessage = "Проверка завершилась ошибкой.";
        }
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ConvertAsync();
        }
        catch (OperationCanceledException)
        {
            _viewModel.StateMessage = "Обработка отменена.";
        }
        catch (Exception exception)
        {
            _viewModel.AddError($"Не удалось завершить обработку: {exception.Message}");
            _viewModel.StateMessage = "Обработка завершилась ошибкой.";
        }
    }
}
