namespace Zlet.FolderConverter.Core.Models;

public sealed record PlannedOperation(
    string SourcePath,
    string RelativePath,
    SourceFormat SourceFormat,
    ConversionTarget Target,
    string TargetExtension,
    string TargetPath,
    bool AdapterAvailable,
    OperationStatus Status,
    string Message,
    string OutputRootPath = "",
    string SourceRootPath = "")
{
    public string TargetFormat => Target == ConversionTarget.Skip
        ? "Не трогать"
        : Target.ToDisplayName();
}
