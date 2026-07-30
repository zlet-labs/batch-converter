namespace Zlet.FolderConverter.Core.Models;

public sealed record ScanError(
    string Path,
    string Message);
