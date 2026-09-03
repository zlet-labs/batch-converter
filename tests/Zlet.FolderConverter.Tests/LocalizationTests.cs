using System.Text.Json.Nodes;
using System.Xml.Linq;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.Settings;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Tests;

public sealed class LocalizationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "zlet-localization-tests", Guid.NewGuid().ToString("N"));
    public LocalizationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Resource_sets_have_equal_unique_nonempty_keys_and_required_startup_keys()
    {
        var en = ReadResources("en-US");
        var ru = ReadResources("ru-RU");
        Assert.Equal(en.Keys.Order(), ru.Keys.Order());
        Assert.All(en.Values.Concat(ru.Values), value => Assert.False(string.IsNullOrWhiteSpace(value)));
        foreach (var key in new[] { "SettingsTitle", "LanguageLabel", "LanguageRussian", "LanguageEnglish", "InitialState", "FolderUnavailable", "StatusConverted", "OutputFolder" })
        {
            Assert.Contains(key, en.Keys);
            Assert.Contains(key, ru.Keys);
        }
    }

    [Fact]
    public void Settings_path_uses_current_product_identity() =>
        Assert.EndsWith(Path.Combine("Zlet Labs", "Zlet Converter", "settings.json"), AppSettingsStore.DefaultPath, StringComparison.Ordinal);

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public void Valid_saved_language_round_trips(string language)
    {
        var store = Store(); store.SaveLanguage(language); Assert.Equal(language, store.LoadLanguage());
    }

    [Fact]
    public void Missing_corrupted_and_unknown_settings_require_a_choice()
    {
        var store = Store();
        Assert.Null(store.LoadLanguage());
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        File.WriteAllText(store.SettingsPath, "{broken");
        Assert.Null(store.LoadLanguage());
        File.WriteAllText(store.SettingsPath, "{\"language\":\"xx-YY\"}");
        Assert.Null(store.LoadLanguage());
    }

    [Fact]
    public void Valid_cli_overrides_saved_language_and_requests_persistence()
    {
        var decision = StartupLanguageResolver.Resolve("en-US", "ru-RU");
        Assert.Equal("en-US", decision.Language);
        Assert.True(decision.PersistExplicit);
        Assert.False(decision.ChooserRequired);
    }

    [Fact]
    public void Invalid_cli_keeps_valid_saved_language_without_overwrite()
    {
        var decision = StartupLanguageResolver.Resolve("xx-YY", "ru-RU");
        Assert.Equal("ru-RU", decision.Language);
        Assert.False(decision.PersistExplicit);
        Assert.False(decision.ChooserRequired);
    }

    [Fact]
    public void Invalid_cli_without_saved_language_requires_chooser() =>
        Assert.True(StartupLanguageResolver.Resolve("xx-YY", null).ChooserRequired);

    [Fact]
    public void Settings_update_preserves_unrelated_values()
    {
        var store = Store();
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        File.WriteAllText(store.SettingsPath, "{\"futureSetting\":42,\"language\":\"ru-RU\"}");
        store.SaveLanguage("en-US");
        var json = JsonNode.Parse(File.ReadAllText(store.SettingsPath))!.AsObject();
        Assert.Equal(42, json["futureSetting"]!.GetValue<int>());
        Assert.Equal("en-US", json["language"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(1, "файл")][InlineData(2, "файла")][InlineData(5, "файлов")][InlineData(11, "файлов")][InlineData(21, "файл")][InlineData(22, "файла")][InlineData(25, "файлов")]
    public void Russian_pluralization(int count, string expected) => Assert.Equal(expected, LocalizationFormatting.FileWord(count, AppLanguage.Russian));

    [Theory][InlineData(1, "file")][InlineData(2, "files")]
    public void English_pluralization(int count, string expected) => Assert.Equal(expected, LocalizationFormatting.FileWord(count, AppLanguage.English));

    [Fact]
    public void Sizes_and_times_follow_selected_language()
    {
        Assert.Equal("0,84 МБ", LocalizationFormatting.FormatFileSize(880804, AppLanguage.Russian));
        Assert.Equal("0.84 MB", LocalizationFormatting.FormatFileSize(880804, AppLanguage.English));
        Assert.Equal("3,2 с", LocalizationFormatting.FormatExecutionTime(TimeSpan.FromSeconds(3.2), AppLanguage.Russian));
        Assert.Equal("3.2 s", LocalizationFormatting.FormatExecutionTime(TimeSpan.FromSeconds(3.2), AppLanguage.English));
    }

    [Fact]
    public void Language_switch_preserves_semantic_operation_state()
    {
        var operation = new PlannedOperation("C:\\source\\a.doc", "a.doc", SourceFormat.Doc,
            ConversionTarget.Docx, ".docx", "C:\\result\\a.docx", true,
            OperationStatus.Succeeded, "ok", "C:\\result", "C:\\source", 880804);
        var row = new OperationRowViewModel(operation, new ConversionResult(operation, OperationStatus.Succeeded, "ok"));
        try
        {
            LocalizationService.Current.Apply(AppLanguage.English);
            Assert.Equal("Converted", row.Status);
            Assert.Equal(OperationStatus.Succeeded, row.Operation.Status);
            Assert.Equal("a.doc", row.FilePath);
            LocalizationService.Current.Apply(AppLanguage.Russian);
            Assert.Equal("Преобразовано", row.Status);
            Assert.Equal(OperationStatus.Succeeded, row.Operation.Status);
            Assert.Equal("a.doc", row.FilePath);
        }
        finally { LocalizationService.Current.Apply(AppLanguage.Russian); }
    }

    [Fact]
    public void Status_filter_output_and_office_labels_exist_in_both_languages()
    {
        var en = ReadResources("en-US"); var ru = ReadResources("ru-RU");
        Assert.Equal("Converted", en["StatusConverted"]); Assert.Equal("Преобразовано", ru["StatusConverted"]);
        Assert.Equal("To convert", en["FilterConvert"]); Assert.Equal("К преобразованию", ru["FilterConvert"]);
        Assert.Equal("Folder", en["OutputFolder"]); Assert.Equal("Папка", ru["OutputFolder"]);
        Assert.Equal("available", en["OfficeAvailable"]); Assert.Equal("доступен", ru["OfficeAvailable"]);
    }

    [Fact]
    public void No_stale_product_identity_is_present_in_current_ui()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "Zlet.FolderConverter.App"), "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".xaml");
        Assert.All(files, path => Assert.DoesNotContain("Title=\"Zlet Batch Converter", File.ReadAllText(path)));
    }

    private AppSettingsStore Store() => new(Path.Combine(_root, "settings.json"));
    private static Dictionary<string, string> ReadResources(string language)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(RepositoryRoot(), "src", "Zlet.FolderConverter.App", "Resources", $"Strings.{language}.xaml");
        var entries = XDocument.Load(path).Root!.Elements().Select(element => (Key: (string?)element.Attribute(x + "Key"), Value: element.Value)).ToArray();
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Key)));
        Assert.Equal(entries.Length, entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count());
        return entries.ToDictionary(entry => entry.Key!, entry => entry.Value, StringComparer.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FolderConverter.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
