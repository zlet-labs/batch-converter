namespace Zlet.FolderConverter.Core.Services;

public sealed record LibreOfficeProcessResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false);
