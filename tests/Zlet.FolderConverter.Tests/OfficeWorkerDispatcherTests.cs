using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.OfficeWorker;

namespace Zlet.FolderConverter.Tests;

public sealed class OfficeWorkerDispatcherTests
{
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

    private sealed class RecordingAutomation : IOfficeAutomation
    {
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
