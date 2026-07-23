using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionProcessor
{
    Task<ConversionSummary> ProcessAsync(
        IReadOnlyList<PlannedOperation> operations,
        CancellationToken cancellationToken);
}
