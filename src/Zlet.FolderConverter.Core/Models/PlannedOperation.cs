namespace Zlet.FolderConverter.Core.Models;

public sealed record PlannedOperation(
    string SourcePath,
    string RelativePath,
    DocumentFormat SourceFormat,
    string TargetExtension,
    string TargetPath,
    bool AdapterAvailable,
    OperationStatus Status,
    string Message)
{
    public string TargetFormat => TargetExtension.TrimStart('.').ToUpperInvariant();
}
