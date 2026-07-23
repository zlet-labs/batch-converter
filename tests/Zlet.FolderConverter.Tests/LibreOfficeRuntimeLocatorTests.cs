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
    public void Locate_finds_explicit_runtime_directory()
    {
        var executable = CreateRuntime(_rootPath);
        var locator = new LibreOfficeRuntimeLocator(
            new LibreOfficeConversionOptions { ExplicitRuntimePath = _rootPath },
            Path.Combine(_rootPath, "app"));

        var result = locator.Locate();

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
            appBase).Locate();

        Assert.True(result.IsAvailable);
        Assert.Equal(executable, result.ExecutablePath);
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
            Path.Combine(_rootPath, "app")).Locate();

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
}
