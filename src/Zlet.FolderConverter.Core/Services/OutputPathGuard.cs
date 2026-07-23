namespace Zlet.FolderConverter.Core.Services;

public static class OutputPathGuard
{
    public static bool TryBuildTargetPath(
        string sourceRootPath,
        string outputRootPath,
        string sourcePath,
        string relativePath,
        string targetExtension,
        out string targetPath)
    {
        targetPath = string.Empty;
        try
        {
            var sourceRoot = NormalizeDirectory(sourceRootPath);
            var outputRoot = NormalizeDirectory(outputRootPath);
            var fullSource = Path.GetFullPath(sourcePath);
            if (!IsStrictlyWithin(fullSource, sourceRoot)
                || Path.IsPathRooted(relativePath)
                || ContainsTraversal(relativePath))
            {
                return false;
            }

            var expectedSource = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));
            if (!string.Equals(fullSource, expectedSource, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var targetRelativePath = Path.ChangeExtension(relativePath, targetExtension);
            var candidate = Path.GetFullPath(Path.Combine(outputRoot, targetRelativePath));
            if (!IsStrictlyWithin(candidate, outputRoot))
            {
                return false;
            }

            targetPath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or IOException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return false;
        }
    }

    public static bool IsSafeTargetPath(string targetPath, string outputRootPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(outputRootPath))
        {
            return false;
        }

        try
        {
            var outputRoot = NormalizeDirectory(outputRootPath);
            var candidate = Path.GetFullPath(targetPath);
            if (!IsStrictlyWithin(candidate, outputRoot))
            {
                return false;
            }

            for (var directory = Path.GetDirectoryName(candidate);
                 directory is not null && IsWithinOrEqual(directory, outputRoot);
                 directory = Path.GetDirectoryName(directory))
            {
                if (Directory.Exists(directory)
                    && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                if (string.Equals(NormalizeDirectory(directory), outputRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or IOException
                                           or UnauthorizedAccessException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return false;
        }
    }

    private static bool ContainsTraversal(string relativePath)
    {
        var components = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return components.Any(component => component is "." or "..");
    }

    private static bool IsStrictlyWithin(string candidate, string root) =>
        Path.GetFullPath(candidate).StartsWith(
            NormalizeDirectory(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinOrEqual(string candidate, string root)
    {
        var normalizedCandidate = NormalizeDirectory(candidate);
        var normalizedRoot = NormalizeDirectory(root);
        return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
               || normalizedCandidate.StartsWith(
                   normalizedRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
