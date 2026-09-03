using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal sealed class OfficeConversionDispatcher : IDisposable
{
    private readonly IOfficeAutomation _automation;
    private readonly ExcelWorksheetAutomation _excelWorksheetAutomation;

    public OfficeConversionDispatcher(IOfficeAutomation automation)
        : this(automation, new ExcelWorksheetAutomation())
    {
    }

    internal OfficeConversionDispatcher(
        IOfficeAutomation automation,
        ExcelWorksheetAutomation excelWorksheetAutomation)
    {
        _automation = automation;
        _excelWorksheetAutomation = excelWorksheetAutomation;
    }

    public OfficeWorkerMessage Convert(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        if (request.Application == OfficeApplicationKind.Excel
            && (request.Operation == OfficeWorkerOperation.InspectWorkbook
                || request.Target is ConversionTarget.Csv or ConversionTarget.Tsv))
        {
            return _excelWorksheetAutomation.Execute(request, report);
        }

        return request.Application switch
        {
            OfficeApplicationKind.Word => _automation.ConvertWord(request, report),
            OfficeApplicationKind.Excel => _automation.ConvertExcel(request, report),
            OfficeApplicationKind.PowerPoint => _automation.ConvertPowerPoint(request, report),
            _ => new OfficeWorkerMessage(
                OfficeWorkerMessageType.Result,
                false,
                "application_unsupported")
        };
    }

    public void Dispose() => _excelWorksheetAutomation.Dispose();
}

internal interface IOfficeAutomation
{
    OfficeWorkerMessage ConvertWord(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report);

    OfficeWorkerMessage ConvertExcel(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report);

    OfficeWorkerMessage ConvertPowerPoint(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report);
}
