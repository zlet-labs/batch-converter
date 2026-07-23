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
        FormatLabel = $"{operation.SourceFormat.ToString().ToUpperInvariant()} → {operation.TargetFormat}";
        TargetPath = operation.TargetPath;
        FutureRelativePath = Path.Combine(
            "_converted",
            Path.ChangeExtension(operation.RelativePath, operation.TargetExtension));
        Status = LocalizeStatus(status);
        StatusTone = status switch
        {
            OperationStatus.Ready or OperationStatus.Succeeded => "Positive",
            OperationStatus.Conflict => "Warning",
            OperationStatus.Failed => "Danger",
            _ => "Neutral"
        };
        Message = result?.Message ?? CreateShortMessage(status);
    }

    public PlannedOperation Operation { get; }
    public string SourcePath { get; }
    public string FilePath { get; }
    public string FormatLabel { get; }
    public string TargetPath { get; }
    public string FutureRelativePath { get; }
    public string Status { get; }
    public string StatusTone { get; }
    public string Message { get; }
    public bool HasTechnicalMessage => false;

    public static string LocalizeStatus(OperationStatus status) => status switch
    {
        OperationStatus.Ready => "Готово к обработке",
        OperationStatus.Succeeded => "Преобразовано",
        OperationStatus.Unsupported => "Не поддерживается",
        OperationStatus.Conflict => "Конфликт",
        OperationStatus.Failed => "Ошибка",
        OperationStatus.Skipped => "Пропущено",
        _ => "Неизвестно"
    };

    private static string CreateShortMessage(OperationStatus status) => status switch
    {
        OperationStatus.Unsupported => "Конвертация недоступна.",
        OperationStatus.Conflict => "Файл или папка результата уже существует.",
        OperationStatus.Ready => "Файл можно обработать.",
        OperationStatus.Failed => "Не удалось обработать файл.",
        OperationStatus.Succeeded => "Файл преобразован.",
        _ => "Файл пропущен."
    };
}
