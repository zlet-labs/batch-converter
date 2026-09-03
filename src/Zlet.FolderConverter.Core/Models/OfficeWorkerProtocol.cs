namespace Zlet.FolderConverter.Core.Models;

public enum OfficeWorkerOperation
{
    Convert,
    InspectWorkbook
}

public sealed record OfficeWorkerRequest(
    OfficeApplicationKind Application,
    string SourcePath,
    string OutputPath,
    ConversionTarget Target = ConversionTarget.Skip,
    string WorksheetName = "",
    OfficeWorkerOperation Operation = OfficeWorkerOperation.Convert);

public enum OfficeWorkerMessageType
{
    Started,
    Result
}

public sealed record OfficeWorkerMessage(
    OfficeWorkerMessageType MessageType,
    bool Success = false,
    string ErrorCode = "",
    int? OfficeProcessId = null,
    long? OfficeProcessStartTimeUtcTicks = null,
    bool OfficeProcessOwned = false,
    int? HResult = null,
    bool SessionInvalid = false,
    bool AbandonOfficeProcessOwnership = false,
    IReadOnlyList<WorksheetInfo>? Worksheets = null);
