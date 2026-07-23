namespace Zlet.FolderConverter.App.ViewModels;

public enum OutputMode
{
    Folder,
    Zip
}

public sealed record OutputModeOption(OutputMode Mode, string Label);
