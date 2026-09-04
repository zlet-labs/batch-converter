using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.OfficeWorker;

namespace Zlet.FolderConverter.Tests;

public sealed class OfficeWorkerDispatcherTests
{
    [Fact]
    public void Switching_excel_routes_closes_prior_session_but_reuses_each_route()
    {
        var automation = new RecordingAutomation();
        var worksheets = new RecordingWorksheets();
        using var dispatcher = new OfficeConversionDispatcher(automation, worksheets);
        var legacy = new OfficeWorkerRequest(OfficeApplicationKind.Excel, "book.xls", "book.xlsx", ConversionTarget.Xlsx);
        var csv = legacy with { Target = ConversionTarget.Csv, WorksheetName = "Data" };
        dispatcher.Convert(legacy, _ => { });
        dispatcher.Convert(legacy, _ => { });
        Assert.Equal(0, automation.Disposals);
        dispatcher.Convert(csv, _ => { });
        Assert.Equal(1, automation.Disposals);
        dispatcher.Convert(csv with { Target = ConversionTarget.Tsv }, _ => { });
        Assert.Equal(0, worksheets.Disposals);
        dispatcher.Convert(legacy, _ => { });
        Assert.Equal(1, worksheets.Disposals);
        Assert.Equal(3, automation.Calls.Count);
        Assert.Equal(2, worksheets.Calls);
    }

    private sealed class RecordingWorksheets : IExcelWorksheetAutomation
    {
        public int Calls { get; private set; }
        public int Disposals { get; private set; }
        public OfficeWorkerMessage Execute(OfficeWorkerRequest request, Action<OfficeWorkerMessage> report)
        {
            Calls++;
            return new(OfficeWorkerMessageType.Result, true);
        }
        public void Dispose() => Disposals++;
    }
    [Theory]
    [InlineData(OfficeApplicationKind.Word)]
    [InlineData(OfficeApplicationKind.Excel)]
    [InlineData(OfficeApplicationKind.PowerPoint)]
    public void Dispatcher_routes_one_operation_to_the_requested_application(
        OfficeApplicationKind application)
    {
        var automation = new RecordingAutomation();
        var request = new OfficeWorkerRequest(application, "source", "output");

        var result = new OfficeConversionDispatcher(automation)
            .Convert(request, _ => { });

        Assert.True(result.Success);
        Assert.Equal([application], automation.Calls);
    }

    private sealed class RecordingAutomation : IOfficeAutomation, IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
        public List<OfficeApplicationKind> Calls { get; } = [];

        public OfficeWorkerMessage ConvertWord(
            OfficeWorkerRequest request,
            Action<OfficeWorkerMessage> report) =>
            Record(OfficeApplicationKind.Word);

        public OfficeWorkerMessage ConvertExcel(
            OfficeWorkerRequest request,
            Action<OfficeWorkerMessage> report) =>
            Record(OfficeApplicationKind.Excel);

        public OfficeWorkerMessage ConvertPowerPoint(
            OfficeWorkerRequest request,
            Action<OfficeWorkerMessage> report) =>
            Record(OfficeApplicationKind.PowerPoint);

        private OfficeWorkerMessage Record(OfficeApplicationKind application)
        {
            Calls.Add(application);
            return new OfficeWorkerMessage(OfficeWorkerMessageType.Result, true);
        }
    }
}
