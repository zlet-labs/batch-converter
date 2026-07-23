using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class MicrosoftOfficeCapabilityTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-office-capability-tests",
        Guid.NewGuid().ToString("N"));

    public MicrosoftOfficeCapabilityTests() => Directory.CreateDirectory(_rootPath);

    [Theory]
    [InlineData(OfficeApplicationKind.Word)]
    [InlineData(OfficeApplicationKind.Excel)]
    [InlineData(OfficeApplicationKind.PowerPoint)]
    public void Detector_reports_each_application_independently(
        OfficeApplicationKind availableApplication)
    {
        var detector = new MicrosoftOfficeCapabilityDetector(
            new FakeProgIdResolver([availableApplication]));

        var result = detector.Detect();

        Assert.True(result.Single(item =>
            item.Application == availableApplication).IsAvailable);
        Assert.All(
            result.Where(item => item.Application != availableApplication),
            item => Assert.False(item.IsAvailable));
    }

    [Theory]
    [InlineData(SourceFormat.Doc, ConversionTarget.Docx, OfficeApplicationKind.Word)]
    [InlineData(SourceFormat.Xls, ConversionTarget.Xlsx, OfficeApplicationKind.Excel)]
    [InlineData(SourceFormat.Ppt, ConversionTarget.Pptx, OfficeApplicationKind.PowerPoint)]
    public void Missing_application_has_specific_requirement(
        SourceFormat source,
        ConversionTarget target,
        OfficeApplicationKind missingApplication)
    {
        var available = Enum.GetValues<OfficeApplicationKind>()
            .Where(application => application != missingApplication)
            .ToArray();
        var detector = new FakeCapabilityDetector(available);
        var resolver = new DefaultConversionAdapterResolver(
            detector,
            new FakeWorkerRunner(isAvailable: true));
        var operation = Plan(source, target, resolver);

        Assert.Equal(OperationStatus.EngineUnavailable, operation.Status);
        Assert.Equal(missingApplication.ToRequiredMessage(), operation.Message);
    }

    [Fact]
    public void Missing_powerpoint_does_not_block_word_or_excel()
    {
        var detector = new FakeCapabilityDetector(
            [OfficeApplicationKind.Word, OfficeApplicationKind.Excel]);
        var resolver = new DefaultConversionAdapterResolver(
            detector,
            new FakeWorkerRunner(isAvailable: true));
        var scan = new ScanResult(
            _rootPath,
            [
                CreateFile("legacy.doc", SourceFormat.Doc),
                CreateFile("legacy.xls", SourceFormat.Xls),
                CreateFile("legacy.ppt", SourceFormat.Ppt)
            ],
            []);

        var operations = new ConversionPlanner(resolver)
            .CreatePlan(scan, _rootPath, RuleSet.CreateDefault());

        Assert.Equal(OperationStatus.Ready, operations.Single(operation =>
            operation.SourceFormat == SourceFormat.Doc).Status);
        Assert.Equal(OperationStatus.Ready, operations.Single(operation =>
            operation.SourceFormat == SourceFormat.Xls).Status);
        var powerPoint = operations.Single(operation =>
            operation.SourceFormat == SourceFormat.Ppt);
        Assert.Equal(OperationStatus.EngineUnavailable, powerPoint.Status);
        Assert.Equal("Требуется Microsoft PowerPoint", powerPoint.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private PlannedOperation Plan(
        SourceFormat source,
        ConversionTarget target,
        IConversionAdapterResolver resolver)
    {
        var file = CreateFile($"legacy.{source.ToString().ToLowerInvariant()}", source);
        var scan = new ScanResult(_rootPath, [file], []);
        var rules = RuleSet.CreateDefault().WithRule(source, target);
        return Assert.Single(new ConversionPlanner(resolver)
            .CreatePlan(scan, _rootPath, rules));
    }

    private ScannedFile CreateFile(string relativePath, SourceFormat format)
    {
        var path = Path.Combine(_rootPath, relativePath);
        File.WriteAllText(path, "synthetic");
        return new ScannedFile(path, relativePath, format);
    }

    private sealed class FakeProgIdResolver(
        IReadOnlyCollection<OfficeApplicationKind> available) : IOfficeProgIdResolver
    {
        public bool IsRegistered(OfficeApplicationKind application) =>
            available.Contains(application);
    }

    internal sealed class FakeCapabilityDetector(
        IReadOnlyCollection<OfficeApplicationKind> available)
        : IMicrosoftOfficeCapabilityDetector
    {
        public IReadOnlyList<OfficeApplicationAvailability> Detect() =>
            Enum.GetValues<OfficeApplicationKind>()
                .Select(application => new OfficeApplicationAvailability(
                    application,
                    available.Contains(application)))
                .ToArray();
    }

    internal sealed class FakeWorkerRunner(bool isAvailable) : IMicrosoftOfficeWorkerRunner
    {
        public bool IsAvailable { get; } = isAvailable;

        public Task<OfficeWorkerExecutionResult> RunAsync(
            OfficeWorkerRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Planner tests must not invoke the worker.");
    }
}
