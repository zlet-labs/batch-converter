using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public static class DocumentFormatDetector
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".bmp", ".gif", ".heic", ".jpeg", ".jpg", ".png", ".svg", ".tif", ".tiff", ".webp"
    ];

    private static readonly HashSet<string> ArchiveExtensions =
    [
        ".7z", ".bz2", ".gz", ".rar", ".tar", ".tgz", ".zip"
    ];

    public static SourceFormat Detect(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".json" => SourceFormat.Json,
            ".doc" => SourceFormat.Doc,
            ".xls" => SourceFormat.Xls,
            ".ppt" => SourceFormat.Ppt,
            ".docx" => SourceFormat.Docx,
            ".xlsx" => SourceFormat.Xlsx,
            ".pptx" => SourceFormat.Pptx,
            ".odt" => SourceFormat.Odt,
            ".ods" => SourceFormat.Ods,
            ".odp" => SourceFormat.Odp,
            ".pdf" => SourceFormat.Pdf,
            _ when ImageExtensions.Contains(extension) => SourceFormat.Image,
            _ when ArchiveExtensions.Contains(extension) => SourceFormat.Archive,
            _ => SourceFormat.Unknown
        };
    }

    public static bool TryDetect(string path, out SourceFormat format)
    {
        format = Detect(path);
        return format != SourceFormat.Unknown;
    }

    public static string GetTargetExtension(ConversionTarget target) => target.ToExtension();
}
