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
            .SelectMany(file => CreateOperations(
                file,
                fullRootPath,
                targetRootPath,
                ruleSet.GetRule(file.Format)))
            .ToArray();
    }

    private IEnumerable<PlannedOperation> CreateOperations(
        ScannedFile file,
        string sourceRootPath,
        string targetRootPath,
        ConversionRule rule)
    {
        if (rule.Target is ConversionTarget.Csv or ConversionTarget.Tsv
            && file.Format is SourceFormat.Xls or SourceFormat.Xlsx)
        {
            return CreateWorksheetOperations(file, sourceRootPath, targetRootPath, rule);
        }

        return [CreateOperation(file, sourceRootPath, targetRootPath, rule)];
    }

    private IReadOnlyList<PlannedOperation> CreateWorksheetOperations(
        ScannedFile file,
        string sourceRootPath,
        string targetRootPath,
        ConversionRule rule)
    {
        var targetExtension = rule.Target.ToExtension();
        if (!string.IsNullOrWhiteSpace(file.WorksheetInspectionErrorCode))
        {
            return
            [
                CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    string.Empty,
                    false,
                    OperationStatus.Failed,
                    "Не удалось прочитать список листов Excel.",
                    targetRootPath,
                    sourceRootPath,
                    worksheetName: string.Empty,
                    WorksheetVisibility.Visible,
                    worksheetIsEmpty: false,
                    defaultSelected: false,
                    resultRelativePath: string.Empty)
            ];
        }

        if (file.Worksheets is null)
        {
            return [CreateOperation(file, sourceRootPath, targetRootPath, rule)];
        }

        if (file.Worksheets.Count == 0)
        {
            return
            [
                CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    string.Empty,
                    false,
                    OperationStatus.Skipped,
                    "В книге Excel не найдено обычных листов.",
                    targetRootPath,
                    sourceRootPath,
                    worksheetName: string.Empty,
                    WorksheetVisibility.Visible,
                    worksheetIsEmpty: true,
                    defaultSelected: false,
                    resultRelativePath: string.Empty)
            ];
        }

        var workbookBaseName = Path.GetFileNameWithoutExtension(file.RelativePath);
        var outputNames = WorksheetOutputNameBuilder.Build(
            workbookBaseName,
            file.Worksheets.Select(sheet => sheet.Name),
            targetExtension);
        var relativeDirectory = Path.GetDirectoryName(file.RelativePath) ?? string.Empty;
        var adapter = adapterResolver.Resolve(file.Format, rule.Target);
        var adapterAvailable = adapter?.IsAvailable == true;
        var result = new List<PlannedOperation>(file.Worksheets.Count);

        for (var index = 0; index < file.Worksheets.Count; index++)
        {
            var sheet = file.Worksheets[index];
            var relativeOutput = string.IsNullOrWhiteSpace(relativeDirectory)
                ? outputNames[index]
                : Path.Combine(relativeDirectory, outputNames[index]);
            var targetPath = Path.GetFullPath(Path.Combine(targetRootPath, relativeOutput));

            if (!OutputPathGuard.IsSafeTargetPath(targetPath, targetRootPath))
            {
                result.Add(CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    string.Empty,
                    false,
                    OperationStatus.Failed,
                    "Недопустимый путь результата.",
                    targetRootPath,
                    sourceRootPath,
                    sheet.Name,
                    sheet.Visibility,
                    sheet.IsEmpty,
                    defaultSelected: false,
                    relativeOutput));
                continue;
            }

            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                result.Add(CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    targetPath,
                    adapterAvailable,
                    OperationStatus.Conflict,
                    "Файл результата уже существует.",
                    targetRootPath,
                    sourceRootPath,
                    sheet.Name,
                    sheet.Visibility,
                    sheet.IsEmpty,
                    defaultSelected: false,
                    relativeOutput));
                continue;
            }

            if (sheet.IsEmpty)
            {
                result.Add(CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    targetPath,
                    adapterAvailable,
                    OperationStatus.Skipped,
                    "Пустой лист не экспортируется.",
                    targetRootPath,
                    sourceRootPath,
                    sheet.Name,
                    sheet.Visibility,
                    worksheetIsEmpty: true,
                    defaultSelected: false,
                    relativeOutput));
                continue;
            }

            if (adapter is null)
            {
                result.Add(CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    targetPath,
                    false,
                    OperationStatus.Unsupported,
                    "Выбранное преобразование не поддерживается.",
                    targetRootPath,
                    sourceRootPath,
                    sheet.Name,
                    sheet.Visibility,
                    worksheetIsEmpty: false,
                    defaultSelected: false,
                    relativeOutput));
                continue;
            }

            if (!adapterAvailable)
            {
                result.Add(CreateWorksheet(
                    file,
                    rule.Target,
                    targetExtension,
                    targetPath,
                    false,
                    OperationStatus.EngineUnavailable,
                    adapter.AvailabilityMessage,
                    targetRootPath,
                    sourceRootPath,
                    sheet.Name,
                    sheet.Visibility,
                    worksheetIsEmpty: false,
                    defaultSelected: false,
                    relativeOutput));
                continue;
            }

            var hidden = sheet.Visibility != WorksheetVisibility.Visible;
            result.Add(CreateWorksheet(
                file,
                rule.Target,
                targetExtension,
                targetPath,
                true,
                OperationStatus.Ready,
                hidden
                    ? "Скрытый лист. Не выбран по умолчанию."
                    : "Готово к преобразованию.",
                targetRootPath,
                sourceRootPath,
                sheet.Name,
                sheet.Visibility,
                worksheetIsEmpty: false,
                defaultSelected: !hidden,
                relativeOutput));
        }

        return result;
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

    private static PlannedOperation CreateWorksheet(
        ScannedFile file,
        ConversionTarget target,
        string targetExtension,
        string targetPath,
        bool adapterAvailable,
        OperationStatus status,
        string message,
        string outputRootPath,
        string sourceRootPath,
        string worksheetName,
        WorksheetVisibility worksheetVisibility,
        bool worksheetIsEmpty,
        bool defaultSelected,
        string resultRelativePath) =>
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
            file.SizeBytes,
            worksheetName,
            worksheetVisibility,
            worksheetIsEmpty,
            defaultSelected,
            resultRelativePath);
}
