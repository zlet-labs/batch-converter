using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class UnsupportedConversionAdapter(
    DocumentFormat sourceFormat,
    string targetExtension,
    string availabilityMessage) : IConversionAdapter
{
    public DocumentFormat SourceFormat { get; } = sourceFormat;

    public string TargetExtension { get; } = targetExtension;

    public bool IsAvailable => false;

    public string AvailabilityMessage { get; } = availabilityMessage;

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
