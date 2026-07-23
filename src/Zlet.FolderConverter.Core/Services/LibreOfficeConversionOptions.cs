namespace Zlet.FolderConverter.Core.Services;

public sealed record LibreOfficeConversionOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public string? ExplicitRuntimePath { get; init; }

    public string? TemporaryRootPath { get; init; }

    public string? LocalSettingsPath { get; init; }
}
