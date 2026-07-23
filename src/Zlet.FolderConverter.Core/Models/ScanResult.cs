namespace Zlet.FolderConverter.Core.Models;

public sealed record ScanResult(
    string RootPath,
    IReadOnlyList<ScannedFile> Files,
    IReadOnlyList<ScanError> Errors)
{
    public int Count(SourceFormat format) => Files.Count(file => file.Format == format);
}
