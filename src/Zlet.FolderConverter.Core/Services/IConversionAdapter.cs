using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionAdapter
{
    bool IsAvailable { get; }

    string AvailabilityMessage { get; }

    bool CanConvert(SourceFormat sourceFormat, ConversionTarget target);

    Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken);

    Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        IProgress<int>? progress,
        CancellationToken cancellationToken) => ConvertAsync(operation, cancellationToken);
}
