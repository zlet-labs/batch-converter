namespace Zlet.FolderConverter.Core.Models;

public sealed record ScanResult(
    string RootPath,
    IReadOnlyList<ScannedFile> Files,
    IReadOnlyList<ScanError> Errors)
{
    public int JsonCount => Files.Count(file => file.Format == DocumentFormat.Json);

    public int DocCount => Files.Count(file => file.Format == DocumentFormat.Doc);

    public int XlsCount => Files.Count(file => file.Format == DocumentFormat.Xls);

    public int PptCount => Files.Count(file => file.Format == DocumentFormat.Ppt);
}
