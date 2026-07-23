using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class ConversionPlanner(IConversionAdapterResolver adapterResolver) : IConversionPlanner
{
    public IReadOnlyList<PlannedOperation> CreatePlan(
        ScanResult scanResult,
        string rootPath,
        OutputFormat outputFormat = OutputFormat.TXT)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        var targetRootPath = Path.Combine(fullRootPath, "_converted");

        return scanResult.Files
            .Select(file => CreateOperation(file, targetRootPath, outputFormat))
            .ToArray();
    }

    private PlannedOperation CreateOperation(
        ScannedFile file,
        string targetRootPath,
        OutputFormat outputFormat)
    {
        var targetExtension = DocumentFormatDetector.GetTargetExtension(file.Format, outputFormat);
        var targetRelativePath = Path.ChangeExtension(file.RelativePath, targetExtension);
        var targetPath = Path.Combine(targetRootPath, targetRelativePath);
        var adapter = adapterResolver.Resolve(file.Format);
        var adapterAvailable = adapter?.IsAvailable == true;

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            return new PlannedOperation(
                file.SourcePath,
                file.RelativePath,
                file.Format,
                targetExtension,
                targetPath,
                adapterAvailable,
                OperationStatus.Conflict,
                "Target file already exists and will not be overwritten.",
                targetRootPath);
        }

        if (!adapterAvailable)
        {
            return new PlannedOperation(
                file.SourcePath,
                file.RelativePath,
                file.Format,
                targetExtension,
                targetPath,
                false,
                OperationStatus.Unsupported,
                adapter?.AvailabilityMessage ?? "No confirmed embedded converter is available for this format.",
                targetRootPath);
        }

        return new PlannedOperation(
            file.SourcePath,
            file.RelativePath,
            file.Format,
            targetExtension,
            targetPath,
            true,
            OperationStatus.Ready,
            "Ready to convert with a confirmed embedded adapter.",
            targetRootPath);
    }
}
