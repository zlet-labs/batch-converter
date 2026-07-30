using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class DefaultConversionAdapterResolver : IConversionAdapterResolver
{
    private readonly IReadOnlyList<IConversionAdapter> _adapters;

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
    }

    public DefaultConversionAdapterResolver(IEnumerable<IConversionAdapter> adapters)
    {
        _adapters = adapters.ToArray();
    }

    public IConversionAdapter? Resolve(SourceFormat sourceFormat, ConversionTarget target) =>
        _adapters.FirstOrDefault(adapter => adapter.CanConvert(sourceFormat, target));

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
