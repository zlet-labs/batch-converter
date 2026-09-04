using System.Runtime.InteropServices;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.Settings;

public static class DiagnosticsText
{
    // Deliberate allowlist: no paths, environment variables, document data or logs.
    public static string Create(LocalizationService localization, IReadOnlyList<OfficeApplicationAvailability> office) =>
        string.Join(Environment.NewLine, new[]
        {
            $"{ProductIdentity.Name} {ProductIdentity.Version}",
            localization.Format("DiagnosticsWindows", Environment.OSVersion.Version),
            localization.Format("DiagnosticsOsArch", RuntimeInformation.OSArchitecture),
            localization.Format("DiagnosticsAppArch", RuntimeInformation.ProcessArchitecture),
            localization.Format("DiagnosticsLanguage", localization.Language)
        }.Concat(Enum.GetValues<OfficeApplicationKind>().Select(kind =>
            $"{kind}: {localization.Get(office.Any(item => item.Application == kind && item.IsAvailable) ? "OfficeAvailable" : "OfficeUnavailable")}")));
}
