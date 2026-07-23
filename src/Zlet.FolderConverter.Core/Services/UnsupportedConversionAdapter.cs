using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class UnsupportedConversionAdapter(
    SourceFormat sourceFormat,
    ConversionTarget target,
    string availabilityMessage) : IConversionAdapter
{
    public bool IsAvailable => false;

    public string AvailabilityMessage { get; } = availabilityMessage;

    public bool CanConvert(SourceFormat candidateSource, ConversionTarget candidateTarget) =>
        candidateSource == sourceFormat && candidateTarget == target;

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ConversionResult(
            operation,
            OperationStatus.Unsupported,
            AvailabilityMessage));
    }
}
