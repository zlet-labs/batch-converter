using System.Globalization;
using System.Windows;

namespace Zlet.FolderConverter.App.Localization;

public sealed class LocalizationService
{
    private const string DictionaryPrefix = "Resources/Strings.";
    private static readonly Lazy<LocalizationService> LazyCurrent = new(() => new());
    private static readonly object DictionaryLoadLock = new();

    private IReadOnlyDictionary<string, string> _strings;

    private LocalizationService()
    {
        var dictionary = LoadDictionary(AppLanguage.Russian);
        _strings = CopyStrings(dictionary);
    }

    public static LocalizationService Current => LazyCurrent.Value;
    public static LocalizationService CreateStandalone(string language)
    {
        var service = new LocalizationService();
        service.Apply(language);
        return service;
    }
    public string Language { get; private set; } = AppLanguage.Russian;
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language);
    public event EventHandler? LanguageChanged;

    public void Apply(string language)
    {
        if (!AppLanguage.IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        language = AppLanguage.Normalize(language);
        var app = System.Windows.Application.Current;
        if (app is not null)
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var old = dictionaries.FirstOrDefault(dictionary =>
                dictionary.Source?.OriginalString.Contains(DictionaryPrefix, StringComparison.OrdinalIgnoreCase) == true);
            var replacement = LoadDictionary(language);
            if (old is null) dictionaries.Insert(0, replacement);
            else dictionaries[dictionaries.IndexOf(old)] = replacement;
            _strings = CopyStrings(replacement);
        }
        else
        {
            _strings = CopyStrings(LoadDictionary(language));
        }
        Language = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        return _strings.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Localization key '{key}' was not found for {Language}.");
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    public string FileWord(int count)
        => Get(LocalizationFormatting.FileWordResourceKey(count, Language));

    public string FormatFileSize(long bytes)
    {
        return LocalizationFormatting.FormatFileSize(bytes, Culture, Get("MegabyteUnit"));
    }

    public string FormatExecutionTime(TimeSpan elapsed)
    {
        return LocalizationFormatting.FormatExecutionTime(elapsed, Culture, Get("SecondUnit"));
    }

    private static ResourceDictionary LoadDictionary(string language)
    {
        lock (DictionaryLoadLock)
        {
            return (ResourceDictionary)System.Windows.Application.LoadComponent(
                new Uri($"/{typeof(LocalizationService).Assembly.GetName().Name};component/Resources/Strings.{language}.xaml", UriKind.Relative));
        }
    }

    private static IReadOnlyDictionary<string, string> CopyStrings(ResourceDictionary dictionary) =>
        dictionary.Keys.Cast<object>().ToDictionary(
            key => key.ToString()!,
            key => dictionary[key] as string
                ?? throw new System.IO.InvalidDataException($"Localization value '{key}' is not a string."),
            StringComparer.Ordinal);

}
