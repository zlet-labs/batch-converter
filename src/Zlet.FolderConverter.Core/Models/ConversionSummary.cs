namespace Zlet.FolderConverter.Core.Models;

public sealed record ConversionSummary(
    int Succeeded,
    int Conflicts,
    int Failed,
    int Unsupported,
    IReadOnlyList<ConversionResult> Results);
