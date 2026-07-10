using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public static class DocumentFormatDetector
{
    public static bool TryDetect(string path, out DocumentFormat format)
    {
        var extension = Path.GetExtension(path);

        if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase))
        {
            format = DocumentFormat.Doc;
            return true;
        }

        if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            format = DocumentFormat.Xls;
            return true;
        }

        if (extension.Equals(".ppt", StringComparison.OrdinalIgnoreCase))
        {
            format = DocumentFormat.Ppt;
            return true;
        }

        format = default;
        return false;
    }

    public static string GetTargetExtension(DocumentFormat format)
    {
        return format switch
        {
            DocumentFormat.Doc => ".docx",
            DocumentFormat.Xls => ".xlsx",
            DocumentFormat.Ppt => ".pptx",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported document format.")
        };
    }
}
