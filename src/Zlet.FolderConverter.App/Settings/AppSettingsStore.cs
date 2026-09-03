using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using Zlet.FolderConverter.App.Localization;

namespace Zlet.FolderConverter.App.Settings;

public sealed class AppSettingsStore
{
    public AppSettingsStore(string? path = null) => SettingsPath = path ?? DefaultPath;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Zlet Labs", "Zlet Converter", "settings.json");

    public string SettingsPath { get; }

    public string? LoadLanguage()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var root = JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject;
            var language = root?["language"]?.GetValue<string>();
            return AppLanguage.IsSupported(language) ? AppLanguage.Normalize(language!) : null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public void SaveLanguage(string language)
    {
        if (!AppLanguage.IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        JsonObject root;
        try
        {
            root = File.Exists(SettingsPath)
                ? JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (JsonException) { root = new JsonObject(); }
        catch (InvalidOperationException) { root = new JsonObject(); }

        root["language"] = AppLanguage.Normalize(language);
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, SettingsPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
