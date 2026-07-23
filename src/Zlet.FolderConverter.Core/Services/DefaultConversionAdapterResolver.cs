using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class DefaultConversionAdapterResolver : IConversionAdapterResolver
{
    private readonly IReadOnlyList<IConversionAdapter> _adapters;

    public DefaultConversionAdapterResolver()
        : this(CreateDefaultAdapters())
    {
    }

    public DefaultConversionAdapterResolver(IEnumerable<IConversionAdapter> adapters)
    {
        _adapters = adapters.ToArray();
    }

    public IConversionAdapter? Resolve(SourceFormat sourceFormat, ConversionTarget target) =>
        _adapters.FirstOrDefault(adapter => adapter.CanConvert(sourceFormat, target));

    private static IConversionAdapter[] CreateDefaultAdapters()
    {
        var options = new LibreOfficeConversionOptions();
        var validator = new OutputResultValidator();
        return
        [
            new JsonConversionAdapter(validator),
            new LibreOfficeConversionAdapter(
                new LibreOfficeRuntimeLocator(options),
                new LibreOfficeProcessRunner(),
                validator,
                options)
        ];
    }
}
