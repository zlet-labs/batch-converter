namespace Zlet.FolderConverter.Core.Models;

public sealed record ConversionSummary(
    int Succeeded,
    int Skipped,
    int Failed,
    IReadOnlyList<ConversionResult> Results);
