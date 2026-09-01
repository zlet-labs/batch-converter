using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class JsonConversionAdapter : IConversionAdapter
{
    private readonly SafeFileOperationExecutor _executor;

    public JsonConversionAdapter(
        IOutputResultValidator validator,
        string? temporaryRoot = null)
    {
        _executor = new SafeFileOperationExecutor(validator, temporaryRoot);
    }

    public bool IsAvailable => true;

    public string AvailabilityMessage => "JSON-преобразование доступно локально.";

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        sourceFormat == SourceFormat.Json
        && target is ConversionTarget.Txt or ConversionTarget.Markdown;

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken) =>
        ConvertAsync(operation, progress: null, cancellationToken);

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync(
            operation,
            operation.Target,
            async (temporaryOutput, token) =>
            {
                try
                {
                    var sourceText = await File.ReadAllTextAsync(
                        operation.SourcePath,
                        Encoding.UTF8,
                        token);
                    using var document = JsonDocument.Parse(sourceText);
                    var normalizedJson = FormatJson(document);
                    var content = operation.TargetExtension.Equals(
                        ".md",
                        StringComparison.OrdinalIgnoreCase)
                        ? CreateMarkdown(Path.GetFileName(operation.SourcePath), normalizedJson)
                        : normalizedJson;
                    await File.WriteAllTextAsync(
                        temporaryOutput,
                        content,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        token);
                    return new TemporaryOutputProductionResult(true);
                }
                catch (JsonException exception)
                {
                    return new TemporaryOutputProductionResult(
                        false,
                        "invalid_json",
                        $"Некорректный JSON: {CreateJsonError(exception)}");
                }
            },
            "Преобразовано.",
            progress,
            cancellationToken);
    }

    private static string CreateMarkdown(string fileName, string json)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in json)
        {
            currentRun = character == '`' ? currentRun + 1 : 0;
            longestRun = Math.Max(longestRun, currentRun);
        }

        var fence = new string('`', Math.Max(3, longestRun + 1));
        return $"# {fileName}{Environment.NewLine}{Environment.NewLine}{fence}json{Environment.NewLine}{json}{Environment.NewLine}{fence}{Environment.NewLine}";
    }

    private static string FormatJson(JsonDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateJsonError(JsonException exception)
    {
        return exception.Message.Contains("separator", StringComparison.OrdinalIgnoreCase)
            ? "ожидался разделитель или конец объекта."
            : "файл имеет неверный формат.";
    }

}
