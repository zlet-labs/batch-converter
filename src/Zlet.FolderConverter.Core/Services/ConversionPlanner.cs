using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class ConversionPlanner(IConversionAdapterResolver adapterResolver) : IConversionPlanner
{
    public IReadOnlyList<PlannedOperation> CreatePlan(
        ScanResult scanResult,
        string rootPath,
        RuleSet ruleSet) =>
        CreatePlan(scanResult, rootPath, Path.Combine(Path.GetFullPath(rootPath), "_converted"), ruleSet);

    public IReadOnlyList<PlannedOperation> CreatePlan(
        ScanResult scanResult,
        string sourceRootPath,
        string outputRootPath,
        RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootPath);

        var fullRootPath = Path.GetFullPath(sourceRootPath);
        var targetRootPath = Path.GetFullPath(outputRootPath);

        return scanResult.Files
            .Select(file => CreateOperation(file, fullRootPath, targetRootPath, ruleSet.GetRule(file.Format)))
            .ToArray();
    }

    private PlannedOperation CreateOperation(
        ScannedFile file,
        string sourceRootPath,
        string targetRootPath,
        ConversionRule rule)
    {
        if (rule.Target == ConversionTarget.Skip)
        {
            return Create(
                file,
                rule.Target,
                string.Empty,
                string.Empty,
                false,
                OperationStatus.Skipped,
                "Файл не будет изменён.",
                targetRootPath,
                sourceRootPath);
        }

        if (!FormatCapabilityCatalog.Get(file.Format).Supports(rule.Target))
        {
            return Create(
                file,
                rule.Target,
                rule.Target.ToExtension(),
                string.Empty,
                false,
                OperationStatus.Unsupported,
                "Выбранное преобразование не поддерживается.",
                targetRootPath,
                sourceRootPath);
        }

        var targetExtension = rule.Target == ConversionTarget.Copy
            ? Path.GetExtension(file.RelativePath)
            : rule.Target.ToExtension();
        if (!OutputPathGuard.TryBuildTargetPath(
                sourceRootPath,
                targetRootPath,
                file.SourcePath,
                file.RelativePath,
                targetExtension,
                out var targetPath))
        {
            return Create(
                file,
                rule.Target,
                targetExtension,
                string.Empty,
                false,
                OperationStatus.Failed,
                "Недопустимый путь результата.",
                targetRootPath,
                sourceRootPath);
        }

        var adapter = adapterResolver.Resolve(file.Format, rule.Target);
        var adapterAvailable = adapter?.IsAvailable == true;

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            return Create(
                file,
                rule.Target,
                targetExtension,
                targetPath,
                adapterAvailable,
                OperationStatus.Conflict,
                "Файл результата уже существует.",
                targetRootPath,
                sourceRootPath);
        }

        if (adapter is null)
        {
            return Create(
                file,
                rule.Target,
                targetExtension,
                targetPath,
                false,
                OperationStatus.Unsupported,
                "Выбранное преобразование не поддерживается.",
                targetRootPath,
                sourceRootPath);
        }

        if (!adapterAvailable)
        {
            return Create(
                file,
                rule.Target,
                targetExtension,
                targetPath,
                false,
                OperationStatus.EngineUnavailable,
                adapter.AvailabilityMessage,
                targetRootPath,
                sourceRootPath);
        }

        return Create(
            file,
            rule.Target,
            targetExtension,
            targetPath,
            true,
            OperationStatus.Ready,
            rule.Target == ConversionTarget.Copy
                ? "Будет скопирован без изменений."
                : "Готово к преобразованию.",
            targetRootPath,
            sourceRootPath);
    }

    private static PlannedOperation Create(
        ScannedFile file,
        ConversionTarget target,
        string targetExtension,
        string targetPath,
        bool adapterAvailable,
        OperationStatus status,
        string message,
        string outputRootPath,
        string sourceRootPath) =>
        new(
            file.SourcePath,
            file.RelativePath,
            file.Format,
            target,
            targetExtension,
            targetPath,
            adapterAvailable,
            status,
            message,
            outputRootPath,
            sourceRootPath,
            file.SizeBytes);
}
