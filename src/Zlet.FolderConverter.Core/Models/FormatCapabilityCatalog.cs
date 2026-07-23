namespace Zlet.FolderConverter.Core.Models;

public static class FormatCapabilityCatalog
{
    private static readonly IReadOnlyDictionary<SourceFormat, FormatCapability> Capabilities =
        new Dictionary<SourceFormat, FormatCapability>
        {
            [SourceFormat.Json] = Capability(SourceFormat.Json, ConversionTarget.Txt, ConversionTarget.Txt, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Doc] = Capability(SourceFormat.Doc, ConversionTarget.Docx, ConversionTarget.Docx, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Xls] = Capability(SourceFormat.Xls, ConversionTarget.Xlsx, ConversionTarget.Xlsx, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Ppt] = Capability(SourceFormat.Ppt, ConversionTarget.Pptx, ConversionTarget.Pptx, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Docx] = Capability(SourceFormat.Docx, ConversionTarget.Skip, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Xlsx] = Capability(SourceFormat.Xlsx, ConversionTarget.Skip, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Pptx] = Capability(SourceFormat.Pptx, ConversionTarget.Skip, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Odt] = Capability(SourceFormat.Odt, ConversionTarget.Skip, ConversionTarget.Docx, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Ods] = Capability(SourceFormat.Ods, ConversionTarget.Skip, ConversionTarget.Xlsx, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Odp] = Capability(SourceFormat.Odp, ConversionTarget.Skip, ConversionTarget.Pptx, ConversionTarget.Pdf, ConversionTarget.Skip),
            [SourceFormat.Pdf] = Capability(SourceFormat.Pdf, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Image] = Capability(SourceFormat.Image, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Archive] = Capability(SourceFormat.Archive, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Unknown] = Capability(SourceFormat.Unknown, ConversionTarget.Skip, ConversionTarget.Skip)
        };

    public static IReadOnlyCollection<FormatCapability> All => Capabilities.Values.ToArray();

    public static FormatCapability Get(SourceFormat format) => Capabilities[format];

    public static bool RequiresLibreOffice(SourceFormat source, ConversionTarget target) =>
        target != ConversionTarget.Skip
        && source is not SourceFormat.Json
        && Get(source).Supports(target);

    private static FormatCapability Capability(
        SourceFormat source,
        ConversionTarget defaultTarget,
        params ConversionTarget[] allowedTargets) =>
        new(source, allowedTargets, defaultTarget);
}
