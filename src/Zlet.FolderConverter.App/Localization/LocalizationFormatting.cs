using System.Globalization;

namespace Zlet.FolderConverter.App.Localization;

public static class LocalizationFormatting
{
    public static string FileWordResourceKey(int count, string language)
    {
        if (AppLanguage.Normalize(language) == AppLanguage.English)
            return Math.Abs(count) == 1 ? "FileOne" : "FileMany";
        var absolute = Math.Abs(count);
        if (absolute % 100 is >= 11 and <= 14) return "FileMany";
        return (absolute % 10) switch { 1 => "FileOne", 2 or 3 or 4 => "FileFew", _ => "FileMany" };
    }

    public static string FormatFileSize(long bytes, CultureInfo culture, string unit)
    {
        var megabytes = Math.Max(0, bytes) / 1024d / 1024d;
        var format = megabytes < 10 ? "0.##" : megabytes < 100 ? "0.#" : "0";
        return $"{megabytes.ToString(format, culture)} {unit}";
    }

    public static string FormatExecutionTime(TimeSpan elapsed, CultureInfo culture, string unit)
    {
        var seconds = Math.Max(0, elapsed.TotalSeconds);
        if (seconds >= 60) return $"{(int)seconds / 60}:{(int)seconds % 60:00}";
        return $"{seconds.ToString(seconds < 10 ? "0.#" : "0", culture)} {unit}";
    }
}
