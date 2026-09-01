using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class DefaultConversionAdapterResolver
    : IConversionAdapterResolver, IConversionBatchLifecycle
{
    private readonly IReadOnlyList<IConversionAdapter> _adapters;
    private readonly IMicrosoftOfficeWorkerRunner? _workerRunner;

    public DefaultConversionAdapterResolver()
        : this(
            new MicrosoftOfficeCapabilityDetector(),
            new MicrosoftOfficeWorkerProcessRunner())
    {
    }

    public DefaultConversionAdapterResolver(
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner workerRunner)
        : this(CreateDefaultAdapters(capabilityDetector, workerRunner))
    {
        _workerRunner = workerRunner;
    }

    public DefaultConversionAdapterResolver(IEnumerable<IConversionAdapter> adapters)
    {
        _adapters = adapters.ToArray();
    }

    public IConversionAdapter? Resolve(SourceFormat sourceFormat, ConversionTarget target) =>
        _adapters.FirstOrDefault(adapter => adapter.CanConvert(sourceFormat, target));

    Task IConversionBatchLifecycle.BeginBatchAsync(CancellationToken cancellationToken) =>
        _workerRunner?.BeginBatchAsync(cancellationToken) ?? Task.CompletedTask;

    Task IConversionBatchLifecycle.EndBatchAsync() =>
        _workerRunner?.EndBatchAsync() ?? Task.CompletedTask;

    private static IConversionAdapter[] CreateDefaultAdapters(
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner workerRunner)
    {
        var validator = new OutputResultValidator();
        return
        [
            new JsonConversionAdapter(validator),
            new SafeFileCopyAdapter(validator),
            new MicrosoftOfficeConversionAdapter(
                OfficeApplicationKind.Word,
                capabilityDetector,
                workerRunner,
                validator,
                temporaryRoot: null),
            new MicrosoftOfficeConversionAdapter(
                OfficeApplicationKind.Excel,
                capabilityDetector,
                workerRunner,
                validator,
                temporaryRoot: null),
            new MicrosoftOfficeConversionAdapter(
                OfficeApplicationKind.PowerPoint,
                capabilityDetector,
                workerRunner,
                validator,
                temporaryRoot: null)
        ];
    }
}
