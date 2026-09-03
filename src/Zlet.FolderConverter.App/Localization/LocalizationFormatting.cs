using System.Globalization;

namespace Zlet.FolderConverter.App.Localization;

public static class LocalizationFormatting
{
    public static string FileWord(int count, string language)
    {
        if (AppLanguage.Normalize(language) == AppLanguage.English)
            return Math.Abs(count) == 1 ? "file" : "files";
        var absolute = Math.Abs(count);
        if (absolute % 100 is >= 11 and <= 14) return "файлов";
        return (absolute % 10) switch { 1 => "файл", 2 or 3 or 4 => "файла", _ => "файлов" };
    }

    public static string FormatFileSize(long bytes, string language)
    {
        var normalized = AppLanguage.Normalize(language);
        var culture = CultureInfo.GetCultureInfo(normalized);
        var megabytes = Math.Max(0, bytes) / 1024d / 1024d;
        var format = megabytes < 10 ? "0.##" : megabytes < 100 ? "0.#" : "0";
        return $"{megabytes.ToString(format, culture)} {(normalized == AppLanguage.Russian ? "МБ" : "MB")}";
    }

    public static string FormatExecutionTime(TimeSpan elapsed, string language)
    {
        var normalized = AppLanguage.Normalize(language);
        var seconds = Math.Max(0, elapsed.TotalSeconds);
        if (seconds >= 60) return $"{(int)seconds / 60}:{(int)seconds % 60:00}";
        return $"{seconds.ToString(seconds < 10 ? "0.#" : "0", CultureInfo.GetCultureInfo(normalized))} {(normalized == AppLanguage.Russian ? "с" : "s")}";
    }
}
