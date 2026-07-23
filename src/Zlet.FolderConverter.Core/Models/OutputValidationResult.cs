namespace Zlet.FolderConverter.Core.Models;

public sealed record OutputValidationResult(
    bool IsValid,
    string ErrorCode = "");
