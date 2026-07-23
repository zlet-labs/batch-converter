namespace Zlet.FolderConverter.Core.Services;

public interface ILibreOfficeProcessRunner
{
    Task<LibreOfficeProcessResult> RunAsync(
        LibreOfficeProcessRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
