using System.Reflection;

namespace Zlet.FolderConverter.App;

public static class ProductIdentity
{
    private static readonly Assembly Assembly = typeof(ProductIdentity).Assembly;

    public static string Name =>
        Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Zlet Converter";

    public static string ExecutableName =>
        Assembly.GetName().Name ?? "ZletConverter";

    public static string Version =>
        (Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
             ?.InformationalVersion ?? "0.0.2")
        .Split('+')[0];

    public static string ResultZipFileName =>
        $"{ExecutableName}-v{Version}-results.zip";
}
