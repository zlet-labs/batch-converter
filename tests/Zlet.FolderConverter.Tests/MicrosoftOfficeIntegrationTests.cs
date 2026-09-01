using System.Security.Cryptography;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class MicrosoftOfficeIntegrationTests
{
    [OfficeBatchIntegrationFact(
        OfficeApplicationKind.Word,
        "ZLET_OFFICE_WORD_BATCH_FIXTURE_DIR")]
    [Trait("Category", "OfficeIntegration")]
    public async Task Converts_twenty_plus_docs_in_one_reused_worker_batch()
    {
        var sourceRoot = Path.GetFullPath(
            Environment.GetEnvironmentVariable("ZLET_OFFICE_WORD_BATCH_FIXTURE_DIR")!);
        var sourcePaths = Directory.EnumerateFiles(sourceRoot, "*.doc")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sourceHashes = sourcePaths.ToDictionary(path => path, Hash);
        var outputRoot = Path.Combine(
            Path.GetTempPath(), "ZletBatchConverter", "office-batch-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var detector = new MicrosoftOfficeCapabilityDetector();
            var runner = new MicrosoftOfficeWorkerProcessRunner(
                new MicrosoftOfficeWorkerOptions
                {
                    WorkerExecutablePath = Path.Combine(
                        AppContext.BaseDirectory,
                        "Zlet.FolderConverter.OfficeWorker.exe"),
                    Timeout = TimeSpan.FromMinutes(2)
                });
            var resolver = new DefaultConversionAdapterResolver(detector, runner);
            var scanner = new FileSystemFolderScanner();
            var scan = await scanner.ScanAsync(sourceRoot, true, CancellationToken.None);
            var operations = new ConversionPlanner(resolver).CreatePlan(
                scan,
                sourceRoot,
                outputRoot,
                RuleSet.CreateDefault());

            var summary = await new ConversionProcessor(resolver).ProcessAsync(
                operations,
                progress: null,
                CancellationToken.None);

            Assert.True(sourcePaths.Length >= 20);
            Assert.Equal(sourcePaths.Length, summary.Succeeded);
            Assert.Equal(sourcePaths.Length, Directory.EnumerateFiles(outputRoot, "*.docx").Count());
            Assert.All(sourcePaths, path => Assert.Equal(sourceHashes[path], Hash(path)));
        }
        finally
        {
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
        }
    }

    [OfficeIntegrationFact(
        OfficeApplicationKind.Word,
        "ZLET_OFFICE_WORD_FIXTURE")]
    [Trait("Category", "OfficeIntegration")]
    public Task Converts_doc_to_docx() =>
        ConvertAsync(
            OfficeApplicationKind.Word,
            SourceFormat.Doc,
            ConversionTarget.Docx,
            "ZLET_OFFICE_WORD_FIXTURE");

    [OfficeIntegrationFact(
        OfficeApplicationKind.Excel,
        "ZLET_OFFICE_EXCEL_FIXTURE")]
    [Trait("Category", "OfficeIntegration")]
    public Task Converts_xls_to_xlsx() =>
        ConvertAsync(
            OfficeApplicationKind.Excel,
            SourceFormat.Xls,
            ConversionTarget.Xlsx,
            "ZLET_OFFICE_EXCEL_FIXTURE");

    [OfficeIntegrationFact(
        OfficeApplicationKind.PowerPoint,
        "ZLET_OFFICE_POWERPOINT_FIXTURE")]
    [Trait("Category", "OfficeIntegration")]
    public Task Converts_ppt_to_pptx() =>
        ConvertAsync(
            OfficeApplicationKind.PowerPoint,
            SourceFormat.Ppt,
            ConversionTarget.Pptx,
            "ZLET_OFFICE_POWERPOINT_FIXTURE");

    private static async Task ConvertAsync(
        OfficeApplicationKind application,
        SourceFormat sourceFormat,
        ConversionTarget target,
        string fixtureVariable)
    {
        var sourcePath = Path.GetFullPath(
            Environment.GetEnvironmentVariable(fixtureVariable)!);
        var sourceRoot = Path.GetDirectoryName(sourcePath)!;
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "ZletBatchConverter",
            "office-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        try
        {
            var sourceHash = Hash(sourcePath);
            var targetPath = Path.Combine(
                outputRoot,
                Path.GetFileNameWithoutExtension(sourcePath) + target.ToExtension());
            var operation = new PlannedOperation(
                sourcePath,
                Path.GetFileName(sourcePath),
                sourceFormat,
                target,
                target.ToExtension(),
                targetPath,
                true,
                OperationStatus.Ready,
                "Готово к преобразованию.",
                outputRoot,
                sourceRoot);
            var detector = new MicrosoftOfficeCapabilityDetector();
            var runner = new MicrosoftOfficeWorkerProcessRunner(
                new MicrosoftOfficeWorkerOptions
                {
                    WorkerExecutablePath = Path.Combine(
                        AppContext.BaseDirectory,
                        "Zlet.FolderConverter.OfficeWorker.exe"),
                    Timeout = TimeSpan.FromMinutes(2)
                });
            var adapter = new MicrosoftOfficeConversionAdapter(
                application,
                detector,
                runner,
                new OutputResultValidator());

            var result = await adapter.ConvertAsync(
                operation,
                CancellationToken.None);

            Assert.Equal(OperationStatus.Succeeded, result.Status);
            Assert.True(File.Exists(targetPath));
            Assert.Equal(sourceHash, Hash(sourcePath));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}

public sealed class OfficeIntegrationFactAttribute : FactAttribute
{
    public OfficeIntegrationFactAttribute(
        OfficeApplicationKind application,
        string fixtureVariable)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ZLET_OFFICE_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set ZLET_OFFICE_INTEGRATION=1 to opt in to Office integration tests.";
            return;
        }

        var available = new MicrosoftOfficeCapabilityDetector().Detect()
            .Single(item => item.Application == application)
            .IsAvailable;
        if (!available)
        {
            Skip = $"{application.ToDisplayName()} is not installed.";
            return;
        }

        var fixture = Environment.GetEnvironmentVariable(fixtureVariable);
        if (string.IsNullOrWhiteSpace(fixture) || !File.Exists(fixture))
        {
            Skip = $"Set {fixtureVariable} to a real legacy Office fixture.";
        }
    }
}

public sealed class OfficeBatchIntegrationFactAttribute : FactAttribute
{
    public OfficeBatchIntegrationFactAttribute(
        OfficeApplicationKind application,
        string fixtureDirectoryVariable)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ZLET_OFFICE_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set ZLET_OFFICE_INTEGRATION=1 to opt in to Office integration tests.";
            return;
        }

        var available = new MicrosoftOfficeCapabilityDetector().Detect()
            .Single(item => item.Application == application)
            .IsAvailable;
        if (!available)
        {
            Skip = $"{application.ToDisplayName()} is not installed.";
            return;
        }

        var directory = Environment.GetEnvironmentVariable(fixtureDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory)
            || !Directory.Exists(directory)
            || Directory.EnumerateFiles(directory, "*.doc").Take(20).Count() < 20)
        {
            Skip = $"Set {fixtureDirectoryVariable} to a directory containing 20+ real DOC files.";
        }
    }
}
