using Zlet.FolderConverter.Core.Models;
using System.Runtime.InteropServices;

namespace Zlet.FolderConverter.Core.Services;

public sealed class MicrosoftOfficeCapabilityDetector : IMicrosoftOfficeCapabilityDetector
{
    private readonly IOfficeProgIdResolver _resolver;

    public MicrosoftOfficeCapabilityDetector()
        : this(new OfficeProgIdResolver())
    {
    }

    internal MicrosoftOfficeCapabilityDetector(IOfficeProgIdResolver resolver)
    {
        _resolver = resolver;
    }

    public IReadOnlyList<OfficeApplicationAvailability> Detect() =>
        Enum.GetValues<OfficeApplicationKind>()
            .Select(application => new OfficeApplicationAvailability(
                application,
                _resolver.IsRegistered(application)))
            .ToArray();
}

internal interface IOfficeProgIdResolver
{
    bool IsRegistered(OfficeApplicationKind application);
}

internal sealed class OfficeProgIdResolver : IOfficeProgIdResolver
{
    public bool IsRegistered(OfficeApplicationKind application)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return Type.GetTypeFromProgID(GetProgId(application), throwOnError: false) is not null;
        }
        catch (Exception exception) when (exception is COMException
                                           or PlatformNotSupportedException
                                           or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetProgId(OfficeApplicationKind application) => application switch
    {
        OfficeApplicationKind.Word => "Word.Application",
        OfficeApplicationKind.Excel => "Excel.Application",
        OfficeApplicationKind.PowerPoint => "PowerPoint.Application",
        _ => throw new ArgumentOutOfRangeException(nameof(application), application, null)
    };
}
