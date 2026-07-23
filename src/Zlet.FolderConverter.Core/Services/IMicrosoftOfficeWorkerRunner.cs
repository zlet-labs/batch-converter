using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IMicrosoftOfficeWorkerRunner
{
    bool IsAvailable { get; }

    Task<OfficeWorkerExecutionResult> RunAsync(
        OfficeWorkerRequest request,
        CancellationToken cancellationToken);
}

public sealed record OfficeWorkerExecutionResult(
    bool Success,
    string ErrorCode = "",
    int? ExitCode = null,
    bool TimedOut = false,
    bool HasStandardOutput = false,
    bool HasStandardError = false,
    int? HResult = null);
