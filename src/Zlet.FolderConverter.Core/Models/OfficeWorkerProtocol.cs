namespace Zlet.FolderConverter.Core.Models;

public sealed record OfficeWorkerRequest(
    OfficeApplicationKind Application,
    string SourcePath,
    string OutputPath);

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
    bool SessionInvalid = false);
