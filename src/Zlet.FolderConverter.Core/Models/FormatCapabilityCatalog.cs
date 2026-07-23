namespace Zlet.FolderConverter.Core.Models;

public static class FormatCapabilityCatalog
{
    private static readonly IReadOnlyDictionary<SourceFormat, FormatCapability> Capabilities =
        new Dictionary<SourceFormat, FormatCapability>
        {
            [SourceFormat.Json] = Capability(SourceFormat.Json, ConversionTarget.Txt, ConversionTarget.Txt, ConversionTarget.Markdown, ConversionTarget.Skip),
            [SourceFormat.Doc] = Capability(SourceFormat.Doc, ConversionTarget.Docx, ConversionTarget.Docx, ConversionTarget.Skip),
            [SourceFormat.Xls] = Capability(SourceFormat.Xls, ConversionTarget.Xlsx, ConversionTarget.Xlsx, ConversionTarget.Skip),
            [SourceFormat.Ppt] = Capability(SourceFormat.Ppt, ConversionTarget.Pptx, ConversionTarget.Pptx, ConversionTarget.Skip),
            [SourceFormat.Docx] = Capability(SourceFormat.Docx, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Xlsx] = Capability(SourceFormat.Xlsx, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Pptx] = Capability(SourceFormat.Pptx, ConversionTarget.Copy, ConversionTarget.Copy, ConversionTarget.Skip),
            [SourceFormat.Odt] = Capability(SourceFormat.Odt, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Ods] = Capability(SourceFormat.Ods, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Odp] = Capability(SourceFormat.Odp, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Pdf] = Capability(SourceFormat.Pdf, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Image] = Capability(SourceFormat.Image, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Archive] = Capability(SourceFormat.Archive, ConversionTarget.Skip, ConversionTarget.Skip),
            [SourceFormat.Unknown] = Capability(SourceFormat.Unknown, ConversionTarget.Skip, ConversionTarget.Skip)
        };

    public static IReadOnlyCollection<FormatCapability> All => Capabilities.Values.ToArray();

    public static FormatCapability Get(SourceFormat format) => Capabilities[format];

    public static OfficeApplicationKind? RequiredOfficeApplication(
        SourceFormat source,
        ConversionTarget target) =>
        (source, target) switch
        {
            (SourceFormat.Doc, ConversionTarget.Docx) => OfficeApplicationKind.Word,
            (SourceFormat.Xls, ConversionTarget.Xlsx) => OfficeApplicationKind.Excel,
            (SourceFormat.Ppt, ConversionTarget.Pptx) => OfficeApplicationKind.PowerPoint,
            _ => null
        };

    public static bool IsSafeCopy(SourceFormat source, ConversionTarget target) =>
        target == ConversionTarget.Copy
        && source is SourceFormat.Docx or SourceFormat.Xlsx or SourceFormat.Pptx;

    private static FormatCapability Capability(
        SourceFormat source,
        ConversionTarget defaultTarget,
        params ConversionTarget[] allowedTargets) =>
        new(source, allowedTargets, defaultTarget);
}
