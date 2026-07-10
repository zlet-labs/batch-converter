using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFolderScanner _folderScanner;
    private readonly IConversionPlanner _conversionPlanner;
    private string _selectedFolder = string.Empty;
    private bool _includeSubfolders = true;
    private bool _isScanning;
    private int _docCount;
    private int _xlsCount;
    private int _pptCount;
    private string _stateMessage = "Выберите папку для проверки.";
    private string _emptyStateMessage = "Выберите папку со старыми файлами DOC, XLS или PPT.";

    public MainWindowViewModel(
        IFolderScanner folderScanner,
        IConversionPlanner conversionPlanner)
    {
        _folderScanner = folderScanner;
        _conversionPlanner = conversionPlanner;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OperationRowViewModel> Operations { get; } = [];

    public ObservableCollection<string> ErrorMessages { get; } = [];

    public ObservableCollection<FormatCardViewModel> FormatCards { get; } =
    [
        new("DOC"),
        new("XLS"),
        new("PPT")
    ];

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set => SetProperty(ref _includeSubfolders, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    public bool CanScan => !IsScanning && Directory.Exists(SelectedFolder);

    public int DocCount
    {
        get => _docCount;
        private set
        {
            if (SetProperty(ref _docCount, value))
            {
                UpdateFormatCards();
            }
        }
    }

    public int XlsCount
    {
        get => _xlsCount;
        private set
        {
            if (SetProperty(ref _xlsCount, value))
            {
                UpdateFormatCards();
            }
        }
    }

    public int PptCount
    {
        get => _pptCount;
        private set
        {
            if (SetProperty(ref _pptCount, value))
            {
                UpdateFormatCards();
            }
        }
    }

    public string StateMessage
    {
        get => _stateMessage;
        set => SetProperty(ref _stateMessage, value);
    }

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set => SetProperty(ref _emptyStateMessage, value);
    }

    public bool HasErrors => ErrorMessages.Count > 0;

    public string ErrorHeader => HasErrors
        ? $"Ошибки проверки: {ErrorMessages.Count}"
        : string.Empty;

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(SelectedFolder))
        {
            StateMessage = "Выбранная папка недоступна.";
            return;
        }

        IsScanning = true;
        StateMessage = "Проверяем папку...";
        EmptyStateMessage = "Проверяем папку...";
        Operations.Clear();
        ErrorMessages.Clear();
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorHeader));

        try
        {
            var scanResult = await _folderScanner.ScanAsync(
                SelectedFolder,
                IncludeSubfolders,
                cancellationToken);

            DocCount = scanResult.DocCount;
            XlsCount = scanResult.XlsCount;
            PptCount = scanResult.PptCount;

            foreach (var error in scanResult.Errors)
            {
                AddError($"Не удалось прочитать: {error.Path}. {error.Message}");
            }

            var plan = _conversionPlanner.CreatePlan(scanResult, SelectedFolder);
            foreach (var operation in plan)
            {
                Operations.Add(new OperationRowViewModel(operation));

                if (operation.Status is OperationStatus.Failed)
                {
                    AddError($"{operation.RelativePath}: {operation.Message}");
                }
            }

            EmptyStateMessage = Operations.Count == 0
                ? "Подходящие файлы не найдены."
                : string.Empty;
            StateMessage = HasErrors
                ? "Не удалось прочитать часть папок. Подробности показаны ниже."
                : $"Проверка завершена. Найдено файлов: {Operations.Count}.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void AddError(string message)
    {
        ErrorMessages.Add(message);
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorHeader));
    }

    private void UpdateFormatCards()
    {
        FormatCards[0].Count = DocCount;
        FormatCards[1].Count = XlsCount;
        FormatCards[2].Count = PptCount;
        OnPropertyChanged(nameof(FormatCards));
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
