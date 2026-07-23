using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class JsonConversionAdapter(IOutputResultValidator validator) : IConversionAdapter
{
    public bool IsAvailable => true;

    public string AvailabilityMessage => "JSON-преобразование доступно локально.";

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        sourceFormat == SourceFormat.Json
        && target is ConversionTarget.Txt or ConversionTarget.Markdown;

    public async Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        try
        {
            if (!OutputPathGuard.IsSafeTargetPath(operation.TargetPath, operation.OutputRootPath))
            {
                return new ConversionResult(operation, OperationStatus.Failed, "Недопустимый путь результата.");
            }

            if (File.Exists(operation.TargetPath) || Directory.Exists(operation.TargetPath))
            {
                return new ConversionResult(operation, OperationStatus.Conflict, "Файл или папка результата уже существует.");
            }

            var sourceText = await File.ReadAllTextAsync(operation.SourcePath, Encoding.UTF8, cancellationToken);
            using var document = JsonDocument.Parse(sourceText);
            var normalizedJson = FormatJson(document);
            var content = operation.TargetExtension.Equals(".md", StringComparison.OrdinalIgnoreCase)
                ? CreateMarkdown(Path.GetFileName(operation.SourcePath), normalizedJson)
                : normalizedJson;

            var targetDirectory = Path.GetDirectoryName(operation.TargetPath)
                ?? throw new IOException("Не удалось определить папку результата.");
            Directory.CreateDirectory(targetDirectory);
            temporaryPath = Path.Combine(
                targetDirectory,
                $".{Path.GetFileName(operation.TargetPath)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(
                temporaryPath,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            var temporaryValidation = validator.Validate(temporaryPath, operation.Target);
            if (!temporaryValidation.IsValid)
            {
                return new ConversionResult(
                    operation,
                    OperationStatus.Failed,
                    "Формат результата не прошёл проверку.",
                    new ConversionDiagnostic(temporaryValidation.ErrorCode));
            }

            File.Move(temporaryPath, operation.TargetPath, overwrite: false);
            temporaryPath = null;

            var finalValidation = validator.Validate(operation.TargetPath, operation.Target);
            if (!finalValidation.IsValid)
            {
                File.Delete(operation.TargetPath);
                return new ConversionResult(
                    operation,
                    OperationStatus.Failed,
                    "Формат результата не прошёл проверку.",
                    new ConversionDiagnostic(finalValidation.ErrorCode));
            }

            return new ConversionResult(operation, OperationStatus.Succeeded, "Преобразовано.");
        }
        catch (JsonException exception)
        {
            return new ConversionResult(
                operation,
                OperationStatus.Failed,
                $"Некорректный JSON: {CreateJsonError(exception)}",
                new ConversionDiagnostic("invalid_json"));
        }
        catch (IOException) when (File.Exists(operation.TargetPath) || Directory.Exists(operation.TargetPath))
        {
            return new ConversionResult(operation, OperationStatus.Conflict, "Файл результата уже существует.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var message = File.Exists(operation.SourcePath)
                ? "Не удалось записать результат."
                : "Не удалось открыть исходный файл.";
            return new ConversionResult(
                operation,
                OperationStatus.Failed,
                message,
                new ConversionDiagnostic(exception is UnauthorizedAccessException
                    ? "access_denied"
                    : "io_failure"));
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
            }
        }
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
