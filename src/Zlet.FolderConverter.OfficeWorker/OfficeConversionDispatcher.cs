using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal sealed class OfficeConversionDispatcher : IDisposable
{
    private readonly IOfficeAutomation _automation;
    private readonly IExcelWorksheetAutomation _excelWorksheetAutomation;
    private bool? _worksheetRoute;

    public OfficeConversionDispatcher(IOfficeAutomation automation)
        : this(automation, new ExcelWorksheetAutomation())
    {
    }

    internal OfficeConversionDispatcher(
        IOfficeAutomation automation,
        IExcelWorksheetAutomation excelWorksheetAutomation)
    {
        _automation = automation;
        _excelWorksheetAutomation = excelWorksheetAutomation;
    }

    public OfficeWorkerMessage Convert(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report)
    {
        var worksheetRoute = request.Application == OfficeApplicationKind.Excel
            && (request.Operation == OfficeWorkerOperation.InspectWorkbook
                || request.Target is ConversionTarget.Csv or ConversionTarget.Tsv);
        if (_worksheetRoute.HasValue && _worksheetRoute != worksheetRoute)
        {
            // The parent worker tracks one owned Office process. Close the previous
            // automation session before another route reports a new process identity.
            if (_worksheetRoute.Value) _excelWorksheetAutomation.Dispose();
            else if (_automation is IDisposable disposable) disposable.Dispose();
        }
        _worksheetRoute = worksheetRoute;
        if (worksheetRoute)
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

internal interface IExcelWorksheetAutomation : IDisposable
{
    OfficeWorkerMessage Execute(OfficeWorkerRequest request, Action<OfficeWorkerMessage> report);
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
