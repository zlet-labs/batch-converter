using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Tests;

public sealed class RuleSetTests
{
    [Theory]
    [InlineData(SourceFormat.Json, ConversionTarget.Txt)]
    [InlineData(SourceFormat.Doc, ConversionTarget.Docx)]
    [InlineData(SourceFormat.Xls, ConversionTarget.Xlsx)]
    [InlineData(SourceFormat.Ppt, ConversionTarget.Pptx)]
    [InlineData(SourceFormat.Docx, ConversionTarget.Copy)]
    [InlineData(SourceFormat.Xlsx, ConversionTarget.Copy)]
    [InlineData(SourceFormat.Pptx, ConversionTarget.Copy)]
    [InlineData(SourceFormat.Odt, ConversionTarget.Skip)]
    [InlineData(SourceFormat.Ods, ConversionTarget.Skip)]
    [InlineData(SourceFormat.Odp, ConversionTarget.Skip)]
    [InlineData(SourceFormat.Pdf, ConversionTarget.Skip)]
    [InlineData(SourceFormat.Image, ConversionTarget.Skip)]
    [InlineData(SourceFormat.Archive, ConversionTarget.Skip)]
    [InlineData(SourceFormat.Unknown, ConversionTarget.Skip)]
    public void Default_rules_match_product_defaults(
        SourceFormat source,
        ConversionTarget expectedTarget)
    {
        Assert.Equal(expectedTarget, RuleSet.CreateDefault().GetRule(source).Target);
    }

    [Theory]
    [InlineData(SourceFormat.Json, ConversionTarget.Txt)]
    [InlineData(SourceFormat.Json, ConversionTarget.Markdown)]
    [InlineData(SourceFormat.Doc, ConversionTarget.Docx)]
    [InlineData(SourceFormat.Xls, ConversionTarget.Xlsx)]
    [InlineData(SourceFormat.Ppt, ConversionTarget.Pptx)]
    [InlineData(SourceFormat.Docx, ConversionTarget.Copy)]
    [InlineData(SourceFormat.Xlsx, ConversionTarget.Copy)]
    [InlineData(SourceFormat.Pptx, ConversionTarget.Copy)]
    public void Rules_accept_required_mappings(SourceFormat source, ConversionTarget target)
    {
        var rules = RuleSet.CreateDefault().WithRule(source, target);

        Assert.Equal(target, rules.GetRule(source).Target);
    }

    [Fact]
    public void Xlsx_to_csv_is_not_available()
    {
        var capability = FormatCapabilityCatalog.Get(SourceFormat.Xlsx);

        Assert.Equal([ConversionTarget.Copy, ConversionTarget.Skip], capability.AllowedTargets);
    }

    [Fact]
    public void Unsupported_mapping_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => RuleSet.CreateDefault().WithRule(SourceFormat.Pdf, ConversionTarget.Txt));
    }
}
