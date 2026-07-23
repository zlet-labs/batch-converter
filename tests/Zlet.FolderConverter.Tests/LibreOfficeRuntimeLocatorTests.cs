using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class LibreOfficeRuntimeLocatorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-lo-locator-tests",
        Guid.NewGuid().ToString("N"));

    public LibreOfficeRuntimeLocatorTests() => Directory.CreateDirectory(_rootPath);

    [Fact]
    public void Locate_prefers_environment_before_local_bundled_and_explicit_paths()
    {
        var environmentRuntime = Path.Combine(_rootPath, "environment");
        var localRuntime = Path.Combine(_rootPath, "local");
        var explicitRuntime = Path.Combine(_rootPath, "explicit");
        var appBase = Path.Combine(_rootPath, "app");
        var executable = CreateRuntime(environmentRuntime);
        CreateRuntime(localRuntime);
        CreateRuntime(explicitRuntime);
        CreateRuntime(Path.Combine(appBase, "runtime", "libreoffice"));
        var settingsPath = Path.Combine(_rootPath, "ZletFolderConverter.local.json");
        File.WriteAllText(
            settingsPath,
            $$"""{"libreOfficePath":"{{EscapeJson(localRuntime)}}"}""");
        var locator = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions
            {
                ExplicitRuntimePath = explicitRuntime,
                LocalSettingsPath = settingsPath
            },
            appBase,
            () => environmentRuntime);

        var result = locator.Locate();

        Assert.True(result.IsAvailable);
        Assert.Equal(executable, result.ExecutablePath);
    }

    [Fact]
    public void Locate_reads_ignored_local_setting_from_development_working_directory()
    {
        var appBase = Path.Combine(_rootPath, "app");
        var runtime = Path.Combine(_rootPath, "local-runtime");
        var executable = CreateRuntime(runtime);
        var settingsPath = Path.Combine(_rootPath, "ZletFolderConverter.local.json");
        File.WriteAllText(
            settingsPath,
            $$"""{"libreOfficePath":"{{EscapeJson(runtime)}}"}""");

        var result = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions(),
            appBase,
            () => null,
            _rootPath).Locate();

        Assert.True(result.IsAvailable);
        Assert.Equal(executable, result.ExecutablePath);
    }

    [Fact]
    public void Locate_finds_bundled_portable_runtime()
    {
        var appBase = Path.Combine(_rootPath, "app");
        var runtime = Path.Combine(appBase, "runtime", "libreoffice");
        var executable = CreateRuntime(runtime);

        var result = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions(),
            appBase,
            () => null,
            Path.Combine(_rootPath, "working")).Locate();

        Assert.True(result.IsAvailable);
        Assert.Equal(executable, result.ExecutablePath);
    }

    [Fact]
    public void Locate_prefers_console_launcher_for_synchronous_automation()
    {
        var runtime = Path.Combine(_rootPath, "runtime");
        var program = Path.Combine(runtime, "program");
        Directory.CreateDirectory(program);
        var guiExecutable = Path.Combine(program, "soffice.exe");
        var consoleExecutable = Path.Combine(program, "soffice.com");
        File.WriteAllText(guiExecutable, "synthetic");
        File.WriteAllText(consoleExecutable, "synthetic");

        var fromDirectory = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions(),
            Path.Combine(_rootPath, "app"),
            () => runtime,
            Path.Combine(_rootPath, "working")).Locate();
        var fromGuiExecutable = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions(),
            Path.Combine(_rootPath, "app"),
            () => guiExecutable,
            Path.Combine(_rootPath, "working")).Locate();

        Assert.Equal(consoleExecutable, fromDirectory.ExecutablePath);
        Assert.Equal(consoleExecutable, fromGuiExecutable.ExecutablePath);
    }

    [Fact]
    public void Locate_uses_system_installation_only_for_development_self_check()
    {
        var systemRuntime = Path.Combine(_rootPath, "system");
        var executable = CreateRuntime(systemRuntime);
        var appBase = Path.Combine(_rootPath, "app");
        var withoutOptIn = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions(),
            appBase,
            () => null,
            Path.Combine(_rootPath, "working"),
            [systemRuntime]).Locate();
        var withOptIn = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions
            {
                IncludeSystemInstallationsForDevelopment = true
            },
            appBase,
            () => null,
            Path.Combine(_rootPath, "working"),
            [systemRuntime]).Locate();

        Assert.False(withoutOptIn.IsAvailable);
        Assert.True(withOptIn.IsAvailable);
        Assert.Equal(executable, withOptIn.ExecutablePath);
    }

    [Fact]
    public void Locate_returns_unavailable_without_runtime()
    {
        var result = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions
            {
                ExplicitRuntimePath = Path.Combine(_rootPath, "missing"),
                LocalSettingsPath = Path.Combine(_rootPath, "missing.json")
            },
            Path.Combine(_rootPath, "app"),
            () => null,
            Path.Combine(_rootPath, "working")).Locate();

        Assert.False(result.IsAvailable);
        Assert.Equal(string.Empty, result.ExecutablePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static string CreateRuntime(string root)
    {
        var program = Path.Combine(root, "program");
        Directory.CreateDirectory(program);
        var executable = Path.Combine(program, "soffice.exe");
        File.WriteAllText(executable, "synthetic");
        return executable;
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal);
}
