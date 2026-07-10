using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionAdapter
{
    DocumentFormat SourceFormat { get; }

    string TargetExtension { get; }

    bool IsAvailable { get; }

    string AvailabilityMessage { get; }

    Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken);
}
