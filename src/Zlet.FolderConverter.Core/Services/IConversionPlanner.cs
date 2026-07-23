using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionPlanner
{
    IReadOnlyList<PlannedOperation> CreatePlan(
        ScanResult scanResult,
        string rootPath,
        OutputFormat outputFormat = OutputFormat.TXT);
}
