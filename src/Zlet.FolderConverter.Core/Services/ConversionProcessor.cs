using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class ConversionProcessor(IConversionAdapterResolver adapterResolver) : IConversionProcessor
{
    public async Task<ConversionSummary> ProcessAsync(
        IReadOnlyList<PlannedOperation> operations,
        CancellationToken cancellationToken)
    {
        var results = new List<ConversionResult>(operations.Count);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Status != OperationStatus.Ready)
            {
                results.Add(new ConversionResult(operation, operation.Status, operation.Message));
                continue;
            }

            var adapter = adapterResolver.Resolve(operation.SourceFormat);
            if (adapter?.IsAvailable != true)
            {
                results.Add(new ConversionResult(operation, OperationStatus.Unsupported, "Конвертация недоступна."));
                continue;
            }

            try
            {
                results.Add(await adapter.ConvertAsync(operation, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(new ConversionResult(
                    operation,
                    OperationStatus.Failed,
                    "Не удалось обработать файл.",
                    exception));
            }
        }

        return new ConversionSummary(
            results.Count(result => result.Status == OperationStatus.Succeeded),
            results.Count(result => result.Status == OperationStatus.Conflict),
            results.Count(result => result.Status == OperationStatus.Failed),
            results.Count(result => result.Status == OperationStatus.Unsupported),
            results);
    }
}
