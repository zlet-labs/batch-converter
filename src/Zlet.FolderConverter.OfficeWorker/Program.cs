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
        using var automation = new ComOfficeAutomation();
        var dispatcher = new OfficeConversionDispatcher(automation);
        try
        {
            while (Console.In.ReadLine() is { } input)
            {
                OfficeWorkerMessage result;
                try
                {
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        result = Failure("request_missing");
                    }
                    else
                    {
                        var request = JsonSerializer.Deserialize<OfficeWorkerRequest>(input, JsonOptions);
                        result = request is null || !OfficeRequestValidator.IsValid(request)
                            ? Failure("request_invalid")
                            : dispatcher.Convert(request, WriteMessage);
                    }
                }
                catch (JsonException)
                {
                    result = Failure("request_invalid");
                }
                catch (COMException exception)
                {
                    result = Failure(
                        "office_com_failure",
                        exception.HResult,
                        sessionInvalid: true);
                }
                catch (Exception exception)
                {
                    result = Failure(
                        "worker_failure",
                        exception.HResult,
                        sessionInvalid: true);
                }

                WriteMessage(result);
            }

            return 0;
        }
        catch (Exception exception)
        {
            WriteMessage(Failure(
                "worker_failure",
                exception.HResult,
                sessionInvalid: true));
            return 1;
        }
    }

    private static OfficeWorkerMessage Failure(
        string errorCode,
        int? hResult = null,
        bool sessionInvalid = false) =>
        new(
            OfficeWorkerMessageType.Result,
            false,
            errorCode,
            HResult: hResult,
            SessionInvalid: sessionInvalid);

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
