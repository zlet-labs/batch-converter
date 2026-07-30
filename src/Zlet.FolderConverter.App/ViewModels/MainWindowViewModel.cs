using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Zlet.FolderConverter.App;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFolderScanner _folderScanner;
    private readonly IConversionPlanner _conversionPlanner;
    private readonly IConversionProcessor _conversionProcessor;
    private readonly IReadOnlyList<OfficeApplicationAvailability> _officeAvailability;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _progressTimer;
    private string _selectedFolder = string.Empty;
    private string _sourcePathError = string.Empty;
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
    private int _selectedReadyCount;
    private int _skippedCount;
    private int _unavailableCount;
    private int _conflictCount;
    private int _errorCount;
    private double _progressPercent;
    private string _currentFile = string.Empty;
    private int _progressCompleted;
    private int _progressTotal;
    private long _conversionStartTimestamp;
    private string _elapsedTimeText = "Прошло: 00:00";
    private string _remainingTimeText = "Осталось: рассчитываем…";
    private string _finalDurationText = string.Empty;
    private bool _hasFinalReport;
    private int _finalSucceeded;
    private int _finalFailed;
    private int _finalConflicts;
    private int _finalUnavailable;
    private int _finalSkipped;
    private int _finalNotSelected;
    private string _resultFolder = string.Empty;
    private OutputMode _selectedOutputMode;
    private string _outputPath = string.Empty;
    private string _folderOutputPath = string.Empty;
    private string _zipOutputPath = string.Empty;
    private string _outputPathError = string.Empty;
    private bool _folderOutputEdited;
    private bool _zipOutputEdited;
    private bool _applyingOutputDefault;
    private readonly string _zipStagingRoot = Path.Combine(
        Path.GetTempPath(),
        "ZletBatchConverter",
        "result-staging",
        Guid.NewGuid().ToString("N"));

    public MainWindowViewModel(
        IFolderScanner folderScanner,
        IConversionPlanner conversionPlanner,
        IConversionProcessor? conversionProcessor = null,
        IMicrosoftOfficeCapabilityDetector? officeCapabilityDetector = null,
        TimeProvider? timeProvider = null)
    {
        _folderScanner = folderScanner;
        _conversionPlanner = conversionPlanner;
        _conversionProcessor = conversionProcessor
            ?? new ConversionProcessor(new DefaultConversionAdapterResolver());
        _officeAvailability = (officeCapabilityDetector
            ?? new MicrosoftOfficeCapabilityDetector()).Detect();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _progressTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _progressTimer.Tick += (_, _) => RefreshConversionTiming();
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
    public string WordOfficeStatus => GetOfficeStatus(OfficeApplicationKind.Word);
    public string ExcelOfficeStatus => GetOfficeStatus(OfficeApplicationKind.Excel);
    public string PowerPointOfficeStatus => GetOfficeStatus(OfficeApplicationKind.PowerPoint);

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
                UpdateSourcePathError();
                RefreshDefaultOutputPaths();
                InvalidateScan("Папка изменена. Нажмите «Найти файлы».");
                NotifyAvailability();
            }
        }
    }

    public string SelectedFolderDisplay => PathDisplayFormatter.Format(SelectedFolder);
    public bool CanCopySelectedFolder => !string.IsNullOrWhiteSpace(SelectedFolder);
    public string SourcePathError
    {
        get => _sourcePathError;
        private set
        {
            if (SetProperty(ref _sourcePathError, value))
            {
                OnPropertyChanged(nameof(HasSourcePathError));
            }
        }
    }
    public bool HasSourcePathError => !string.IsNullOrWhiteSpace(SourcePathError);

    public IReadOnlyList<OutputModeOption> OutputModes { get; } =
        [new(OutputMode.Folder, "Папка"), new(OutputMode.Zip, "ZIP-архив")];

    public OutputModeOption SelectedOutputModeOption
    {
        get => OutputModes.Single(option => option.Mode == SelectedOutputMode);
        set
        {
            if (value is not null)
            {
                SelectedOutputMode = value.Mode;
                OnPropertyChanged();
            }
        }
    }

    public OutputMode SelectedOutputMode
    {
        get => _selectedOutputMode;
        set
        {
            if (!SetProperty(ref _selectedOutputMode, value))
            {
                return;
            }

            ApplyCurrentModePath();
            HasFinalReport = false;
            if (_lastScan is not null)
            {
                RebuildPreview();
            }
            OnPropertyChanged(nameof(OutputModeLabel));
            OnPropertyChanged(nameof(OutputBrowseButtonText));
            OnPropertyChanged(nameof(ResultActionText));
            OnPropertyChanged(nameof(SelectedOutputModeOption));
        }
    }

    public string OutputModeLabel => SelectedOutputMode == OutputMode.Folder
        ? "Папка"
        : "ZIP-архив";

    public string OutputBrowseButtonText => SelectedOutputMode == OutputMode.Folder
        ? "Выбрать папку"
        : "Выбрать ZIP";

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (!SetProperty(ref _outputPath, value))
            {
                return;
            }

            if (SelectedOutputMode == OutputMode.Folder)
            {
                _folderOutputPath = value;
                if (!_applyingOutputDefault)
                {
                    _folderOutputEdited = true;
                }
            }
            else
            {
                _zipOutputPath = value;
                if (!_applyingOutputDefault)
                {
                    _zipOutputEdited = true;
                }
            }

            ValidateOutputPath();
            HasFinalReport = false;
            if (_lastScan is not null && SelectedOutputMode == OutputMode.Folder)
            {
                RebuildPreview();
            }
            NotifyAvailability();
        }
    }

    public string OutputPathError
    {
        get => _outputPathError;
        private set
        {
            if (SetProperty(ref _outputPathError, value))
            {
                OnPropertyChanged(nameof(HasOutputPathError));
            }
        }
    }

    public bool HasOutputPathError => !string.IsNullOrWhiteSpace(OutputPathError);

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
    public bool CanScan => !IsBusy && Directory.Exists(NormalizePathInput(SelectedFolder));
    public bool CanConvert => !IsBusy
                              && SelectedReadyCount > 0
                              && !HasOutputPathError
                              && !string.IsNullOrWhiteSpace(OutputPath);
    public bool CanChangeSettings => !IsBusy;
    public bool HasRules => FormatRules.Count > 0;
    public bool HasPreview => Operations.Count > 0;
    public bool HasErrors => ErrorMessages.Count > 0;
    public bool HasEngineUnavailable => Operations.Any(
        row => row.Operation.Status == OperationStatus.EngineUnavailable);
    public bool ShowProgress => IsConverting;
    public bool CanOpenResult => SelectedOutputMode == OutputMode.Folder
        ? Directory.Exists(ResultFolder)
        : File.Exists(ResultFolder);
    public string ResultActionText => SelectedOutputMode == OutputMode.Folder
        ? "Открыть папку результата"
        : "Показать ZIP в проводнике";

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

    public int SelectedReadyCount
    {
        get => _selectedReadyCount;
        private set
        {
            if (SetProperty(ref _selectedReadyCount, value))
            {
                OnPropertyChanged(nameof(CanConvert));
                OnPropertyChanged(nameof(ConvertButtonText));
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }
    }

    public int SelectableCount => Operations.Count(row => row.CanSelect);
    public string SelectionSummary => $"Выбрано: {SelectedReadyCount} из {SelectableCount}";

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

    public string ConvertButtonText => SelectedReadyCount == 0
        ? "Выберите файлы"
        : $"Преобразовать {SelectedReadyCount} {GetRussianFileWord(SelectedReadyCount)}";

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
        private set
        {
            if (SetProperty(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(ProgressPercentText));
            }
        }
    }

    public string ProgressPercentText => $"{ProgressPercent:0}%";
    public string ProgressCountText => $"{_progressCompleted} из {_progressTotal}";

    public string CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        private set => SetProperty(ref _elapsedTimeText, value);
    }

    public string RemainingTimeText
    {
        get => _remainingTimeText;
        private set => SetProperty(ref _remainingTimeText, value);
    }

    public string FinalDurationText
    {
        get => _finalDurationText;
        private set => SetProperty(ref _finalDurationText, value);
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

    public int FinalNotSelected
    {
        get => _finalNotSelected;
        private set => SetProperty(ref _finalNotSelected, value);
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
        var selectedFolder = NormalizePathInput(SelectedFolder);
        var includeSubfolders = IncludeSubfolders;
        if (!Directory.Exists(selectedFolder))
        {
            SourcePathError = "Папка не существует или недоступна.";
            StateMessage = "Выбранная папка недоступна.";
            return;
        }

        if (!string.Equals(SelectedFolder, selectedFolder, StringComparison.Ordinal))
        {
            _selectedFolder = selectedFolder;
            OnPropertyChanged(nameof(SelectedFolder));
            OnPropertyChanged(nameof(SelectedFolderDisplay));
            OnPropertyChanged(nameof(CanCopySelectedFolder));
        }
        SourcePathError = string.Empty;
        IsScanning = true;
        StateMessage = "Ищем файлы...";
        EmptyStateMessage = "Ищем файлы...";
        ClearScanState();

        try
        {
            var excludedDirectory = SelectedOutputMode == OutputMode.Folder
                ? NormalizePathInput(OutputPath)
                : null;
            var excludedFile = SelectedOutputMode == OutputMode.Zip
                ? NormalizePathInput(OutputPath)
                : null;
            var scanResult = await _folderScanner.ScanAsync(
                selectedFolder,
                includeSubfolders,
                excludedDirectory,
                excludedFile,
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
        var originalRows = Operations.ToArray();
        var selectedRows = originalRows
            .Where(row => row.CanSelect && row.IsSelected)
            .ToArray();
        var operations = selectedRows.Select(row => row.Operation).ToArray();
        if (operations.Length == 0)
        {
            return;
        }

        ValidateOutputPath();
        if (HasOutputPathError)
        {
            StateMessage = "Проверьте путь результата.";
            return;
        }

        if (SelectedOutputMode == OutputMode.Zip)
        {
            TryDeleteZipStaging();
        }

        IsConverting = true;
        HasFinalReport = false;
        _progressCompleted = 0;
        _progressTotal = operations.Length;
        OnPropertyChanged(nameof(ProgressCountText));
        ProgressPercent = 0;
        CurrentFile = string.Empty;
        FinalDurationText = string.Empty;
        _conversionStartTimestamp = _timeProvider.GetTimestamp();
        RefreshConversionTiming();
        _progressTimer.Start();
        StateMessage = "Преобразуем файлы...";
        var progress = new InlineProgress<ConversionProgress>(UpdateProgress);

        try
        {
            var summary = await _conversionProcessor.ProcessAsync(
                operations,
                progress,
                cancellationToken);
            if (SelectedOutputMode == OutputMode.Zip)
            {
                try
                {
                    var zipResult = await new ResultZipPublisher().PublishAsync(
                        _zipStagingRoot,
                        NormalizePathInput(OutputPath),
                        summary,
                        cancellationToken);
                    if (!zipResult.Created)
                    {
                        AddError(zipResult.ErrorCode == "no_successful_outputs"
                            ? "ZIP не создан: нет успешно преобразованных файлов."
                            : "Не удалось безопасно создать ZIP результата.");
                    }
                }
                catch (Exception exception) when (exception is IOException
                                                   or InvalidDataException
                                                   or UnauthorizedAccessException)
                {
                    AddError("Не удалось безопасно создать ZIP результата.");
                }
            }
            var resultsBySource = summary.Results.ToDictionary(
                result => result.Operation.SourcePath,
                StringComparer.OrdinalIgnoreCase);
            Operations.Clear();
            foreach (var row in originalRows)
            {
                if (!resultsBySource.TryGetValue(row.Operation.SourcePath, out var result))
                {
                    Operations.Add(new OperationRowViewModel(
                        row.Operation,
                        isSelected: false,
                        isNotSelected: row.Operation.Status == OperationStatus.Ready,
                        selectionChanged: SelectionChanged));
                    continue;
                }

                var completedOperation = result.Operation with
                {
                    Status = result.Status,
                    Message = result.Message
                };
                Operations.Add(new OperationRowViewModel(
                    completedOperation,
                    result with { Operation = completedOperation },
                    isSelected: false,
                    selectionChanged: SelectionChanged));
                if (result.Status == OperationStatus.Failed)
                {
                    AddError(FormatConversionError(result));
                }
            }

            UpdatePreviewSummary();
            FinalSucceeded = summary.Succeeded;
            FinalFailed = summary.Failed;
            var unprocessedRows = originalRows.Where(row =>
                !resultsBySource.ContainsKey(row.Operation.SourcePath)).ToArray();
            FinalConflicts = summary.Conflicts + unprocessedRows.Count(row =>
                row.Operation.Status == OperationStatus.Conflict);
            FinalUnavailable = summary.EngineUnavailable + summary.Unsupported
                               + unprocessedRows.Count(row => row.Operation.Status is
                                   OperationStatus.EngineUnavailable or OperationStatus.Unsupported);
            FinalSkipped = summary.Skipped + unprocessedRows.Count(row =>
                row.Operation.Status == OperationStatus.Skipped);
            FinalNotSelected = unprocessedRows.Count(row =>
                row.Operation.Status == OperationStatus.Ready && !row.IsSelected);
            _progressCompleted = operations.Length;
            _progressTotal = operations.Length;
            ProgressPercent = 100;
            OnPropertyChanged(nameof(ProgressCountText));
            RefreshConversionTiming();
            FinalDurationText = $"Время выполнения: {FormatDuration(GetConversionElapsed())}";
            ResultFolder = SelectedOutputMode == OutputMode.Folder
                ? NormalizePathInput(OutputPath)
                : File.Exists(NormalizePathInput(OutputPath))
                    ? NormalizePathInput(OutputPath)
                    : string.Empty;
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
            _progressTimer.Stop();
            if (SelectedOutputMode == OutputMode.Zip)
            {
                TryDeleteZipStaging();
            }
            IsConverting = false;
            CurrentFile = string.Empty;
        }
    }

    public void AddError(string message)
    {
        ErrorMessages.Add(message);
        OnPropertyChanged(nameof(HasErrors));
    }

    public void SelectAll()
    {
        foreach (var row in Operations.Where(row => row.CanSelect))
        {
            row.IsSelected = true;
        }
        SelectionChanged();
    }

    public void ClearSelection()
    {
        foreach (var row in Operations.Where(row => row.CanSelect))
        {
            row.IsSelected = false;
        }
        SelectionChanged();
    }

    public void InvertSelection()
    {
        foreach (var row in Operations.Where(row => row.CanSelect))
        {
            row.IsSelected = !row.IsSelected;
        }
        SelectionChanged();
    }

    public void ResetOutputPath()
    {
        if (SelectedOutputMode == OutputMode.Folder)
        {
            _folderOutputEdited = false;
        }
        else
        {
            _zipOutputEdited = false;
        }

        ApplyDefaultForCurrentMode();
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
        var previousSelection = Operations
            .Where(row => row.CanSelect)
            .ToDictionary(
                row => row.Operation.SourcePath,
                row => row.IsSelected,
                StringComparer.OrdinalIgnoreCase);
        Operations.Clear();
        if (_lastScan is not null)
        {
            var outputRoot = SelectedOutputMode == OutputMode.Folder
                ? NormalizePathInput(OutputPath)
                : _zipStagingRoot;
            foreach (var operation in _conversionPlanner.CreatePlan(
                         _lastScan,
                         _scanRoot,
                         outputRoot,
                         _ruleSet))
            {
                Operations.Add(new OperationRowViewModel(
                    operation,
                    isSelected: operation.Status == OperationStatus.Ready
                        && (!previousSelection.TryGetValue(operation.SourcePath, out var selected)
                            || selected),
                    selectionChanged: SelectionChanged));
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
        SelectedReadyCount = Operations.Count(row => row.CanSelect && row.IsSelected);
        SkippedCount = Operations.Count(row => row.Operation.Status == OperationStatus.Skipped);
        UnavailableCount = Operations.Count(row => row.Operation.Status is
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported);
        ConflictCount = Operations.Count(row => row.Operation.Status == OperationStatus.Conflict);
        ErrorCount = Operations.Count(row => row.Operation.Status == OperationStatus.Failed);
        OnPropertyChanged(nameof(SelectableCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(HasEngineUnavailable));
        NotifyAvailability();
    }

    private void UpdateProgress(ConversionProgress progress)
    {
        _progressCompleted = progress.Completed;
        _progressTotal = progress.Total;
        OnPropertyChanged(nameof(ProgressCountText));
        CurrentFile = progress.RelativePath;
        ProgressPercent = progress.Total == 0
            ? 0
            : Math.Clamp(progress.Completed * 100d / progress.Total, 0, 100);
        RefreshConversionTiming();

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
        SelectedReadyCount = 0;
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
        FinalNotSelected = 0;
        _progressCompleted = 0;
        _progressTotal = 0;
        ProgressPercent = 0;
        CurrentFile = string.Empty;
        ElapsedTimeText = "Прошло: 00:00";
        RemainingTimeText = "Осталось: рассчитываем…";
        FinalDurationText = string.Empty;
        ResultFolder = string.Empty;
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasEngineUnavailable));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(VisibleOperations));
        OnPropertyChanged(nameof(ProgressCountText));
    }

    private void RefreshConversionTiming()
    {
        if (!IsConverting)
        {
            return;
        }

        var elapsed = GetConversionElapsed();
        ElapsedTimeText = $"Прошло: {FormatDuration(elapsed)}";
        if (_progressTotal <= 0 || _progressCompleted <= 0)
        {
            RemainingTimeText = "Осталось: рассчитываем…";
            return;
        }

        if (_progressCompleted >= _progressTotal)
        {
            RemainingTimeText = "Осталось: 00:00";
            return;
        }

        var remaining = TimeSpan.FromTicks((long)Math.Max(
            0,
            elapsed.Ticks * (double)(_progressTotal - _progressCompleted)
            / _progressCompleted));
        RemainingTimeText = $"Осталось: ~{FormatDuration(remaining)}";
    }

    private TimeSpan GetConversionElapsed() =>
        _timeProvider.GetElapsedTime(_conversionStartTimestamp, _timeProvider.GetTimestamp());

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)Math.Floor(Math.Max(0, duration.TotalHours));
        return totalHours > 0
            ? $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string FormatConversionError(ConversionResult result)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Diagnostic?.ErrorCode))
        {
            details.Add($"код: {result.Diagnostic.ErrorCode}");
        }
        if (result.Diagnostic?.HResult is int hResult)
        {
            details.Add($"HRESULT 0x{unchecked((uint)hResult):X8}");
        }

        var diagnostic = details.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", details)})";
        return $"{result.Operation.RelativePath}: {result.Message}{diagnostic}";
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanChangeSettings));
    }

    private string GetOfficeStatus(OfficeApplicationKind application) =>
        _officeAvailability.Single(item => item.Application == application).StatusText;

    private void RefreshDefaultOutputPaths()
    {
        var source = NormalizePathInput(SelectedFolder);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (!_folderOutputEdited)
        {
            _folderOutputPath = Path.Combine(source, "_converted");
        }

        if (!_zipOutputEdited)
        {
            _zipOutputPath = Path.Combine(source, ProductIdentity.ResultZipFileName);
        }

        ApplyCurrentModePath();
    }

    private void ApplyDefaultForCurrentMode()
    {
        var source = NormalizePathInput(SelectedFolder);
        var value = SelectedOutputMode == OutputMode.Folder
            ? Path.Combine(source, "_converted")
            : Path.Combine(source, ProductIdentity.ResultZipFileName);
        if (SelectedOutputMode == OutputMode.Folder)
        {
            _folderOutputPath = value;
        }
        else
        {
            _zipOutputPath = value;
        }

        SetOutputPathWithoutMarkingEdited(value);
    }

    private void ApplyCurrentModePath()
    {
        var value = SelectedOutputMode == OutputMode.Folder
            ? _folderOutputPath
            : _zipOutputPath;
        if (string.IsNullOrWhiteSpace(value))
        {
            ApplyDefaultForCurrentMode();
            return;
        }

        SetOutputPathWithoutMarkingEdited(value);
    }

    private void SetOutputPathWithoutMarkingEdited(string value)
    {
        _applyingOutputDefault = true;
        try
        {
            OutputPath = value;
        }
        finally
        {
            _applyingOutputDefault = false;
        }
    }

    private void ValidateOutputPath()
    {
        var source = NormalizePathInput(SelectedFolder);
        var output = NormalizePathInput(OutputPath);
        if (string.IsNullOrWhiteSpace(source))
        {
            OutputPathError = string.Empty;
            return;
        }

        var validation = SelectedOutputMode == OutputMode.Folder
            ? OutputPathGuard.ValidateFolderDestination(source, output)
            : OutputPathGuard.ValidateZipDestination(source, output, _zipStagingRoot);
        OutputPathError = validation.IsValid
            ? string.Empty
            : validation.ErrorCode switch
            {
                "output_equals_source" => "Папка результата не может совпадать с исходной.",
                "output_is_source_parent" => "Папка результата не может быть родителем исходной.",
                "output_is_file" => "На месте папки результата существует файл.",
                "zip_target_conflict" => "ZIP результата уже существует и не будет перезаписан.",
                "zip_extension_required" => "Путь ZIP должен оканчиваться на .zip.",
                _ => "Путь результата недоступен или небезопасен."
            };
    }

    private void TryDeleteZipStaging()
    {
        try
        {
            var expectedRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "ZletBatchConverter",
                "result-staging"));
            var staging = Path.GetFullPath(_zipStagingRoot);
            if (staging.StartsWith(
                    expectedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
        }
    }

    private void SelectionChanged()
    {
        SelectedReadyCount = Operations.Count(row => row.CanSelect && row.IsSelected);
        OnPropertyChanged(nameof(SelectableCount));
        OnPropertyChanged(nameof(SelectionSummary));
        NotifyAvailability();
    }

    private void UpdateSourcePathError()
    {
        var normalized = NormalizePathInput(SelectedFolder);
        SourcePathError = string.IsNullOrWhiteSpace(normalized) || Directory.Exists(normalized)
            ? string.Empty
            : "Папка не существует или недоступна.";
    }

    public static string NormalizePathInput(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length >= 2
            && normalized[0] == '"'
            && normalized[^1] == '"')
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized;
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
