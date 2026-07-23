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
                OperationStatus.Converting));

            ConversionResult result;
            var adapter = adapterResolver.Resolve(operation.SourceFormat, operation.Target);
            if (adapter?.IsAvailable != true)
            {
                result = new ConversionResult(
                    operation,
                    FormatCapabilityCatalog.RequiresLibreOffice(operation.SourceFormat, operation.Target)
                        ? OperationStatus.EngineUnavailable
                        : OperationStatus.Unsupported,
                    FormatCapabilityCatalog.RequiresLibreOffice(operation.SourceFormat, operation.Target)
                        ? "LibreOffice не найден в portable package."
                        : "Преобразование недоступно.");
            }
            else
            {
                try
                {
                    result = await adapter.ConvertAsync(operation, cancellationToken);
                }
                catch (OperationCanceledException)
                {
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
                result));
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
