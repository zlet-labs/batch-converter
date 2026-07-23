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
    private string _stateMessage = "Выберите папку и найдите файлы.";
    private string _emptyStateMessage = "Preview появится после сканирования папки.";
    private RuleSet _ruleSet = RuleSet.CreateDefault();
    private ScanResult? _lastScan;
    private string _scanRoot = string.Empty;
    private PreviewFilterOption _selectedPreviewFilter;
    private int _foundCount;
    private int _readyCount;
    private int _skippedCount;
    private int _unavailableCount;
    private int _conflictCount;
    private int _errorCount;
    private double _progressPercent;
    private string _currentFile = string.Empty;
    private bool _hasFinalReport;
    private int _finalSucceeded;
    private int _finalFailed;
    private int _finalConflicts;
    private int _finalUnavailable;
    private int _finalSkipped;
    private string _resultFolder = string.Empty;

    public MainWindowViewModel(
        IFolderScanner folderScanner,
        IConversionPlanner conversionPlanner,
        IConversionProcessor? conversionProcessor = null)
    {
        _folderScanner = folderScanner;
        _conversionPlanner = conversionPlanner;
        _conversionProcessor = conversionProcessor
            ?? new ConversionProcessor(new DefaultConversionAdapterResolver());
        PreviewFilters =
        [
            new(PreviewFilter.All, "Все"),
            new(PreviewFilter.Convert, "К преобразованию"),
            new(PreviewFilter.Skip, "Не трогаем"),
            new(PreviewFilter.Unavailable, "Недоступно"),
            new(PreviewFilter.Conflicts, "Конфликты"),
            new(PreviewFilter.Errors, "Ошибки")
        ];
        _selectedPreviewFilter = PreviewFilters[0];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RuleRowViewModel> FormatRules { get; } = [];
    public ObservableCollection<OperationRowViewModel> Operations { get; } = [];
    public ObservableCollection<string> ErrorMessages { get; } = [];
    public IReadOnlyList<PreviewFilterOption> PreviewFilters { get; }

    public IEnumerable<OperationRowViewModel> VisibleOperations =>
        Operations.Where(MatchesSelectedFilter).ToArray();

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                OnPropertyChanged(nameof(SelectedFolderDisplay));
                OnPropertyChanged(nameof(CanCopySelectedFolder));
                InvalidateScan("Папка изменена. Нажмите «Найти файлы».");
                NotifyAvailability();
            }
        }
    }

    public string SelectedFolderDisplay => PathDisplayFormatter.Format(SelectedFolder);
    public bool CanCopySelectedFolder => !string.IsNullOrWhiteSpace(SelectedFolder);

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (SetProperty(ref _includeSubfolders, value))
            {
                InvalidateScan("Настройка подпапок изменена. Нажмите «Найти файлы».");
            }
        }
    }

    public PreviewFilterOption SelectedPreviewFilter
    {
        get => _selectedPreviewFilter;
        set
        {
            if (value is not null && SetProperty(ref _selectedPreviewFilter, value))
            {
                OnPropertyChanged(nameof(VisibleOperations));
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
                OnPropertyChanged(nameof(ShowProgress));
            }
        }
    }

    public bool IsBusy => IsScanning || IsConverting;
    public bool CanScan => !IsBusy && Directory.Exists(SelectedFolder);
    public bool CanConvert => !IsBusy && ReadyCount > 0;
    public bool CanChangeSettings => !IsBusy;
    public bool HasRules => FormatRules.Count > 0;
    public bool HasPreview => Operations.Count > 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public bool HasEngineUnavailable => Operations.Any(
        row => row.Operation.Status == OperationStatus.EngineUnavailable);
    public bool ShowProgress => IsConverting;
    public bool CanOpenResult => Directory.Exists(ResultFolder);

    public int FoundCount
    {
        get => _foundCount;
        private set => SetProperty(ref _foundCount, value);
    }

    public int ReadyCount
    {
        get => _readyCount;
        private set
        {
            if (SetProperty(ref _readyCount, value))
            {
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(ConvertButtonText));
            }
        }
    }

    public int SkippedCount
    {
        get => _skippedCount;
        private set => SetProperty(ref _skippedCount, value);
    }

    public int UnavailableCount
    {
        get => _unavailableCount;
        private set => SetProperty(ref _unavailableCount, value);
    }

    public int ConflictCount
    {
        get => _conflictCount;
        private set => SetProperty(ref _conflictCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public string ConvertButtonText =>
        $"Преобразовать {ReadyCount} {GetRussianFileWord(ReadyCount)}";

    public static string GetRussianFileWord(int count)
    {
        var absolute = Math.Abs(count);
        var lastTwoDigits = absolute % 100;
        if (lastTwoDigits is >= 11 and <= 14)
        {
            return "файлов";
        }

        return (absolute % 10) switch
        {
            1 => "файл",
            2 or 3 or 4 => "файла",
            _ => "файлов"
        };
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

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    public bool HasFinalReport
    {
        get => _hasFinalReport;
        private set => SetProperty(ref _hasFinalReport, value);
    }

    public int FinalSucceeded
    {
        get => _finalSucceeded;
        private set => SetProperty(ref _finalSucceeded, value);
    }

    public int FinalFailed
    {
        get => _finalFailed;
        private set => SetProperty(ref _finalFailed, value);
    }

    public int FinalConflicts
    {
        get => _finalConflicts;
        private set => SetProperty(ref _finalConflicts, value);
    }

    public int FinalUnavailable
    {
        get => _finalUnavailable;
        private set => SetProperty(ref _finalUnavailable, value);
    }

    public int FinalSkipped
    {
        get => _finalSkipped;
        private set => SetProperty(ref _finalSkipped, value);
    }

    public string ResultFolder
    {
        get => _resultFolder;
        private set
        {
            if (SetProperty(ref _resultFolder, value))
            {
                OnPropertyChanged(nameof(CanOpenResult));
            }
        }
    }

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        var selectedFolder = SelectedFolder;
        var includeSubfolders = IncludeSubfolders;
        if (!Directory.Exists(selectedFolder))
        {
            StateMessage = "Выбранная папка недоступна.";
            return;
        }

        IsScanning = true;
        StateMessage = "Ищем файлы...";
        EmptyStateMessage = "Ищем файлы...";
        ClearScanState();

        try
        {
            var scanResult = await _folderScanner.ScanAsync(
                selectedFolder,
                includeSubfolders,
                cancellationToken);
            _lastScan = scanResult;
            _scanRoot = selectedFolder;
            _ruleSet = RuleSet.CreateDefault();
            FoundCount = scanResult.Files.Count;

            foreach (var error in scanResult.Errors)
            {
                AddError($"Не удалось прочитать папку: {Path.GetFileName(error.Path)}.");
            }

            foreach (var group in scanResult.Files
                         .GroupBy(file => file.Format)
                         .OrderBy(group => (int)group.Key))
            {
                var capability = FormatCapabilityCatalog.Get(group.Key);
                FormatRules.Add(new RuleRowViewModel(
                    capability,
                    group.Count(),
                    _ruleSet.GetRule(group.Key).Target,
                    ChangeRule,
                    group.Key == SourceFormat.Unknown
                        ? ExtensionBreakdownFormatter.Format(group)
                        : string.Empty));
            }

            RebuildPreview();
            EmptyStateMessage = Operations.Count == 0
                ? "Файлы не найдены. Выберите другую папку или включите подпапки."
                : string.Empty;
            StateMessage = $"Найдено файлов: {FoundCount}. Preview готов.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task ConvertAsync(CancellationToken cancellationToken = default)
    {
        var operations = Operations.Select(row => row.Operation).ToArray();
        if (!operations.Any(operation => operation.Status == OperationStatus.Ready))
        {
            return;
        }

        IsConverting = true;
        HasFinalReport = false;
        ProgressPercent = 0;
        CurrentFile = string.Empty;
        StateMessage = "Преобразуем файлы...";
        var progress = new InlineProgress<ConversionProgress>(UpdateProgress);

        try
        {
            var summary = await _conversionProcessor.ProcessAsync(
                operations,
                progress,
                cancellationToken);
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

            UpdatePreviewSummary();
            FinalSucceeded = summary.Succeeded;
            FinalFailed = summary.Failed;
            FinalConflicts = summary.Conflicts;
            FinalUnavailable = summary.EngineUnavailable + summary.Unsupported;
            FinalSkipped = summary.Skipped;
            ResultFolder = Path.Combine(_scanRoot, "_converted");
            HasFinalReport = true;
            StateMessage = "Пакетная обработка завершена.";
            OnPropertyChanged(nameof(VisibleOperations));
            OnPropertyChanged(nameof(CanOpenResult));
        }
        catch (OperationCanceledException)
        {
            RebuildPreview();
            StateMessage = "Обработка отменена. Preview обновлён.";
            throw;
        }
        finally
        {
            IsConverting = false;
            CurrentFile = string.Empty;
        }
    }

    public void AddError(string message)
    {
        ErrorMessages.Add(message);
        OnPropertyChanged(nameof(HasErrors));
    }

    private void ChangeRule(SourceFormat sourceFormat, ConversionTarget target)
    {
        if (IsBusy || _lastScan is null)
        {
            return;
        }

        _ruleSet = _ruleSet.WithRule(sourceFormat, target);
        HasFinalReport = false;
        RebuildPreview();
        StateMessage = "Правило изменено. Preview обновлён.";
    }

    private void RebuildPreview()
    {
        Operations.Clear();
        if (_lastScan is not null)
        {
            foreach (var operation in _conversionPlanner.CreatePlan(_lastScan, _scanRoot, _ruleSet))
            {
                Operations.Add(new OperationRowViewModel(operation));
            }
        }

        UpdatePreviewSummary();
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasEngineUnavailable));
        OnPropertyChanged(nameof(VisibleOperations));
    }

    private void UpdatePreviewSummary()
    {
        FoundCount = Operations.Count;
        ReadyCount = Operations.Count(row => row.Operation.Status is
            OperationStatus.Ready or OperationStatus.Converting);
        SkippedCount = Operations.Count(row => row.Operation.Status == OperationStatus.Skipped);
        UnavailableCount = Operations.Count(row => row.Operation.Status is
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported);
        ConflictCount = Operations.Count(row => row.Operation.Status == OperationStatus.Conflict);
        ErrorCount = Operations.Count(row => row.Operation.Status == OperationStatus.Failed);
        OnPropertyChanged(nameof(HasEngineUnavailable));
        NotifyAvailability();
    }

    private void UpdateProgress(ConversionProgress progress)
    {
        CurrentFile = progress.RelativePath;
        ProgressPercent = progress.Total == 0
            ? 0
            : Math.Clamp(progress.Completed * 100d / progress.Total, 0, 100);

        var index = -1;
        for (var position = 0; position < Operations.Count; position++)
        {
            var operation = Operations[position].Operation;
            if (operation.RelativePath == progress.RelativePath
                && operation.Status is OperationStatus.Ready or OperationStatus.Converting)
            {
                index = position;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        var currentOperation = Operations[index].Operation;
        if (progress.Status == OperationStatus.Converting)
        {
            var converting = currentOperation with
            {
                Status = OperationStatus.Converting,
                Message = "Преобразование..."
            };
            Operations[index] = new OperationRowViewModel(converting);
        }
        else if (progress.Result is not null)
        {
            var completed = progress.Result.Operation with
            {
                Status = progress.Result.Status,
                Message = progress.Result.Message
            };
            Operations[index] = new OperationRowViewModel(
                completed,
                progress.Result with { Operation = completed });
        }

        UpdatePreviewSummary();
        OnPropertyChanged(nameof(VisibleOperations));
    }

    private bool MatchesSelectedFilter(OperationRowViewModel row) =>
        SelectedPreviewFilter.Filter switch
        {
            PreviewFilter.All => true,
            PreviewFilter.Convert => row.Operation.Status is OperationStatus.Ready
                or OperationStatus.Converting
                or OperationStatus.Succeeded,
            PreviewFilter.Skip => row.Operation.Status == OperationStatus.Skipped,
            PreviewFilter.Unavailable => row.Operation.Status is
                OperationStatus.EngineUnavailable or OperationStatus.Unsupported,
            PreviewFilter.Conflicts => row.Operation.Status == OperationStatus.Conflict,
            PreviewFilter.Errors => row.Operation.Status == OperationStatus.Failed,
            _ => true
        };

    private void InvalidateScan(string message)
    {
        if (IsBusy || _lastScan is null)
        {
            return;
        }

        ClearScanState();
        _lastScan = null;
        _scanRoot = string.Empty;
        StateMessage = message;
        EmptyStateMessage = "Выполните сканирование, чтобы построить новый preview.";
    }

    private void ClearScanState()
    {
        FormatRules.Clear();
        Operations.Clear();
        ErrorMessages.Clear();
        FoundCount = 0;
        ReadyCount = 0;
        SkippedCount = 0;
        UnavailableCount = 0;
        ConflictCount = 0;
        ErrorCount = 0;
        HasFinalReport = false;
        FinalSucceeded = 0;
        FinalFailed = 0;
        FinalConflicts = 0;
        FinalUnavailable = 0;
        FinalSkipped = 0;
        ResultFolder = string.Empty;
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasEngineUnavailable));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(VisibleOperations));
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanChangeSettings));
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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
