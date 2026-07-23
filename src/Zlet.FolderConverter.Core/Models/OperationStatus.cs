namespace Zlet.FolderConverter.Core.Models;

public enum OperationStatus
{
    Ready,
    Skipped,
    Converting,
    Succeeded,
    Conflict,
    Failed,
    EngineUnavailable,
    Unsupported
}
