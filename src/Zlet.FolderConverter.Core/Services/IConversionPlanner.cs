using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IConversionPlanner
{
    IReadOnlyList<PlannedOperation> CreatePlan(
        ScanResult scanResult,
        string rootPath,
        RuleSet ruleSet);

    IReadOnlyList<PlannedOperation> CreatePlan(
        ScanResult scanResult,
        string sourceRootPath,
        string outputRootPath,
        RuleSet ruleSet) =>
        CreatePlan(scanResult, sourceRootPath, ruleSet);
}
