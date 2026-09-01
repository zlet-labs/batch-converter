using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class ConversionProcessor(IConversionAdapterResolver adapterResolver) : IConversionProcessor
{
    public async Task<ConversionSummary> ProcessAsync(
        IReadOnlyList<PlannedOperation> operations,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<ConversionResult>(operations.Count);
        var readyTotal = operations.Count(operation => operation.Status == OperationStatus.Ready);
        var completedReady = 0;
        var batchLifecycle = adapterResolver as IConversionBatchLifecycle;

        if (batchLifecycle is not null)
        {
            await batchLifecycle.BeginBatchAsync(cancellationToken);
        }

        try
        {
            foreach (var operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operation.Status != OperationStatus.Ready)
                {
                    results.Add(new ConversionResult(operation, operation.Status, operation.Message));
                    continue;
                }

                progress?.Report(new ConversionProgress(
                    completedReady,
                    readyTotal,
                    operation.RelativePath,
                    OperationStatus.Converting,
                    OperationPercent: 10));

                ConversionResult result;
                var adapter = adapterResolver.Resolve(operation.SourceFormat, operation.Target);
                if (adapter?.IsAvailable != true)
                {
                    result = new ConversionResult(
                        operation,
                        adapter is null
                            ? OperationStatus.Unsupported
                            : OperationStatus.EngineUnavailable,
                        adapter?.AvailabilityMessage ?? "Преобразование недоступно.");
                }
                else
                {
                    try
                    {
                        result = await adapter.ConvertAsync(operation, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        var cancelled = new ConversionResult(
                            operation,
                            OperationStatus.Cancelled,
                            "Отменено пользователем.");
                        progress?.Report(new ConversionProgress(
                            completedReady,
                            readyTotal,
                            operation.RelativePath,
                            OperationStatus.Cancelled,
                            cancelled));
                        throw;
                    }
                    catch
                    {
                        result = new ConversionResult(
                            operation,
                            OperationStatus.Failed,
                            "Не удалось обработать файл.",
                            new ConversionDiagnostic("unexpected_adapter_failure"));
                    }
                }

                results.Add(result);
                completedReady++;
                progress?.Report(new ConversionProgress(
                    completedReady,
                    readyTotal,
                    operation.RelativePath,
                    result.Status,
                    result,
                    result.Status == OperationStatus.Succeeded ? 100 : null));
            }
        }
        finally
        {
            if (batchLifecycle is not null)
            {
                await batchLifecycle.EndBatchAsync();
            }
        }

        return new ConversionSummary(
            results.Count(result => result.Status == OperationStatus.Succeeded),
            results.Count(result => result.Status == OperationStatus.Conflict),
            results.Count(result => result.Status == OperationStatus.Failed),
            results.Count(result => result.Status == OperationStatus.Skipped),
            results.Count(result => result.Status == OperationStatus.EngineUnavailable),
            results.Count(result => result.Status == OperationStatus.Unsupported),
            results);
    }
}
