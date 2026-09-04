namespace Zlet.FolderConverter.Core.Models;

public sealed record ScannedFile(
    string SourcePath,
    string RelativePath,
    SourceFormat Format,
    long SizeBytes = 0,
    IReadOnlyList<WorksheetInfo>? Worksheets = null,
    string WorksheetInspectionErrorCode = "");
