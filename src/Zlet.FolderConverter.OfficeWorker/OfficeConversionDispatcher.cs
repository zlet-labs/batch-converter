using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal sealed class OfficeConversionDispatcher(IOfficeAutomation automation)
{
    public OfficeWorkerMessage Convert(
        OfficeWorkerRequest request,
        Action<OfficeWorkerMessage> report) =>
        request.Application switch
        {
            OfficeApplicationKind.Word => automation.ConvertWord(request, report),
            OfficeApplicationKind.Excel => automation.ConvertExcel(request, report),
            OfficeApplicationKind.PowerPoint => automation.ConvertPowerPoint(request, report),
            _ => new OfficeWorkerMessage(
                OfficeWorkerMessageType.Result,
                false,
                "application_unsupported")
        };
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
