namespace Zlet.FolderConverter.Core.Models;

public sealed record ConversionProgress(
    int Completed,
    int Total,
    string RelativePath,
    OperationStatus Status,
    ConversionResult? Result = null,
    int? OperationPercent = null,
    string WorksheetName = "");
