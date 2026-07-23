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
    private readonly IConversionProcessor _conversionProcessor;
    private string _selectedFolder = string.Empty;
    private bool _includeSubfolders = true;
    private bool _isScanning;
    private bool _isConverting;
    private OutputFormat _selectedOutputFormat = OutputFormat.TXT;
    private string _stateMessage = "Выберите папку с JSON-файлами.";
    private string _emptyStateMessage = "Выберите папку, затем нажмите «Проверить файлы».";
    private string _summaryMessage = string.Empty;

    public MainWindowViewModel(
        IFolderScanner folderScanner,
        IConversionPlanner conversionPlanner,
        IConversionProcessor? conversionProcessor = null)
    {
        _folderScanner = folderScanner;
        _conversionPlanner = conversionPlanner;
        _conversionProcessor = conversionProcessor
            ?? new ConversionProcessor(new DefaultConversionAdapterResolver());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<OperationRowViewModel> Operations { get; } = [];
    public ObservableCollection<string> ErrorMessages { get; } = [];
    public ObservableCollection<FormatCardViewModel> FormatCards { get; } =
    [
        new("JSON", true),
        new("DOC"),
        new("XLS"),
        new("PPT")
    ];
    public IReadOnlyList<OutputFormat> OutputFormats { get; } = Enum.GetValues<OutputFormat>();

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                InvalidatePreview();
                NotifyAvailability();
            }
        }
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (SetProperty(ref _includeSubfolders, value))
            {
                InvalidatePreview();
            }
        }
    }

    public OutputFormat SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set
        {
            if (SetProperty(ref _selectedOutputFormat, value))
            {
                InvalidatePreview();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                NotifyAvailability();
            }
        }
    }

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            if (SetProperty(ref _isConverting, value))
            {
                NotifyAvailability();
            }
        }
    }

    public bool IsBusy => IsScanning || IsConverting;
    public bool CanScan => !IsBusy && Directory.Exists(SelectedFolder);
    public bool CanConvert => !IsBusy && Operations.Any(row => row.Operation.Status == OperationStatus.Ready);
    public bool CanChangeSettings => !IsBusy;
    public int JsonCount { get; private set; }
    public int DocCount { get; private set; }
    public int XlsCount { get; private set; }
    public int PptCount { get; private set; }

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

    public string SummaryMessage
    {
        get => _summaryMessage;
        private set
        {
            if (SetProperty(ref _summaryMessage, value))
            {
                OnPropertyChanged(nameof(HasSummary));
            }
        }
    }

    public bool HasSummary => !string.IsNullOrWhiteSpace(SummaryMessage);
    public bool HasErrors => ErrorMessages.Count > 0;
    public string ErrorHeader => HasErrors ? $"Ошибки: {ErrorMessages.Count}" : string.Empty;

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        var selectedFolder = SelectedFolder;
        var includeSubfolders = IncludeSubfolders;
        var outputFormat = SelectedOutputFormat;
        if (!Directory.Exists(selectedFolder))
        {
            StateMessage = "Выбранная папка недоступна.";
            return;
        }

        IsScanning = true;
        StateMessage = "Проверяем папку...";
        EmptyStateMessage = "Проверяем папку...";
        SummaryMessage = string.Empty;
        Operations.Clear();
        ErrorMessages.Clear();
        NotifyErrors();

        try
        {
            var scanResult = await _folderScanner.ScanAsync(selectedFolder, includeSubfolders, cancellationToken);
            JsonCount = scanResult.JsonCount;
            DocCount = scanResult.DocCount;
            XlsCount = scanResult.XlsCount;
            PptCount = scanResult.PptCount;
            UpdateFormatCards();

            foreach (var error in scanResult.Errors)
            {
                AddError($"Не удалось прочитать: {error.Path}. {error.Message}");
            }

            var plan = _conversionPlanner.CreatePlan(scanResult, selectedFolder, outputFormat);
            foreach (var operation in plan)
            {
                Operations.Add(new OperationRowViewModel(operation));
            }

            EmptyStateMessage = Operations.Count == 0
                ? "JSON, DOC, XLS или PPT не найдены. Выберите другую папку или включите подпапки."
                : string.Empty;
            StateMessage = $"Проверка завершена. Найдено файлов: {Operations.Count}.";
            OnPropertyChanged(nameof(CanConvert));
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task ConvertAsync(CancellationToken cancellationToken = default)
    {
        var selectedFolder = SelectedFolder;
        var operations = Operations.Select(row => row.Operation).ToArray();
        if (!operations.Any(operation => operation.Status == OperationStatus.Ready))
        {
            return;
        }

        IsConverting = true;
        StateMessage = "Преобразуем JSON-файлы...";
        SummaryMessage = string.Empty;
        try
        {
            var summary = await _conversionProcessor.ProcessAsync(operations, cancellationToken);
            Operations.Clear();
            foreach (var result in summary.Results)
            {
                var completedOperation = result.Operation with
                {
                    Status = result.Status,
                    Message = result.Message
                };
                Operations.Add(new OperationRowViewModel(
                    completedOperation,
                    result with { Operation = completedOperation }));
                if (result.Status == OperationStatus.Failed)
                {
                    AddError($"{result.Operation.RelativePath}: {result.Message}");
                }
            }

            SummaryMessage =
                $"Успешно: {summary.Succeeded}. Конфликтов: {summary.Conflicts}. Ошибок: {summary.Failed}. " +
                $"Не поддерживается: {summary.Unsupported}. Результат: {Path.Combine(selectedFolder, "_converted")}";
            StateMessage = "Обработка завершена.";
        }
        finally
        {
            IsConverting = false;
        }
    }

    public void AddError(string message)
    {
        ErrorMessages.Add(message);
        NotifyErrors();
    }

    private void UpdateFormatCards()
    {
        FormatCards[0].Count = JsonCount;
        FormatCards[1].Count = DocCount;
        FormatCards[2].Count = XlsCount;
        FormatCards[3].Count = PptCount;
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanChangeSettings));
    }

    private void NotifyErrors()
    {
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ErrorHeader));
    }

    private void InvalidatePreview()
    {
        if (IsBusy || Operations.Count == 0)
        {
            return;
        }

        Operations.Clear();
        SummaryMessage = string.Empty;
        EmptyStateMessage = "Настройки изменены. Нажмите «Проверить файлы», чтобы обновить preview.";
        StateMessage = "Требуется повторная проверка.";
        OnPropertyChanged(nameof(CanConvert));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
