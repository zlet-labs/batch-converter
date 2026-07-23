using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class OperationRowViewModel : INotifyPropertyChanged
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;

    public OperationRowViewModel(
        PlannedOperation operation,
        ConversionResult? result = null,
        bool? isSelected = null,
        bool isNotSelected = false,
        Action? selectionChanged = null)
    {
        Operation = operation;
        var status = result?.Status ?? operation.Status;
        CanSelect = status == OperationStatus.Ready;
        _isSelected = CanSelect && (isSelected ?? true);
        _selectionChanged = selectionChanged;
        SourcePath = operation.SourcePath;
        FilePath = operation.RelativePath;
        ActionLabel = operation.Target switch
        {
            ConversionTarget.Skip => "Не трогать",
            ConversionTarget.Copy => "Копировать без изменений",
            _ => $"{operation.SourceFormat.ToDisplayName()} → {operation.Target.ToDisplayName()}"
        };
        TargetPath = operation.TargetPath;
        ResultPath = operation.Target == ConversionTarget.Skip
            ? "—"
            : Path.ChangeExtension(operation.RelativePath, operation.TargetExtension);
        Status = isNotSelected && status == OperationStatus.Ready
            ? "Не выбрано"
            : LocalizeStatus(
                status,
                operation.Target,
                result?.Message ?? operation.Message,
                result?.Diagnostic?.ErrorCode ?? string.Empty);
        StatusTone = status switch
        {
            OperationStatus.Ready or OperationStatus.Converting or OperationStatus.Succeeded => "Positive",
            OperationStatus.Conflict or OperationStatus.EngineUnavailable => "Warning",
            OperationStatus.Failed => "Danger",
            _ => "Neutral"
        };
        Message = result?.Message ?? operation.Message;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PlannedOperation Operation { get; }
    public bool CanSelect { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            var next = CanSelect && value;
            if (_isSelected == next)
            {
                return;
            }

            _isSelected = next;
            OnPropertyChanged();
            _selectionChanged?.Invoke();
        }
    }
    public string SourcePath { get; }
    public string FilePath { get; }
    public string ActionLabel { get; }
    public string TargetPath { get; }
    public string ResultPath { get; }
    public string Status { get; }
    public string StatusTone { get; }
    public string Message { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public static string LocalizeStatus(
        OperationStatus status,
        ConversionTarget target = ConversionTarget.Skip,
        string message = "",
        string errorCode = "") => status switch
    {
        OperationStatus.Ready when target == ConversionTarget.Copy => "Будет скопирован без изменений",
        OperationStatus.Ready => "Готово к преобразованию",
        OperationStatus.Skipped => "Пропущен",
        OperationStatus.Converting => "В процессе",
        OperationStatus.Succeeded when target == ConversionTarget.Copy => "Скопировано",
        OperationStatus.Succeeded => "Преобразовано",
        OperationStatus.Conflict => "Файл результата уже существует",
        OperationStatus.Failed when errorCode == "powerpoint_already_running"
                                    && !string.IsNullOrWhiteSpace(message) => message,
        OperationStatus.Failed => "Ошибка",
        OperationStatus.EngineUnavailable when !string.IsNullOrWhiteSpace(message) => message,
        OperationStatus.EngineUnavailable => "Требуется Microsoft Office",
        OperationStatus.Unsupported => "Не поддерживается",
        _ => "Неизвестно"
    };
}
