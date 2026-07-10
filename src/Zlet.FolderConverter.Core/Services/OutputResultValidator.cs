namespace Zlet.FolderConverter.Core.Services;

public sealed class OutputResultValidator : IOutputResultValidator
{
    public bool IsSuccessfulOutput(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            return false;
        }

        var fileInfo = new FileInfo(targetPath);
        return fileInfo.Length > 0;
    }
}
