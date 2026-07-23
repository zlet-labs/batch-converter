using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class LibreOfficeIntegrationTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "zlet-lo-integration-tests",
        Guid.NewGuid().ToString("N"),
        "вложенная папка Ω с пробелами");

    public LibreOfficeIntegrationTests() => Directory.CreateDirectory(_rootPath);

    [LibreOfficeIntegrationFact]
    [Trait("Category", "LibreOfficeIntegration")]
    public Task Converts_doc_to_docx() =>
        ConvertGeneratedLegacyFixtureAsync(SourceFormat.Doc, ConversionTarget.Docx);

    [LibreOfficeIntegrationFact]
    [Trait("Category", "LibreOfficeIntegration")]
    public Task Converts_doc_to_pdf() =>
        ConvertGeneratedLegacyFixtureAsync(SourceFormat.Doc, ConversionTarget.Pdf);

    [LibreOfficeIntegrationFact]
    [Trait("Category", "LibreOfficeIntegration")]
    public Task Converts_xls_to_xlsx() =>
        ConvertGeneratedLegacyFixtureAsync(SourceFormat.Xls, ConversionTarget.Xlsx);

    [LibreOfficeIntegrationFact]
    [Trait("Category", "LibreOfficeIntegration")]
    public Task Converts_xls_to_pdf() =>
        ConvertGeneratedLegacyFixtureAsync(SourceFormat.Xls, ConversionTarget.Pdf);

    [LibreOfficeIntegrationFact]
    [Trait("Category", "LibreOfficeIntegration")]
    public Task Converts_ppt_to_pptx() =>
        ConvertGeneratedLegacyFixtureAsync(SourceFormat.Ppt, ConversionTarget.Pptx);

    [LibreOfficeIntegrationFact]
    [Trait("Category", "LibreOfficeIntegration")]
    public Task Converts_ppt_to_pdf() =>
        ConvertGeneratedLegacyFixtureAsync(SourceFormat.Ppt, ConversionTarget.Pdf);

    private async Task ConvertGeneratedLegacyFixtureAsync(
        SourceFormat sourceFormat,
        ConversionTarget target)
    {
        var runtimePath = Environment.GetEnvironmentVariable("ZLET_LIBREOFFICE_PATH")!;
        var options = new LibreOfficeConversionOptions
        {
            ExplicitRuntimePath = runtimePath,
            TemporaryRootPath = Path.Combine(Path.GetTempPath(), "zlet-lo-integration-work"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        var locator = new LibreOfficeRuntimeLocator(options);
        var runtime = locator.Locate();
        Assert.True(runtime.IsAvailable);
        var sourcePath = await CreateLegacyFixtureAsync(runtime.ExecutablePath, sourceFormat);
        var sourceHash = await HashAsync(sourcePath);
        var targetPath = Path.Combine(
            _rootPath,
            "_converted",
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
            "ready",
            Path.Combine(_rootPath, "_converted"));
        var adapter = new LibreOfficeConversionAdapter(
            locator,
            new LibreOfficeProcessRunner(),
            new OutputResultValidator(),
            options);

        var result = await adapter.ConvertAsync(operation, CancellationToken.None);

        Assert.Equal(OperationStatus.Succeeded, result.Status);
        Assert.True(new OutputResultValidator().Validate(targetPath, target).IsValid);
        Assert.Equal(sourceHash, await HashAsync(sourcePath));
    }

    public void Dispose()
    {
        var testRoot = Directory.GetParent(_rootPath)!.FullName;
        if (Directory.Exists(testRoot)
            && Path.GetFileName(Directory.GetParent(testRoot)!.FullName).Equals(
                "zlet-lo-integration-tests",
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private async Task<string> CreateLegacyFixtureAsync(
        string executablePath,
        SourceFormat format)
    {
        var (seedExtension, legacyExtension, legacyFilter) = format switch
        {
            SourceFormat.Doc => (".odt", ".doc", "doc"),
            SourceFormat.Xls => (".ods", ".xls", "xls"),
            SourceFormat.Ppt => (".odp", ".ppt", "ppt"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        var seedPath = Path.Combine(_rootPath, $"синтетический fixture{seedExtension}");
        CreateOpenDocumentSeed(seedPath, format);
        var profilePath = Path.Combine(_rootPath, $".profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profilePath);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add("--nolockcheck");
        startInfo.ArgumentList.Add(
            $"-env:UserInstallation={new Uri(profilePath).AbsoluteUri}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(legacyFilter);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(_rootPath);
        startInfo.ArgumentList.Add(seedPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Synthetic fixture generation did not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        Assert.Equal(0, process.ExitCode);
        var legacyPath = Path.ChangeExtension(seedPath, legacyExtension);
        Assert.True(File.Exists(legacyPath));
        return legacyPath;
    }

    private static void CreateOpenDocumentSeed(string path, SourceFormat format)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var mimeType = format switch
        {
            SourceFormat.Doc => "application/vnd.oasis.opendocument.text",
            SourceFormat.Xls => "application/vnd.oasis.opendocument.spreadsheet",
            SourceFormat.Ppt => "application/vnd.oasis.opendocument.presentation",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        WriteEntry(archive, "mimetype", mimeType, CompressionLevel.NoCompression);
        WriteEntry(archive, "content.xml", format switch
        {
            SourceFormat.Doc => OdtContent,
            SourceFormat.Xls => OdsContent,
            SourceFormat.Ppt => OdpContent,
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        });
        WriteEntry(archive, "META-INF/manifest.xml", Manifest(mimeType, format));
        if (format is SourceFormat.Doc or SourceFormat.Ppt)
        {
            var image = archive.CreateEntry("Pictures/pixel.png");
            using var stream = image.Open();
            stream.Write(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        }
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        string content,
        CompressionLevel compression = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(name, compression);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string Manifest(string mimeType, SourceFormat format)
    {
        var image = format is SourceFormat.Doc or SourceFormat.Ppt
            ? """<manifest:file-entry manifest:full-path="Pictures/pixel.png" manifest:media-type="image/png"/>"""
            : string.Empty;
        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2">
              <manifest:file-entry manifest:full-path="/" manifest:version="1.2" manifest:media-type="{{mimeType}}"/>
              <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/>
              {{image}}
            </manifest:manifest>
            """;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private const string OdtContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
          xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
          xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
          xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
          xmlns:xlink="http://www.w3.org/1999/xlink"
          xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"
          office:version="1.2">
          <office:body><office:text>
            <text:h text:outline-level="1">Синтетический документ</text:h>
            <text:p>Кириллица и Unicode Ω</text:p>
            <table:table table:name="Таблица"><table:table-row>
              <table:table-cell office:value-type="string"><text:p>Колонка 1</text:p></table:table-cell>
              <table:table-cell office:value-type="string"><text:p>Колонка 2</text:p></table:table-cell>
            </table:table-row></table:table>
            <text:p><draw:frame svg:width="1cm" svg:height="1cm">
              <draw:image xlink:href="Pictures/pixel.png" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/>
            </draw:frame></text:p>
          </office:text></office:body>
        </office:document-content>
        """;

    private const string OdsContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
          xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
          xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
          office:version="1.2">
          <office:body><office:spreadsheet><table:table table:name="Данные Ω">
            <table:table-row><table:table-cell office:value-type="string"><text:p>Значение</text:p></table:table-cell></table:table-row>
            <table:table-row><table:table-cell office:value-type="float" office:value="2"><text:p>2</text:p></table:table-cell></table:table-row>
            <table:table-row><table:table-cell office:value-type="float" office:value="3"><text:p>3</text:p></table:table-cell></table:table-row>
            <table:table-row><table:table-cell table:formula="of:=SUM([.A2:.A3])" office:value-type="float" office:value="5"><text:p>5</text:p></table:table-cell></table:table-row>
          </table:table></office:spreadsheet></office:body>
        </office:document-content>
        """;

    private const string OdpContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
          xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
          xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
          xmlns:presentation="urn:oasis:names:tc:opendocument:xmlns:presentation:1.0"
          xmlns:xlink="http://www.w3.org/1999/xlink"
          xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"
          office:version="1.2">
          <office:body><office:presentation>
            <draw:page draw:name="Слайд 1"><draw:frame presentation:class="title" svg:x="1cm" svg:y="1cm" svg:width="20cm" svg:height="2cm"><draw:text-box><text:p>Первый слайд Ω</text:p></draw:text-box></draw:frame></draw:page>
            <draw:page draw:name="Слайд 2"><draw:frame presentation:class="title" svg:x="1cm" svg:y="1cm" svg:width="20cm" svg:height="2cm"><draw:text-box><text:p>Второй слайд</text:p></draw:text-box></draw:frame>
              <draw:frame svg:x="2cm" svg:y="4cm" svg:width="2cm" svg:height="2cm"><draw:image xlink:href="Pictures/pixel.png" xlink:type="simple" xlink:show="embed" xlink:actuate="onLoad"/></draw:frame>
            </draw:page>
          </office:presentation></office:body>
        </office:document-content>
        """;
}

public sealed class LibreOfficeIntegrationFactAttribute : FactAttribute
{
    public LibreOfficeIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ZLET_LIBREOFFICE_PATH")))
        {
            Skip = "Set ZLET_LIBREOFFICE_PATH to run LibreOffice integration tests.";
        }
    }
}
