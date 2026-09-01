namespace Zlet.FolderConverter.Core.Services;

internal interface IConversionBatchLifecycle
{
    Task BeginBatchAsync(CancellationToken cancellationToken);

    Task EndBatchAsync();
}
