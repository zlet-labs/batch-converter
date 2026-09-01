using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class ConversionProcessorBatchTests
{
    [Fact]
    public async Task Batch_lifecycle_cleans_up_and_file_failure_does_not_stop_next_file()
    {
        var adapter = new SequencedAdapter(
            OperationStatus.Failed,
            OperationStatus.Succeeded);
        var resolver = new BatchResolver(adapter);

        var summary = await new ConversionProcessor(resolver).ProcessAsync(
            [Operation("first.doc"), Operation("second.doc")],
            progress: null,
            CancellationToken.None);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(2, adapter.CallCount);
        Assert.Equal(1, resolver.BeginCount);
        Assert.Equal(1, resolver.EndCount);
    }

    [Fact]
    public async Task Cancellation_ends_active_batch_and_does_not_start_next_file()
    {
        using var cancellation = new CancellationTokenSource();
        var adapter = new CancellingAdapter(cancellation);
        var resolver = new BatchResolver(adapter);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ConversionProcessor(resolver).ProcessAsync(
                [Operation("first.doc"), Operation("second.doc")],
                progress: null,
                cancellation.Token));

        Assert.Equal(1, adapter.CallCount);
        Assert.Equal(1, resolver.BeginCount);
        Assert.Equal(1, resolver.EndCount);
    }

    [Fact]
    public async Task Cancellation_keeps_last_observed_stage_below_100()
    {
        using var cancellation = new CancellationTokenSource();
        var adapter = new StageCancellingAdapter(cancellation);
        var resolver = new BatchResolver(adapter);
        var reports = new List<ConversionProgress>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ConversionProcessor(resolver).ProcessAsync(
                [Operation("first.doc"), Operation("second.doc")],
                new InlineProgress<ConversionProgress>(reports.Add),
                cancellation.Token));

        Assert.Equal(1, adapter.CallCount);
        Assert.Equal(OperationStatus.Cancelled, reports[^1].Status);
        Assert.Equal(60, reports[^1].OperationPercent);
        Assert.DoesNotContain(reports, report => report.OperationPercent == 100);
        var overall = reports.Select(report =>
            (report.Completed + (report.Status is OperationStatus.Converting
                or OperationStatus.Cancelled
                ? (report.OperationPercent ?? 0) / 100d
                : 0)) * 100d / report.Total).ToArray();
        Assert.True(overall.Zip(overall.Skip(1), (left, right) => left <= right).All(value => value));
    }

    private static PlannedOperation Operation(string relativePath) =>
        new(
            Path.GetFullPath(relativePath),
            relativePath,
            SourceFormat.Doc,
            ConversionTarget.Docx,
            ".docx",
            Path.GetFullPath(relativePath + "x"),
            true,
            OperationStatus.Ready,
            "ready");

    private sealed class BatchResolver(IConversionAdapter adapter)
        : IConversionAdapterResolver, IConversionBatchLifecycle
    {
        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }

        public IConversionAdapter? Resolve(
            SourceFormat sourceFormat,
            ConversionTarget target) => adapter;

        public Task BeginBatchAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task EndBatchAsync()
        {
            EndCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class SequencedAdapter(params OperationStatus[] statuses)
        : IConversionAdapter
    {
        public int CallCount { get; private set; }
        public bool IsAvailable => true;
        public string AvailabilityMessage => "available";

        public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) => true;

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            CancellationToken cancellationToken)
        {
            var status = statuses[CallCount++];
            return Task.FromResult(new ConversionResult(operation, status, status.ToString()));
        }
    }

    private sealed class CancellingAdapter(CancellationTokenSource cancellation)
        : IConversionAdapter
    {
        public int CallCount { get; private set; }
        public bool IsAvailable => true;
        public string AvailabilityMessage => "available";

        public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) => true;

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            CancellationToken cancellationToken)
        {
            CallCount++;
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation was not observed.");
        }
    }

    private sealed class StageCancellingAdapter(CancellationTokenSource cancellation)
        : IConversionAdapter
    {
        public int CallCount { get; private set; }
        public bool IsAvailable => true;
        public string AvailabilityMessage => "available";

        public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) => true;

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            CancellationToken cancellationToken) =>
            ConvertAsync(operation, progress: null, cancellationToken);

        public Task<ConversionResult> ConvertAsync(
            PlannedOperation operation,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            progress?.Report(60);
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation was not observed.");
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
