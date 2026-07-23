using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IMicrosoftOfficeCapabilityDetector
{
    IReadOnlyList<OfficeApplicationAvailability> Detect();
}
