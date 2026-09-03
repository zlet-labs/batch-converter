using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using Zlet.FolderConverter.App.Localization;

namespace Zlet.FolderConverter.App.Settings;

public sealed record SettingsSaveResult(bool Success, string? ErrorCode = null)
{
    public static SettingsSaveResult Saved { get; } = new(true);
}

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

    public SettingsSaveResult TrySaveLanguage(string language)
    {
        if (!AppLanguage.IsSupported(language)) return new(false, "invalid_language");
        JsonObject root;
        try
        {
            root = File.Exists(SettingsPath)
                ? JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject()
                : new JsonObject();
            root["language"] = AppLanguage.Normalize(language);
            var directory = Path.GetDirectoryName(SettingsPath);
            if (string.IsNullOrWhiteSpace(directory)) return new(false, "invalid_settings_path");
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, SettingsPath, true);
                return SettingsSaveResult.Saved;
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (JsonException)
        {
            return TryReplaceCorruptedSettings(language);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or NotSupportedException)
        {
            return new(false, "settings_write_failed");
        }
    }

    private SettingsSaveResult TryReplaceCorruptedSettings(string language)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (string.IsNullOrWhiteSpace(directory)) return new(false, "invalid_settings_path");
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");
            try
            {
                var root = new JsonObject { ["language"] = AppLanguage.Normalize(language) };
                File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, SettingsPath, true);
                return SettingsSaveResult.Saved;
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or NotSupportedException)
        {
            return new(false, "settings_write_failed");
        }
    }
}
