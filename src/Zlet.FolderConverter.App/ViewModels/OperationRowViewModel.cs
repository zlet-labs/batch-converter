using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.ViewModels;

public sealed class OperationRowViewModel(PlannedOperation operation)
{
    public string SourcePath { get; } = operation.SourcePath;

    public string RelativePath { get; } = operation.RelativePath;

    public string SourceFormat { get; } = operation.SourceFormat.ToString().ToUpperInvariant();

    public string TargetFormat { get; } = operation.TargetFormat;

    public string TargetPath { get; } = operation.TargetPath;

    public string AdapterAvailability { get; } = operation.AdapterAvailable ? "Available" : "Unavailable";

    public string Status { get; } = operation.Status.ToString();

    public string Message { get; } = operation.Message;
}
