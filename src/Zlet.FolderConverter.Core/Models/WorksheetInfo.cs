namespace Zlet.FolderConverter.Core.Models;

public enum WorksheetVisibility
{
    Visible,
    Hidden,
    VeryHidden
}

public sealed record WorksheetInfo(
    string Name,
    int Index,
    WorksheetVisibility Visibility,
    bool IsEmpty);
