namespace Zlet.FolderConverter.Core.Models;

public sealed record ConversionDiagnostic(
    string ErrorCode,
    int? ExitCode = null,
    bool TimedOut = false,
    bool Cancelled = false,
    bool HasStandardOutput = false,
    bool HasStandardError = false);
