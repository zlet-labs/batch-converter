using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class FileSystemFolderScanner : IFolderScanner
{
    private const string ConvertedFolderName = "_converted";

    public Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSubfolders,
        CancellationToken cancellationToken) =>
        ScanAsync(
            rootPath,
            includeSubfolders,
            Path.Combine(Path.GetFullPath(rootPath), ConvertedFolderName),
            null,
            cancellationToken);

    public Task<ScanResult> ScanAsync(
        string rootPath,
        bool includeSubfolders,
        string? excludedDirectoryPath,
        string? excludedFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        return Task.Run(
            () => Scan(
                rootPath,
                includeSubfolders,
                excludedDirectoryPath,
                excludedFilePath,
                cancellationToken),
            cancellationToken);
    }

    private static ScanResult Scan(
        string rootPath,
        bool includeSubfolders,
        string? excludedDirectoryPath,
        string? excludedFilePath,
        CancellationToken cancellationToken)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var excludedDirectory = NormalizeOptional(excludedDirectoryPath);
        var excludedFile = NormalizeOptional(excludedFilePath);
        var files = new List<ScannedFile>();
        var errors = new List<ScanError>();

        if (!Directory.Exists(fullRootPath))
        {
            return new ScanResult(
                fullRootPath,
                files,
                [new ScanError(fullRootPath, "Selected folder does not exist.")]);
        }

        if (IsReparseDirectory(fullRootPath))
        {
            return new ScanResult(
                fullRootPath,
                files,
                [new ScanError(fullRootPath, "Selected folder is a reparse point.")]);
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

                if (IsTemporaryMicrosoftOfficeFile(filePath) || IsReparseFile(filePath))
                {
                    continue;
                }

                if (excludedFile is not null
                    && string.Equals(
                        Path.GetFullPath(filePath),
                        excludedFile,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(fullRootPath, filePath);
                files.Add(new ScannedFile(
                    filePath,
                    relativePath,
                    DocumentFormatDetector.Detect(filePath)));
            }

            if (!includeSubfolders)
            {
                continue;
            }

            foreach (var directoryPath in EnumerateDirectories(currentDirectory, errors))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsExcludedDirectory(directoryPath, excludedDirectory)
                    || IsReparseDirectory(directoryPath))
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
            errors.Add(new ScanError(directoryPath, "Нет доступа к файлам в этой папке."));
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
            errors.Add(new ScanError(directoryPath, "Нет доступа к вложенным папкам."));
            return [];
        }
    }

    public static bool IsTemporaryMicrosoftOfficeFile(string filePath)
    {
        return Path.GetFileName(filePath).StartsWith("~$", StringComparison.Ordinal);
    }

    private static bool IsExcludedDirectory(string directoryPath, string? excludedDirectory)
    {
        if (excludedDirectory is null)
        {
            return false;
        }

        var candidate = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidate, excludedDirectory, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(
                   excludedDirectory + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static bool IsReparseDirectory(string directoryPath)
    {
        try
        {
            return (File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            return true;
        }
    }

    public static bool IsReparseFile(string filePath)
    {
        try
        {
            return (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (IsRecoverableFileSystemException(exception))
        {
            return true;
        }
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
