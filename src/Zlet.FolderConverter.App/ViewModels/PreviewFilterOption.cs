namespace Zlet.FolderConverter.App.ViewModels;

public enum PreviewFilter
{
    All,
    Convert,
    Skip,
    Unavailable,
    Conflicts,
    Errors
}

public sealed record PreviewFilterOption(PreviewFilter Filter, string Label);
