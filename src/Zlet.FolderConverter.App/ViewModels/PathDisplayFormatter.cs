namespace Zlet.FolderConverter.App.ViewModels;

public static class PathDisplayFormatter
{
    public const string EmptyPathPlaceholder = "Папка не выбрана";
    public const int DefaultMaximumLength = 58;

    public static string Format(string? path, int maximumLength = DefaultMaximumLength)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return EmptyPathPlaceholder;
        }

        if (maximumLength < 2 || path.Length <= maximumLength)
        {
            return path;
        }

        var lastSeparator = LastSeparatorIndex(path, path.Length - 1);
        if (lastSeparator < 0)
        {
            return "…" + SafeTail(path, maximumLength - 1);
        }

        var start = lastSeparator;
        while (start > 0)
        {
            var previousSeparator = LastSeparatorIndex(path, start - 1);
            if (previousSeparator < 0
                || path.Length - previousSeparator + 1 > maximumLength)
            {
                break;
            }

            start = previousSeparator;
        }

        var tail = path[start..];
        if (tail.Length + 1 <= maximumLength
            || start == lastSeparator)
        {
            return "…" + tail;
        }

        return "…" + SafeTail(path, maximumLength - 1);
    }

    private static int LastSeparatorIndex(string path, int startIndex)
    {
        for (var index = startIndex; index >= 0; index--)
        {
            if (path[index] is '\\' or '/')
            {
                return index;
            }
        }

        return -1;
    }

    private static string SafeTail(string value, int length)
    {
        var start = Math.Max(0, value.Length - length);
        if (start < value.Length && char.IsLowSurrogate(value[start]))
        {
            start++;
        }

        return value[start..];
    }
}
