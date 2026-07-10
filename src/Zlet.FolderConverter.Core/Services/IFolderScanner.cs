using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IFolderScanner
{
    Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSubfolders,
        CancellationToken cancellationToken);
}
