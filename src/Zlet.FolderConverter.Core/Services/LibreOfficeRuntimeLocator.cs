using System.Text.Json;

namespace Zlet.FolderConverter.Core.Services;

public sealed class LibreOfficeRuntimeLocator(
    LibreOfficeConversionOptions? options = null,
    string? applicationBasePath = null) : ILibreOfficeRuntimeLocator
{
    private readonly LibreOfficeConversionOptions _options = options ?? new LibreOfficeConversionOptions();
    private readonly string _applicationBasePath = applicationBasePath ?? AppContext.BaseDirectory;

    public LibreOfficeRuntimeLocation Locate()
    {
        foreach (var candidate in GetCandidates())
        {
            var executable = ResolveExecutable(candidate);
            if (executable is not null)
            {
                return new LibreOfficeRuntimeLocation(true, executable);
            }
        }

        return new LibreOfficeRuntimeLocation(false);
    }

    private IEnumerable<string?> GetCandidates()
    {
        yield return _options.ExplicitRuntimePath;
        yield return Environment.GetEnvironmentVariable("ZLET_LIBREOFFICE_PATH");
        yield return ReadLocalSetting();
        yield return Path.Combine(_applicationBasePath, "runtime", "libreoffice");
    }

    private string? ReadLocalSetting()
    {
        var settingsPath = _options.LocalSettingsPath
            ?? Path.Combine(_applicationBasePath, "ZletFolderConverter.local.json");
        try
        {
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            return document.RootElement.TryGetProperty("libreOfficePath", out var value)
                ? value.GetString()
                : null;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or JsonException)
        {
            return null;
        }
    }

    private static string? ResolveExecutable(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            var fullCandidate = Path.GetFullPath(candidate);
            var possiblePaths = File.Exists(fullCandidate)
                ? [fullCandidate]
                : new[]
                {
                    Path.Combine(fullCandidate, "program", "soffice.exe"),
                    Path.Combine(fullCandidate, "soffice.exe")
                };
            return possiblePaths.FirstOrDefault(path =>
                File.Exists(path)
                && Path.GetFileName(path).Equals("soffice.exe", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or IOException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return null;
        }
    }
}
