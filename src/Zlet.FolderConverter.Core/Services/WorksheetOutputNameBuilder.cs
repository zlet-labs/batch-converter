using System.Text;

namespace Zlet.FolderConverter.Core.Services;

public static class WorksheetOutputNameBuilder
{
    private static readonly HashSet<string> ReservedNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

    public static IReadOnlyList<string> Build(
        string workbookBaseName,
        IEnumerable<string> worksheetNames,
        string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookBaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var worksheetName in worksheetNames)
        {
            var safeSheet = SanitizeSegment(worksheetName);
            var baseName = $"{SanitizeSegment(workbookBaseName)}__{safeSheet}";
            var candidate = baseName + normalizedExtension;
            var suffix = 2;
            while (!used.Add(candidate))
            {
                candidate = $"{baseName}-{suffix}{normalizedExtension}";
                suffix++;
            }
            result.Add(candidate);
        }
        return result;
    }

    public static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString().TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Sheet";
        }
        if (ReservedNames.Contains(sanitized.Split('.')[0]))
        {
            sanitized = $"_{sanitized}";
        }
        return sanitized;
    }
}
