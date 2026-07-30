namespace Zlet.FolderConverter.Core.Models;

public enum OfficeApplicationKind
{
    Word,
    Excel,
    PowerPoint
}

public static class OfficeApplicationKindExtensions
{
    public static string ToDisplayName(this OfficeApplicationKind application) => application switch
    {
        OfficeApplicationKind.Word => "Microsoft Word",
        OfficeApplicationKind.Excel => "Microsoft Excel",
        OfficeApplicationKind.PowerPoint => "Microsoft PowerPoint",
        _ => "Microsoft Office"
    };

    public static string ToShortDisplayName(this OfficeApplicationKind application) => application switch
    {
        OfficeApplicationKind.Word => "Word",
        OfficeApplicationKind.Excel => "Excel",
        OfficeApplicationKind.PowerPoint => "PowerPoint",
        _ => "Office"
    };

    public static string ToRequiredMessage(this OfficeApplicationKind application) =>
        $"Требуется {application.ToDisplayName()}";
}
