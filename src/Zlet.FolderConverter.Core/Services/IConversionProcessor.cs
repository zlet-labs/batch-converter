using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionProcessor
{
    Task<ConversionSummary> ProcessAsync(
        IReadOnlyList<PlannedOperation> operations,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken);
}
