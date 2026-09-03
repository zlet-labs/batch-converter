using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class OperationRowViewModel : INotifyPropertyChanged
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;
    private long? _executionStartTimestamp;
    private TimeSpan? _executionElapsed;
    private TimeSpan? _liveExecutionElapsed;
    private int? _operationPercent;
    private bool _isNotSelected;
    private readonly LocalizationService _localization = LocalizationService.Current;

    public OperationRowViewModel(
        PlannedOperation operation,
        ConversionResult? result = null,
        bool? isSelected = null,
        bool isNotSelected = false,
        Action? selectionChanged = null)
    {
        Operation = result is null
            ? operation
            : result.Operation with { Status = result.Status, Message = result.Message };
        _isSelected = Operation.Status == OperationStatus.Ready && (isSelected ?? true);
        _isNotSelected = isNotSelected;
        Result = result;
        _selectionChanged = selectionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public PlannedOperation Operation { get; private set; }
    public ConversionResult? Result { get; private set; }
    public bool CanSelect => (Operation.Status is OperationStatus.Ready
        or OperationStatus.Cancelled or OperationStatus.NotProcessed);
    public bool IsNotSelected => _isNotSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var next = CanSelect && value;
            var clearPreviousBatchState = next && _isNotSelected;
            if (_isSelected == next && !clearPreviousBatchState) return;
            _isSelected = next;
            if (clearPreviousBatchState)
            {
                _isNotSelected = false;
                _operationPercent = null;
                _executionStartTimestamp = null;
                _executionElapsed = null;
                _liveExecutionElapsed = null;
                Result = null;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusTone));
            OnPropertyChanged(nameof(ExecutionTimeText));
            _selectionChanged?.Invoke();
        }
    }

    public string SourcePath => Operation.SourcePath;
    public string FilePath => Operation.RelativePath;
    public string ActionLabel => Operation.Target switch
    {
        ConversionTarget.Skip => _localization.Get("TargetSkip"),
        ConversionTarget.Copy => _localization.Get("TargetCopy"),
        _ => $"{Operation.SourceFormat.ToDisplayName()} → {Operation.Target.ToDisplayName()}"
    };
    public string TargetPath => Operation.TargetPath;
    public string ResultPath => Operation.Target == ConversionTarget.Skip
        ? "—"
        : Path.ChangeExtension(Operation.RelativePath, Operation.TargetExtension);
    public string FileSizeText => _localization.FormatFileSize(Operation.SourceSizeBytes);
    public string ExecutionTimeText => (_executionElapsed ?? _liveExecutionElapsed) is { } elapsed
        ? _localization.FormatExecutionTime(elapsed)
        : "—";
    public string Status
    {
        get
        {
            if (_isNotSelected && Operation.Status == OperationStatus.Ready) return _localization.Get("StatusNotSelected");
            var text = LocalizeStatus(Operation.Status, Operation.Target, Operation.Message);
            return _operationPercent.HasValue ? $"{text} · {_operationPercent.Value.ToString(_localization.Culture)}%" : text;
        }
    }
    public string StatusTone => _isNotSelected ? "Cancelled" : Operation.Status switch
    {
        OperationStatus.Succeeded => "Success",
        OperationStatus.Converting => "InProgress",
        OperationStatus.Ready => "Ready",
        OperationStatus.Conflict or OperationStatus.EngineUnavailable
            or OperationStatus.Unsupported or OperationStatus.Skipped => "Warning",
        OperationStatus.Failed => "Danger",
        OperationStatus.Cancelled or OperationStatus.NotProcessed => "Cancelled",
        _ => "Neutral"
    };
    public string Message => Operation.Message;

    public void BeginExecution(long timestamp, int percent)
    {
        if (Operation.Status != OperationStatus.Converting)
        {
            _executionStartTimestamp = timestamp;
            _executionElapsed = null;
            _liveExecutionElapsed = null;
            Result = null;
        }
        var nextPercent = Math.Clamp(percent, 0, 99);
        _operationPercent = _operationPercent.HasValue
            ? Math.Max(_operationPercent.Value, nextPercent)
            : nextPercent;
        _isSelected = false;
        _isNotSelected = false;
        Operation = Operation with { Status = OperationStatus.Converting, Message = _localization.Get("OperationExecuting") };
        NotifyExecutionChanged();
    }

    public void CompleteExecution(ConversionResult result, TimeProvider timeProvider, long timestamp)
    {
        FreezeExecutionTime(timeProvider, timestamp);
        _operationPercent = result.Status == OperationStatus.Succeeded ? 100 : null;
        Result = result;
        Operation = result.Operation with { Status = result.Status, Message = result.Message };
        _isSelected = result.Status == OperationStatus.Cancelled;
        _isNotSelected = false;
        NotifyExecutionChanged();
    }

    public void CancelExecution(TimeProvider timeProvider, long timestamp)
    {
        FreezeExecutionTime(timeProvider, timestamp);
        _operationPercent = null;
        Result = new ConversionResult(
            Operation,
            OperationStatus.Cancelled,
            _localization.Get("OperationCancelled"));
        Operation = Operation with { Status = OperationStatus.Cancelled, Message = _localization.Get("OperationCancelled") };
        _isSelected = true;
        NotifyExecutionChanged();
    }

    public void MarkNotProcessed()
    {
        _operationPercent = null;
        _executionStartTimestamp = null;
        _executionElapsed = null;
        _liveExecutionElapsed = null;
        Result = null;
        Operation = Operation with { Status = OperationStatus.NotProcessed, Message = _localization.Get("OperationNotProcessed") };
        _isSelected = true;
        NotifyExecutionChanged();
    }

    public void MarkNotSelected()
    {
        _isNotSelected = true;
        _isSelected = false;
        _operationPercent = null;
        _executionStartTimestamp = null;
        _executionElapsed = null;
        _liveExecutionElapsed = null;
        Result = null;
        NotifyExecutionChanged();
    }

    public void RefreshExecutionTime(TimeProvider timeProvider, long timestamp)
    {
        if (_executionStartTimestamp is not long start || _executionElapsed.HasValue) return;
        _liveExecutionElapsed = timeProvider.GetElapsedTime(start, timestamp);
        OnPropertyChanged(nameof(ExecutionTimeText));
    }

    public static string LocalizeStatus(
        OperationStatus status,
        ConversionTarget target = ConversionTarget.Skip,
        string message = "",
        string errorCode = "") => status switch
    {
        OperationStatus.Ready when target == ConversionTarget.Copy => LocalizationService.Current.Get("StatusReadyCopy"),
        OperationStatus.Ready => LocalizationService.Current.Get("StatusReadyConvert"),
        OperationStatus.Skipped => LocalizationService.Current.Get("StatusSkipped"),
        OperationStatus.Converting => LocalizationService.Current.Get("StatusConverting"),
        OperationStatus.Succeeded when target == ConversionTarget.Copy => LocalizationService.Current.Get("StatusCopied"),
        OperationStatus.Succeeded => LocalizationService.Current.Get("StatusConverted"),
        OperationStatus.Conflict => LocalizationService.Current.Get("StatusConflict"),
        OperationStatus.Failed when !string.IsNullOrWhiteSpace(message) => LocalizationService.Current.Format("StatusFailedDetail", message),
        OperationStatus.Failed => LocalizationService.Current.Get("StatusFailed"),
        OperationStatus.EngineUnavailable or OperationStatus.Unsupported
            when !string.IsNullOrWhiteSpace(message) => LocalizationService.Current.Format("StatusUnavailableDetail", message),
        OperationStatus.EngineUnavailable or OperationStatus.Unsupported => LocalizationService.Current.Get("StatusUnavailable"),
        OperationStatus.Cancelled => LocalizationService.Current.Get("StatusCancelled"),
        OperationStatus.NotProcessed => LocalizationService.Current.Get("StatusNotProcessed"),
        _ => LocalizationService.Current.Get("StatusUnknown")
    };

    public static string FormatFileSize(long bytes)
    {
        return LocalizationService.Current.FormatFileSize(bytes);
    }

    public static string FormatExecutionTime(TimeSpan elapsed)
    {
        return LocalizationService.Current.FormatExecutionTime(elapsed);
    }

    private void FreezeExecutionTime(TimeProvider timeProvider, long timestamp)
    {
        if (_executionStartTimestamp is long start)
            _executionElapsed = timeProvider.GetElapsedTime(start, timestamp);
        _liveExecutionElapsed = null;
    }

    private void NotifyExecutionChanged()
    {
        OnPropertyChanged(nameof(Operation));
        OnPropertyChanged(nameof(CanSelect));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusTone));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(ExecutionTimeText));
    }

    public void RefreshLocalization() => OnLanguageChanged(this, EventArgs.Empty);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(ExecutionTimeText));
        OnPropertyChanged(nameof(Status));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
