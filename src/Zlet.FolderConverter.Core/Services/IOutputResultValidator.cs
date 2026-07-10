namespace Zlet.FolderConverter.Core.Services;

public interface IOutputResultValidator
{
    bool IsSuccessfulOutput(string targetPath);
}
