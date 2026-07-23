using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class DefaultConversionAdapterResolver : IConversionAdapterResolver
{
    private readonly IReadOnlyDictionary<DocumentFormat, IConversionAdapter> _adapters;

    public DefaultConversionAdapterResolver()
        : this(CreateDefaultAdapters())
    {
    }

    public DefaultConversionAdapterResolver(IEnumerable<IConversionAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(adapter => adapter.SourceFormat);
    }

    public IConversionAdapter? Resolve(DocumentFormat sourceFormat)
    {
        return _adapters.GetValueOrDefault(sourceFormat);
    }

    private static IConversionAdapter[] CreateDefaultAdapters()
    {
        return
        [
            new JsonConversionAdapter(new OutputResultValidator()),
            new UnsupportedConversionAdapter(
                DocumentFormat.Doc,
                ".docx",
                "DOC to DOCX is unsupported until an embedded converter passes license and synthetic validation."),
            new UnsupportedConversionAdapter(
                DocumentFormat.Xls,
                ".xlsx",
                "XLS to XLSX is unsupported until an embedded converter passes license and synthetic validation."),
            new UnsupportedConversionAdapter(
                DocumentFormat.Ppt,
                ".pptx",
                "PPT to PPTX is unsupported until an embedded converter passes license and synthetic validation.")
        ];
    }
}
