using System.Text.Json;

namespace Zlet.FolderConverter.Core.Services;

public sealed class LibreOfficeRuntimeLocator(
    LibreOfficeConversionOptions? options = null,
    string? applicationBasePath = null,
    Func<string?>? environmentPathProvider = null,
    string? currentDirectoryPath = null,
    IReadOnlyList<string>? developmentSystemPaths = null) : ILibreOfficeRuntimeLocator
{
    private readonly LibreOfficeConversionOptions _options = options ?? new LibreOfficeConversionOptions();
    private readonly string _applicationBasePath = applicationBasePath ?? AppContext.BaseDirectory;
    private readonly Func<string?> _environmentPathProvider = environmentPathProvider
        ?? (() => Environment.GetEnvironmentVariable("ZLET_LIBREOFFICE_PATH"));
    private readonly string _currentDirectoryPath = currentDirectoryPath
        ?? Environment.CurrentDirectory;
    private readonly IReadOnlyList<string> _developmentSystemPaths = developmentSystemPaths
        ??
        [
            @"C:\Program Files\LibreOffice",
            @"C:\Program Files (x86)\LibreOffice"
        ];

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
        yield return _environmentPathProvider();
        yield return ReadLocalSetting();
        yield return Path.Combine(_applicationBasePath, "runtime", "libreoffice");
        yield return _options.ExplicitRuntimePath;

        if (_options.IncludeSystemInstallationsForDevelopment)
        {
            foreach (var systemPath in _developmentSystemPaths)
            {
                yield return systemPath;
            }
        }
    }

    private string? ReadLocalSetting()
    {
        foreach (var settingsPath in GetLocalSettingsPaths())
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (document.RootElement.TryGetProperty("libreOfficePath", out var value)
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString();
                }
            }
            catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or JsonException)
            {
                // An unreadable local development override must not block bundled lookup.
            }
        }

        return null;
    }

    private IEnumerable<string> GetLocalSettingsPaths()
    {
        if (!string.IsNullOrWhiteSpace(_options.LocalSettingsPath))
        {
            yield return _options.LocalSettingsPath;
            yield break;
        }

        var appLocalSettings = Path.Combine(
            _applicationBasePath,
            "ZletFolderConverter.local.json");
        yield return appLocalSettings;

        var workingDirectorySettings = Path.Combine(
            _currentDirectoryPath,
            "ZletFolderConverter.local.json");
        if (!string.Equals(
                appLocalSettings,
                workingDirectorySettings,
                StringComparison.OrdinalIgnoreCase))
        {
            yield return workingDirectorySettings;
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
            if (File.Exists(fullCandidate))
            {
                if (!IsSofficeExecutable(fullCandidate))
                {
                    return null;
                }

                var consoleExecutable = Path.Combine(
                    Path.GetDirectoryName(fullCandidate)!,
                    "soffice.com");
                return File.Exists(consoleExecutable)
                    ? consoleExecutable
                    : fullCandidate;
            }

            var possiblePaths = new[]
            {
                Path.Combine(fullCandidate, "program", "soffice.com"),
                Path.Combine(fullCandidate, "program", "soffice.exe"),
                Path.Combine(fullCandidate, "soffice.com"),
                Path.Combine(fullCandidate, "soffice.exe")
            };
            return possiblePaths.FirstOrDefault(File.Exists);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or IOException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsSofficeExecutable(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals("soffice.exe", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("soffice.com", StringComparison.OrdinalIgnoreCase);
    }
}
