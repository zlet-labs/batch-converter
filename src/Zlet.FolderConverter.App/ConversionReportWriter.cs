using System.IO;
using System.IO.Compression;
using System.Text;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App;

public static class ConversionReportWriter
{
    private const string BaseReportName = "ZletConverter-report.txt";

    public static async Task WriteAsync(
        MainWindowViewModel viewModel,
        DateTimeOffset started,
        DateTimeOffset finished,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = BuildReport(viewModel, started, finished);
            if (viewModel.SelectedOutputMode == OutputMode.Folder)
            {
                var root = MainWindowViewModel.NormalizePathInput(viewModel.OutputPath);
                Directory.CreateDirectory(root);
                var path = GetAvailableReportPath(root);
                await File.WriteAllTextAsync(
                    path,
                    report,
                    new UTF8Encoding(false),
                    cancellationToken);
                return;
            }

            var zipPath = MainWindowViewModel.NormalizePathInput(viewModel.OutputPath);
            var directory = Path.GetDirectoryName(zipPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidDataException("ZIP output directory is missing.");
            }
            Directory.CreateDirectory(directory);
            var mode = File.Exists(zipPath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
            using var archive = ZipFile.Open(zipPath, mode);
            var entryName = GetAvailableEntryName(archive);
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                leaveOpen: false);
            await writer.WriteAsync(report.AsMemory(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException
                                           or ArgumentException)
        {
            viewModel.AddError("Не удалось создать текстовый отчёт о пакетной обработке.");
        }
    }

    public static string BuildReport(
        MainWindowViewModel viewModel,
        DateTimeOffset started,
        DateTimeOffset finished)
    {
        var operations = viewModel.Operations.ToArray();
        var sourceCount = operations
            .Select(row => row.Operation.SourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var duration = finished >= started ? finished - started : TimeSpan.Zero;
        var builder = new StringBuilder();
        builder.AppendLine($"{ProductIdentity.Name} v{ProductIdentity.Version}");
        builder.AppendLine("Conversion report");
        builder.AppendLine($"Started: {started.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Finished: {finished.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Duration: {FormatDuration(duration)}");
        builder.AppendLine($"Batch outcome: {NormalizeBatchOutcome(viewModel.FinalReportTitle)}");
        builder.AppendLine();
        builder.AppendLine("SUMMARY");
        builder.AppendLine();
        builder.AppendLine($"Source files: {sourceCount}");
        builder.AppendLine($"Converted: {viewModel.FinalConverted}");
        builder.AppendLine($"Copied: {viewModel.FinalCopied}");
        builder.AppendLine($"Skipped: {viewModel.FinalSkipped}");
        builder.AppendLine($"Not selected: {viewModel.FinalNotSelected}");
        builder.AppendLine($"Unavailable: {viewModel.FinalUnavailable}");
        builder.AppendLine($"Conflicts: {viewModel.FinalConflicts}");
        builder.AppendLine($"Failed: {viewModel.FinalFailed}");

        var worksheetRows = operations
            .Where(row => row.Operation.IsWorksheetOperation)
            .ToArray();
        if (worksheetRows.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("EXCEL");
            builder.AppendLine();
            AppendWorksheetAggregate(builder, worksheetRows);
            foreach (var workbook in worksheetRows
                         .GroupBy(row => row.Operation.SourcePath, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.First().Operation.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine();
                builder.AppendLine(workbook.First().Operation.RelativePath);
                AppendWorksheetAggregate(builder, workbook.ToArray(), perWorkbook: true);
            }
        }

        builder.AppendLine();
        builder.AppendLine("DETAILS");
        builder.AppendLine();
        foreach (var row in operations)
        {
            builder.AppendLine($"[{ToReportStatus(row)}]");
            builder.AppendLine(row.Operation.IsWorksheetOperation
                ? $"{row.Operation.RelativePath} / {row.Operation.WorksheetName}"
                : row.Operation.RelativePath);
            builder.AppendLine(row.ActionLabel);
            if (!string.IsNullOrWhiteSpace(row.ResultPath) && row.ResultPath != "—")
            {
                builder.AppendLine($"Result: {NormalizeRelativePath(row.ResultPath)}");
            }
            if (!string.IsNullOrWhiteSpace(row.Message)
                && row.Operation.Status is not OperationStatus.Succeeded)
            {
                builder.AppendLine($"Reason: {row.Message}");
            }
            if (row.Result?.Diagnostic?.ErrorCode is { Length: > 0 } errorCode)
            {
                builder.AppendLine($"Error code: {errorCode}");
            }
            if (row.Result?.Diagnostic?.HResult is int hResult)
            {
                builder.AppendLine($"HRESULT: 0x{unchecked((uint)hResult):X8}");
            }
            if (row.ExecutionTimeText != "—")
            {
                builder.AppendLine($"Time: {row.ExecutionTimeText}");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendWorksheetAggregate(
        StringBuilder builder,
        IReadOnlyList<OperationRowViewModel> rows,
        bool perWorkbook = false)
    {
        var selected = rows.Count(IsSelectedForRun);
        var csvExported = rows.Count(row =>
            row.Operation.Target == ConversionTarget.Csv
            && row.Operation.Status == OperationStatus.Succeeded);
        var tsvExported = rows.Count(row =>
            row.Operation.Target == ConversionTarget.Tsv
            && row.Operation.Status == OperationStatus.Succeeded);
        var hiddenSkipped = rows.Count(row =>
            row.Operation.WorksheetVisibility != WorksheetVisibility.Visible
            && !IsSelectedForRun(row));
        var emptySkipped = rows.Count(row => row.Operation.WorksheetIsEmpty);
        var failed = rows.Count(row => row.Operation.Status == OperationStatus.Failed);
        if (!perWorkbook)
        {
            var workbooks = rows
                .Select(row => row.Operation.SourcePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            builder.AppendLine($"Workbooks processed: {workbooks}");
            builder.AppendLine($"Worksheets found: {rows.Count}");
            builder.AppendLine($"Worksheets selected: {selected}");
            builder.AppendLine($"CSV exported: {csvExported}");
            builder.AppendLine($"TSV exported: {tsvExported}");
            builder.AppendLine($"Hidden skipped: {hiddenSkipped}");
            builder.AppendLine($"Empty skipped: {emptySkipped}");
            builder.AppendLine($"Failed: {failed}");
            return;
        }

        builder.AppendLine($"Sheets found: {rows.Count}");
        builder.AppendLine($"Selected: {selected}");
        builder.AppendLine($"CSV exported: {csvExported}");
        builder.AppendLine($"TSV exported: {tsvExported}");
        builder.AppendLine($"Hidden skipped: {hiddenSkipped}");
        builder.AppendLine($"Empty skipped: {emptySkipped}");
        builder.AppendLine($"Failed: {failed}");
    }

    private static bool IsSelectedForRun(OperationRowViewModel row) =>
        row.Result is not null
        || row.Operation.Status is OperationStatus.Succeeded
            or OperationStatus.Failed
            or OperationStatus.Cancelled
            or OperationStatus.NotProcessed;

    private static string ToReportStatus(OperationRowViewModel row)
    {
        if (row.Status == "Не выбрано")
        {
            return "NOT SELECTED";
        }

        return row.Operation.Status switch
        {
            OperationStatus.Succeeded when row.Operation.Target == ConversionTarget.Copy => "COPIED",
            OperationStatus.Succeeded => "CONVERTED",
            OperationStatus.Skipped => "SKIPPED",
            OperationStatus.EngineUnavailable or OperationStatus.Unsupported => "UNAVAILABLE",
            OperationStatus.Conflict => "CONFLICT",
            OperationStatus.Failed => "FAILED",
            OperationStatus.Cancelled => "CANCELLED",
            OperationStatus.NotProcessed => "NOT PROCESSED",
            OperationStatus.Ready => "NOT SELECTED",
            _ => row.Operation.Status.ToString().ToUpperInvariant()
        };
    }

    private static string NormalizeBatchOutcome(string value) =>
        value.Contains("Остановлено пользователем", StringComparison.OrdinalIgnoreCase)
            ? "Batch stopped by user"
            : value;

    private static string NormalizeRelativePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static string FormatDuration(TimeSpan duration)
    {
        var totalHours = (int)Math.Floor(Math.Max(0, duration.TotalHours));
        return totalHours > 0
            ? $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string GetAvailableReportPath(string root)
    {
        var first = Path.Combine(root, BaseReportName);
        if (!File.Exists(first) && !Directory.Exists(first))
        {
            return first;
        }

        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(root, $"ZletConverter-report-{index}.txt");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new IOException("No available report filename.");
    }

    private static string GetAvailableEntryName(ZipArchive archive)
    {
        var existing = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(BaseReportName))
        {
            return BaseReportName;
        }

        for (var index = 2; index < int.MaxValue; index++)
        {
            var candidate = $"ZletConverter-report-{index}.txt";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
        throw new IOException("No available report entry name.");
    }
}
