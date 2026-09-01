using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IMicrosoftOfficeWorkerRunner
{
    bool IsAvailable { get; }

    Task<OfficeWorkerExecutionResult> RunAsync(
        OfficeWorkerRequest request,
        CancellationToken cancellationToken);

    Task BeginBatchAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    Task EndBatchAsync() => Task.CompletedTask;
}

public sealed record OfficeWorkerExecutionResult(
    bool Success,
    string ErrorCode = "",
    int? ExitCode = null,
    bool TimedOut = false,
    bool HasStandardOutput = false,
    bool HasStandardError = false,
    int? HResult = null,
    bool SessionInvalid = false);
