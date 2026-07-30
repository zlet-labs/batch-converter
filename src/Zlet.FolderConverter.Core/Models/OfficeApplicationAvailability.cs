namespace Zlet.FolderConverter.Core.Models;

public sealed record OfficeApplicationAvailability(
    OfficeApplicationKind Application,
    bool IsAvailable)
{
    public string StatusText =>
        $"{Application.ToShortDisplayName()}: {(IsAvailable ? "доступен" : "не установлен")}";
}
