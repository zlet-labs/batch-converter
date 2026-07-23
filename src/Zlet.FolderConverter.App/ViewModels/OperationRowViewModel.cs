using System.IO;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class OperationRowViewModel
{
    public OperationRowViewModel(PlannedOperation operation, ConversionResult? result = null)
    {
        Operation = operation;
        var status = result?.Status ?? operation.Status;
        SourcePath = operation.SourcePath;
        FilePath = operation.RelativePath;
        ActionLabel = operation.Target == ConversionTarget.Skip
            ? "Не трогать"
            : $"{operation.SourceFormat.ToDisplayName()} → {operation.Target.ToDisplayName()}";
        TargetPath = operation.TargetPath;
        ResultPath = operation.Target == ConversionTarget.Skip
            ? "—"
            : Path.ChangeExtension(operation.RelativePath, operation.TargetExtension);
        Status = LocalizeStatus(status);
        StatusTone = status switch
        {
            OperationStatus.Ready or OperationStatus.Converting or OperationStatus.Succeeded => "Positive",
            OperationStatus.Conflict or OperationStatus.EngineUnavailable => "Warning",
            OperationStatus.Failed => "Danger",
            _ => "Neutral"
        };
        Message = result?.Message ?? operation.Message;
    }

    public PlannedOperation Operation { get; }
    public string SourcePath { get; }
    public string FilePath { get; }
    public string ActionLabel { get; }
    public string TargetPath { get; }
    public string ResultPath { get; }
    public string Status { get; }
    public string StatusTone { get; }
    public string Message { get; }

    public static string LocalizeStatus(OperationStatus status) => status switch
    {
        OperationStatus.Ready => "Готов",
        OperationStatus.Skipped => "Пропущен",
        OperationStatus.Converting => "В процессе",
        OperationStatus.Succeeded => "Успешно",
        OperationStatus.Conflict => "Конфликт",
        OperationStatus.Failed => "Ошибка",
        OperationStatus.EngineUnavailable => "Движок недоступен",
        OperationStatus.Unsupported => "Не поддерживается",
        _ => "Неизвестно"
    };
}
