namespace Zlet.FolderConverter.App.Localization;

public static class AppLanguage
{
    public const string Russian = "ru-RU";
    public const string English = "en-US";

    public static IReadOnlyList<string> Supported { get; } = [Russian, English];

    public static bool IsSupported(string? value) =>
        Supported.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        string.Equals(value, Russian, StringComparison.OrdinalIgnoreCase) ? Russian : English;
}
