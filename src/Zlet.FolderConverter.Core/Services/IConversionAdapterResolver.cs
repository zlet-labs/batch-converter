using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionAdapterResolver
{
    IConversionAdapter? Resolve(DocumentFormat sourceFormat);
}
