using System.Diagnostics;
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
        Width = Math.Min(1080, SystemParameters.WorkArea.Width * 0.94);
        Height = Math.Min(700, SystemParameters.WorkArea.Height * 0.92);
        var resolver = new DefaultConversionAdapterResolver();
        _viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            new ConversionProcessor(resolver));
        DataContext = _viewModel;
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Выберите папку с файлами для преобразования",
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
        catch (Exception)
        {
            _viewModel.AddError("Не удалось проверить папку. Повторите попытку или выберите другую папку.");
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
        catch (Exception)
        {
            _viewModel.AddError("Не удалось завершить обработку. Проверьте доступ к папке и повторите попытку.");
            _viewModel.StateMessage = "Обработка завершилась ошибкой.";
        }
    }

    private void OpenResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanOpenResult)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { _viewModel.ResultFolder },
                UseShellExecute = true
            });
        }
        catch
        {
            _viewModel.AddError("Не удалось открыть папку результата.");
        }
    }
}
