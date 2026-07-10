using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class DocumentFormatDetectorTests
{
    [Theory]
    [InlineData("sample.doc", DocumentFormat.Doc)]
    [InlineData("sample.DOC", DocumentFormat.Doc)]
    [InlineData("sample.DoC", DocumentFormat.Doc)]
    [InlineData("sample.xls", DocumentFormat.Xls)]
    [InlineData("sample.XLS", DocumentFormat.Xls)]
    [InlineData("sample.XlS", DocumentFormat.Xls)]
    [InlineData("sample.ppt", DocumentFormat.Ppt)]
    [InlineData("sample.PPT", DocumentFormat.Ppt)]
    [InlineData("sample.PpT", DocumentFormat.Ppt)]
    public void TryDetect_detects_supported_formats_case_insensitively(
        string fileName,
        DocumentFormat expectedFormat)
    {
        var detected = DocumentFormatDetector.TryDetect(fileName, out var format);

        Assert.True(detected);
        Assert.Equal(expectedFormat, format);
    }

    [Theory]
    [InlineData(DocumentFormat.Doc, ".docx")]
    [InlineData(DocumentFormat.Xls, ".xlsx")]
    [InlineData(DocumentFormat.Ppt, ".pptx")]
    public void GetTargetExtension_returns_expected_mapping(
        DocumentFormat format,
        string expectedExtension)
    {
        Assert.Equal(expectedExtension, DocumentFormatDetector.GetTargetExtension(format));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("report.docx")]
    [InlineData("book.xlsx")]
    [InlineData("slides.pptx")]
    public void TryDetect_rejects_unsupported_formats(string fileName)
    {
        var detected = DocumentFormatDetector.TryDetect(fileName, out _);

        Assert.False(detected);
    }
}
