namespace Zlet.FolderConverter.Tests;

public sealed class InstallerPackagingTests
{
    [Fact]
    public void Installer_is_win_x64_per_user_and_uses_central_product_version()
    {
        var root = FindRepositoryRoot();
        var definitionPath = Path.Combine(root, "installer", "ZletBatchConverter.iss");
        var buildScriptPath = Path.Combine(root, "scripts", "build-installer.ps1");

        Assert.True(File.Exists(definitionPath));
        Assert.True(File.Exists(buildScriptPath));

        var definition = File.ReadAllText(definitionPath);
        var buildScript = File.ReadAllText(buildScriptPath);

        Assert.Contains("ArchitecturesAllowed=x64compatible", definition);
        Assert.Contains("ArchitecturesInstallIn64BitMode=x64compatible", definition);
        Assert.Contains("PrivilegesRequired=lowest", definition);
        Assert.Contains(@"DefaultDirName={localappdata}\Programs\Zlet Batch Converter", definition);
        Assert.Contains("Name: \"desktopicon\"", definition);
        Assert.Contains(@"{autoprograms}\Zlet Batch Converter", definition);
        Assert.Contains("UninstallDisplayIcon={app}\\{#AppExeName}", definition);

        Assert.Contains("Get-ProjectProperty \"ZletProductVersion\"", buildScript);
        Assert.Contains("Get-ProjectProperty \"ZletPortableRuntimeIdentifier\"", buildScript);
        Assert.Contains("publish-portable.ps1", buildScript);
        Assert.Contains("ISCC.exe", buildScript);
        Assert.DoesNotContain("<ZletProductVersion>", definition);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FolderConverter.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
