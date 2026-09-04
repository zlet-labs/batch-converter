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
        using var dispatcher = new OfficeConversionDispatcher(automation);
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
            if (!File.Exists(source) || HasReparsePoint(source))
            {
                return false;
            }

            var sourceExtension = Path.GetExtension(source);
            if (request.Operation == OfficeWorkerOperation.InspectWorkbook)
            {
                return request.Application == OfficeApplicationKind.Excel
                       && (sourceExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                           || sourceExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase));
            }

            var target = request.Target == ConversionTarget.Skip
                ? request.Application switch
                {
                    OfficeApplicationKind.Word => ConversionTarget.Docx,
                    OfficeApplicationKind.Excel => ConversionTarget.Xlsx,
                    OfficeApplicationKind.PowerPoint => ConversionTarget.Pptx,
                    _ => ConversionTarget.Skip
                }
                : request.Target;

            var validMapping = request.Application switch
            {
                OfficeApplicationKind.Word =>
                    sourceExtension.Equals(".doc", StringComparison.OrdinalIgnoreCase)
                    && target == ConversionTarget.Docx,
                OfficeApplicationKind.Excel when target == ConversionTarget.Xlsx =>
                    sourceExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase),
                OfficeApplicationKind.Excel when target is ConversionTarget.Csv or ConversionTarget.Tsv =>
                    (sourceExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                     || sourceExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrWhiteSpace(request.WorksheetName),
                OfficeApplicationKind.PowerPoint =>
                    sourceExtension.Equals(".ppt", StringComparison.OrdinalIgnoreCase)
                    && target == ConversionTarget.Pptx,
                _ => false
            };
            if (!validMapping)
            {
                return false;
            }

            var output = Path.GetFullPath(request.OutputPath);
            var expectedOutput = target.ToExtension();
            var outputDirectory = Path.GetDirectoryName(output);
            return !File.Exists(output)
                   && !Directory.Exists(output)
                   && !string.IsNullOrWhiteSpace(outputDirectory)
                   && Directory.Exists(outputDirectory)
                   && Path.GetExtension(output).Equals(
                       expectedOutput,
                       StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(source, output, StringComparison.OrdinalIgnoreCase)
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
