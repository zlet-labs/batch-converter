using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IFolderScanner
{
    Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSubfolders,
        CancellationToken cancellationToken);

    Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSubfolders,
        string? excludedDirectoryPath,
        string? excludedFilePath,
        CancellationToken cancellationToken) =>
        ScanAsync(rootPath, includeSubfolders, cancellationToken);
}
