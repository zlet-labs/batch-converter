namespace Zlet.FolderConverter.Core.Services;

public sealed record LibreOfficeRuntimeLocation(
    bool IsAvailable,
    string ExecutablePath = "");
