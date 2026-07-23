namespace Zlet.FolderConverter.Core.Models;

public sealed record FormatCapability(
    SourceFormat SourceFormat,
    IReadOnlyList<ConversionTarget> AllowedTargets,
    ConversionTarget DefaultTarget)
{
    public string DisplayName => SourceFormat.ToDisplayName();

    public bool Supports(ConversionTarget target) => AllowedTargets.Contains(target);
}
