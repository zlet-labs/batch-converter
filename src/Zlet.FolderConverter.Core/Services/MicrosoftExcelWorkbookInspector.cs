using System.Text;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public interface IExcelWorkbookInspector
{
    bool IsAvailable { get; }

    Task<ExcelWorkbookInspectionResult> InspectAsync(
        string sourcePath,
        CancellationToken cancellationToken);
}

public sealed record ExcelWorkbookInspectionResult(
    bool Success,
    IReadOnlyList<WorksheetInfo> Worksheets,
    string ErrorCode = "");

public sealed class MicrosoftExcelWorkbookInspector : IExcelWorkbookInspector
{
    private const string PayloadPrefix = "worksheets:";
    private readonly IMicrosoftOfficeWorkerRunner _workerRunner;
    private readonly bool _excelAvailable;

    public MicrosoftExcelWorkbookInspector(
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner workerRunner)
    {
        _workerRunner = workerRunner;
        _excelAvailable = capabilityDetector.Detect()
            .Single(item => item.Application == OfficeApplicationKind.Excel)
            .IsAvailable;
    }

    public bool IsAvailable => _excelAvailable && _workerRunner.IsAvailable;

    public async Task<ExcelWorkbookInspectionResult> InspectAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            return new(false, [], _excelAvailable ? "worker_missing" : "office_application_missing");
        }

        var result = await _workerRunner.RunAsync(
            new OfficeWorkerRequest(
                OfficeApplicationKind.Excel,
                sourcePath,
                string.Empty,
                Operation: OfficeWorkerOperation.InspectWorkbook),
            cancellationToken);
        if (!result.Success)
        {
            return new(false, [], result.ErrorCode);
        }

        if (result.Worksheets is { Count: > 0 })
        {
            return new(true, result.Worksheets);
        }

        if (!result.ErrorCode.StartsWith(PayloadPrefix, StringComparison.Ordinal))
        {
            return new(false, [], "worksheet_payload_missing");
        }

        try
        {
            var bytes = Convert.FromBase64String(result.ErrorCode[PayloadPrefix.Length..]);
            var worksheets = JsonSerializer.Deserialize<WorksheetInfo[]>(bytes)
                ?? [];
            return new(true, worksheets);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return new(false, [], "worksheet_payload_invalid");
        }
    }
}
