using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class JsonConversionAdapter(IOutputResultValidator validator) : IConversionAdapter
{
    public DocumentFormat SourceFormat => DocumentFormat.Json;

    public string TargetExtension => ".txt";

    public bool IsAvailable => true;

    public string AvailabilityMessage => "JSON conversion is available locally.";

    public async Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        try
        {
            if (!IsSafeTargetPath(operation))
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

            if (!validator.IsSuccessfulOutput(temporaryPath))
            {
                throw new IOException("Проверка результата не пройдена.");
            }

            File.Move(temporaryPath, operation.TargetPath, overwrite: false);
            temporaryPath = null;

            if (!validator.IsSuccessfulOutput(operation.TargetPath))
            {
                File.Delete(operation.TargetPath);
                throw new IOException("Проверка результата не пройдена.");
            }

            return new ConversionResult(operation, OperationStatus.Succeeded, "Преобразовано.");
        }
        catch (JsonException exception)
        {
            return new ConversionResult(
                operation,
                OperationStatus.Failed,
                $"Некорректный JSON: {CreateJsonError(exception)}",
                exception);
        }
        catch (IOException exception) when (File.Exists(operation.TargetPath) || Directory.Exists(operation.TargetPath))
        {
            return new ConversionResult(operation, OperationStatus.Conflict, "Файл или папка результата уже существует.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ConversionResult(operation, OperationStatus.Failed, "Не удалось записать результат.", exception);
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

    private static bool IsSafeTargetPath(PlannedOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.OutputRootPath))
        {
            return false;
        }

        var outputRoot = Path.GetFullPath(operation.OutputRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var targetPath = Path.GetFullPath(operation.TargetPath);
        if (!targetPath.StartsWith(outputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativeTarget = Path.GetRelativePath(outputRoot, targetPath);
        if (Path.IsPathRooted(relativeTarget)
            || relativeTarget.Equals("..", StringComparison.Ordinal)
            || relativeTarget.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return false;
        }

        for (var directory = Path.GetDirectoryName(targetPath);
             directory is not null
             && directory.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase);
             directory = Path.GetDirectoryName(directory))
        {
            if (Directory.Exists(directory)
                && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            if (string.Equals(directory, outputRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return true;
    }
}
