using System.Runtime.InteropServices;
using System.Text.Json;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.OfficeWorker;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [STAThread]
    private static int Main()
    {
        OfficeWorkerMessage result;
        try
        {
            var input = Console.In.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return WriteResult(new OfficeWorkerMessage(
                    OfficeWorkerMessageType.Result,
                    false,
                    "request_missing"));
            }

            var request = JsonSerializer.Deserialize<OfficeWorkerRequest>(input, JsonOptions);
            if (request is null || !OfficeRequestValidator.IsValid(request))
            {
                return WriteResult(new OfficeWorkerMessage(
                    OfficeWorkerMessageType.Result,
                    false,
                    "request_invalid"));
            }

            var dispatcher = new OfficeConversionDispatcher(new ComOfficeAutomation());
            result = dispatcher.Convert(
                request,
                message => WriteMessage(message));
        }
        catch (JsonException)
        {
            result = new OfficeWorkerMessage(
                OfficeWorkerMessageType.Result,
                false,
                "request_invalid");
        }
        catch (COMException exception)
        {
            result = new OfficeWorkerMessage(
                OfficeWorkerMessageType.Result,
                false,
                "office_com_failure",
                HResult: exception.HResult);
        }
        catch (Exception exception)
        {
            result = new OfficeWorkerMessage(
                OfficeWorkerMessageType.Result,
                false,
                "worker_failure",
                HResult: exception.HResult);
        }

        return WriteResult(result);
    }

    private static int WriteResult(OfficeWorkerMessage message)
    {
        WriteMessage(message);
        return message.Success ? 0 : 1;
    }

    private static void WriteMessage(OfficeWorkerMessage message)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(message, JsonOptions));
        Console.Out.Flush();
    }
}

internal static class OfficeRequestValidator
{
    public static bool IsValid(OfficeWorkerRequest request)
    {
        try
        {
            var source = Path.GetFullPath(request.SourcePath);
            var output = Path.GetFullPath(request.OutputPath);
            var expected = request.Application switch
            {
                OfficeApplicationKind.Word => (Source: ".doc", Output: ".docx"),
                OfficeApplicationKind.Excel => (Source: ".xls", Output: ".xlsx"),
                OfficeApplicationKind.PowerPoint => (Source: ".ppt", Output: ".pptx"),
                _ => (Source: string.Empty, Output: string.Empty)
            };
            var outputDirectory = Path.GetDirectoryName(output);
            return !string.IsNullOrWhiteSpace(expected.Source)
                   && File.Exists(source)
                   && !File.Exists(output)
                   && !Directory.Exists(output)
                   && !string.IsNullOrWhiteSpace(outputDirectory)
                   && Directory.Exists(outputDirectory)
                   && Path.GetExtension(source).Equals(
                       expected.Source,
                       StringComparison.OrdinalIgnoreCase)
                   && Path.GetExtension(output).Equals(
                       expected.Output,
                       StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(source, output, StringComparison.OrdinalIgnoreCase)
                   && !HasReparsePoint(source)
                   && !HasReparsePoint(outputDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or IOException
                                           or UnauthorizedAccessException
                                           or NotSupportedException
                                           or PathTooLongException)
        {
            return false;
        }
    }

    private static bool HasReparsePoint(string path)
    {
        for (var current = Path.GetFullPath(path);
             !string.IsNullOrWhiteSpace(current);
             current = Path.GetDirectoryName(current) ?? string.Empty)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return false;
    }
}
