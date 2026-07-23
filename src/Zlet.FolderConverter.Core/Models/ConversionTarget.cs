namespace Zlet.FolderConverter.Core.Models;

public enum ConversionTarget
{
    Skip,
    Copy,
    Txt,
    Markdown,
    Docx,
    Xlsx,
    Pptx,
    Pdf
}

public static class ConversionTargetExtensions
{
    public static string ToDisplayName(this ConversionTarget target) => target switch
    {
        ConversionTarget.Skip => "Не трогать",
        ConversionTarget.Copy => "Копировать без изменений",
        ConversionTarget.Txt => "TXT",
        ConversionTarget.Markdown => "Markdown",
        ConversionTarget.Docx => "DOCX",
        ConversionTarget.Xlsx => "XLSX",
        ConversionTarget.Pptx => "PPTX",
        ConversionTarget.Pdf => "PDF",
        _ => "Не трогать"
    };

    public static string ToExtension(this ConversionTarget target) => target switch
    {
        ConversionTarget.Copy => throw new InvalidOperationException(
            "Copy operations keep the source extension."),
        ConversionTarget.Txt => ".txt",
        ConversionTarget.Markdown => ".md",
        ConversionTarget.Docx => ".docx",
        ConversionTarget.Xlsx => ".xlsx",
        ConversionTarget.Pptx => ".pptx",
        ConversionTarget.Pdf => ".pdf",
        ConversionTarget.Skip => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown conversion target.")
    };
}
