using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class FileSystemFolderScanner : IFolderScanner
{
    private const string ConvertedFolderName = "_converted";

    public Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSubfolders,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        return Task.Run(
            () => Scan(rootPath, includeSubfolders, cancellationToken),
            cancellationToken);
    }

    private static ScanResult Scan(
        string rootPath,
        bool includeSubfolders,
        CancellationToken cancellationToken)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var files = new List<ScannedFile>();
        var errors = new List<ScanError>();

        if (!Directory.Exists(fullRootPath))
        {
            return new ScanResult(
                fullRootPath,
                files,
                [new ScanError(fullRootPath, "Selected folder does not exist.")]);
        }

        var pending = new Stack<string>();
        pending.Push(fullRootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pending.Pop();

            foreach (var filePath in EnumerateFiles(currentDirectory, errors))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsOfficeTemporaryFile(filePath))
                {
                    continue;
                }

                if (!DocumentFormatDetector.TryDetect(filePath, out var format))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(fullRootPath, filePath);
                files.Add(new ScannedFile(filePath, relativePath, format));
            }

            if (!includeSubfolders)
            {
                continue;
            }

            foreach (var directoryPath in EnumerateDirectories(currentDirectory, errors))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsConvertedDirectory(directoryPath))
                {
                    continue;
                }

                pending.Push(directoryPath);
            }
        }

        return new ScanResult(fullRootPath, files, errors);
    }

    private static IEnumerable<string> EnumerateFiles(string directoryPath, List<ScanError> errors)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath).ToArray();
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            errors.Add(new ScanError(directoryPath, exception.Message));
            return [];
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string directoryPath, List<ScanError> errors)
    {
        try
        {
            return Directory.EnumerateDirectories(directoryPath).ToArray();
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            errors.Add(new ScanError(directoryPath, exception.Message));
            return [];
        }
    }

    public static bool IsOfficeTemporaryFile(string filePath)
    {
        return Path.GetFileName(filePath).StartsWith("~$", StringComparison.Ordinal);
    }

    public static bool IsConvertedDirectory(string directoryPath)
    {
        return string.Equals(
            Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            ConvertedFolderName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableFileSystemException(Exception exception)
    {
        return exception is UnauthorizedAccessException
            or IOException
            or PathTooLongException
            or DirectoryNotFoundException
            or NotSupportedException;
    }
}
