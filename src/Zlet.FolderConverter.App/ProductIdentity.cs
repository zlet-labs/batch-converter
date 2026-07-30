using System.Reflection;

namespace Zlet.FolderConverter.App;

public static class ProductIdentity
{
    private static readonly Assembly Assembly = typeof(ProductIdentity).Assembly;

    public static string Name =>
        Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Zlet Batch Converter";

    public static string Version =>
        (Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
             ?.InformationalVersion ?? "0.0.1")
        .Split('+')[0];

    public static string ResultZipFileName =>
        $"ZletBatchConverter-v{Version}-results.zip";
}
