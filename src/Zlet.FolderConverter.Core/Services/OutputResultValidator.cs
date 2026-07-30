using System.IO.Compression;
using System.Text;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class OutputResultValidator : IOutputResultValidator
{
    public OutputValidationResult Validate(string targetPath, ConversionTarget target)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            return new OutputValidationResult(false, "output_missing");
        }

        try
        {
            if (new FileInfo(targetPath).Length == 0)
            {
                return new OutputValidationResult(false, "output_empty");
            }

            return target switch
            {
                ConversionTarget.Docx => ValidateZip(targetPath, "word/document.xml"),
                ConversionTarget.Xlsx => ValidateZip(targetPath, "xl/workbook.xml"),
                ConversionTarget.Pptx => ValidateZip(targetPath, "ppt/presentation.xml"),
                ConversionTarget.Pdf => ValidatePdf(targetPath),
                ConversionTarget.Txt or ConversionTarget.Markdown => new OutputValidationResult(true),
                _ => new OutputValidationResult(false, "unsupported_output_validation")
            };
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException)
        {
            return new OutputValidationResult(false, "output_unreadable");
        }
    }

    private static OutputValidationResult ValidateZip(string path, string requiredPart)
    {
        using var archive = ZipFile.OpenRead(path);
        var names = archive.Entries
            .Select(entry => entry.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Contains("[Content_Types].xml") && names.Contains(requiredPart)
            ? new OutputValidationResult(true)
            : new OutputValidationResult(false, "ooxml_structure_invalid");
    }

    private static OutputValidationResult ValidatePdf(string path)
    {
        Span<byte> signature = stackalloc byte[5];
        using var stream = File.OpenRead(path);
        return stream.Read(signature) == signature.Length
               && signature.SequenceEqual(Encoding.ASCII.GetBytes("%PDF-"))
            ? new OutputValidationResult(true)
            : new OutputValidationResult(false, "pdf_signature_invalid");
    }
}
