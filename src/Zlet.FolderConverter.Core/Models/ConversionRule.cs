namespace Zlet.FolderConverter.Core.Models;

public sealed record ConversionRule(
    SourceFormat SourceFormat,
    ConversionTarget Target);
