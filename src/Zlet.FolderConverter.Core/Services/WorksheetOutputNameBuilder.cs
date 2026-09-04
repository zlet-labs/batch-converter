using System.Text;

namespace Zlet.FolderConverter.Core.Services;

public static class WorksheetOutputNameBuilder
{
    // Leave room for SafeFileOperationExecutor's dot/GUID/tmp staging suffix (< 255 total).
    public const int MaxFileNameLength = 200;
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
            var workbook = SanitizeSegment(workbookBaseName);
            var candidate = CreateName(workbook, safeSheet, normalizedExtension, "");
            var suffix = 2;
            while (!used.Add(candidate))
            {
                candidate = CreateName(workbook, safeSheet, normalizedExtension, $"-{suffix}");
                suffix++;
            }
            result.Add(candidate);
        }
        return result;
    }

    private static string CreateName(string workbook, string sheet, string extension, string suffix)
    {
        var budget = MaxFileNameLength - extension.Length - suffix.Length - 2;
        var sheetBudget = Math.Min(sheet.Length, budget / 2);
        var workbookBudget = Math.Min(workbook.Length, budget - sheetBudget);
        sheetBudget = budget - workbookBudget;
        return $"{TrimToBudget(workbook, workbookBudget)}__{TrimToBudget(sheet, sheetBudget)}{suffix}{extension}";
    }

    public static string WithCollisionSuffix(string fileName, int suffix)
    {
        var extension = Path.GetExtension(fileName);
        var tail = $"-{suffix}{extension}";
        return TrimToBudget(Path.GetFileNameWithoutExtension(fileName), MaxFileNameLength - tail.Length) + tail;
    }

    private static string TrimToBudget(string value, int budget)
    {
        if (budget < 1) throw new ArgumentException("Filename extension leaves no room for a name.");
        var length = Math.Min(value.Length, budget);
        if (length < value.Length && char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length].TrimEnd(' ', '.');
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
