using System.IO;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class OperationRowViewModel(PlannedOperation operation)
{
    public string SourcePath { get; } = operation.SourcePath;

    public string RelativePath { get; } = operation.RelativePath;

    public string FilePath { get; } = operation.RelativePath;

    public string SourceFormat { get; } = operation.SourceFormat.ToString().ToUpperInvariant();

    public string TargetFormat { get; } = operation.TargetFormat;

    public string FormatLabel { get; } =
        $"{operation.SourceFormat.ToString().ToUpperInvariant()} -> {operation.TargetFormat}";

    public string TargetPath { get; } = operation.TargetPath;

    public string FutureRelativePath { get; } =
        Path.Combine("_converted", Path.ChangeExtension(operation.RelativePath, operation.TargetExtension));

    public string Status { get; } = LocalizeStatus(operation.Status);

    public string StatusTone { get; } = operation.Status switch
    {
        OperationStatus.Ready or OperationStatus.Succeeded => "Positive",
        OperationStatus.Conflict => "Warning",
        OperationStatus.Failed => "Danger",
        _ => "Neutral"
    };

    public string Message { get; } = CreateShortMessage(operation);

    public bool HasTechnicalMessage =>
        Message.Contains("adapter", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("embedded", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("synthetic", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("license", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("mapping", StringComparison.OrdinalIgnoreCase);

    public static string LocalizeStatus(OperationStatus status)
    {
        return status switch
        {
            OperationStatus.Unsupported => "Пока не поддерживается",
            OperationStatus.Conflict => "Конфликт",
            OperationStatus.Ready => "Готово",
            OperationStatus.Failed => "Ошибка",
            OperationStatus.Succeeded => "Готово",
            OperationStatus.Skipped => "Пропущено",
            _ => "Неизвестно"
        };
    }

    private static string CreateShortMessage(PlannedOperation operation)
    {
        return operation.Status switch
        {
            OperationStatus.Unsupported =>
                $"Конвертация {operation.SourceFormat.ToString().ToUpperInvariant()} появится позже.",
            OperationStatus.Conflict =>
                "Файл результата уже существует.",
            OperationStatus.Ready =>
                "Файл можно обработать.",
            OperationStatus.Failed =>
                "Не удалось обработать файл.",
            OperationStatus.Succeeded =>
                "Файл обработан.",
            OperationStatus.Skipped =>
                "Файл пропущен.",
            _ => "Статус неизвестен."
        };
    }
}
