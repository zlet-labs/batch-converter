namespace Zlet.FolderConverter.Core.Models;

public sealed record ScannedFile(
    string SourcePath,
    string RelativePath,
    DocumentFormat Format);
