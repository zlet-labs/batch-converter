# Conversion Research

Zlet Folder Converter must run locally as a self-contained Windows x64 application. Conversion adapters may be added only when the package works inside the .NET app, requires no external runtime, has licensing compatible with a public MIT repository and commercial use, has no watermark or artificial evaluation limits, and passes synthetic validation.

## Summary Decision

No production conversion dependency is added in this prototype. DOC, XLS, and PPT are detected and planned, but all mappings are reported as unsupported.

## Candidate Matrix

| Candidate | Source | License / terms | Runtime | Limits / watermark | Format fit | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| Microsoft Open XML SDK / `DocumentFormat.OpenXml` | Microsoft docs and NuGet: https://learn.microsoft.com/en-us/office/open-xml/about-the-open-xml-sdk, https://www.nuget.org/packages/DocumentFormat.OpenXml | Open source package, suitable for OOXML manipulation. | .NET only. | No evaluation watermark noted. | Works with Open XML packages such as DOCX/XLSX/PPTX. It is not a legacy binary DOC/XLS/PPT converter. | Reject for DOC/XLS/PPT conversion. Useful later for validating or editing generated OOXML. |
| NPOI | GitHub and NuGet: https://github.com/nissl-lab/npoi, https://www.nuget.org/packages/NPOI | Repository shows Apache-2.0, but current binary/NuGet releases include an EULA and maintenance-fee requirement for revenue-generating users. | .NET package, no Office automation. | No watermark found, but commercial terms need explicit product/legal acceptance. | Strong Excel support; legacy binary handling exists in parts of the project, but full DOC/PPT to OOXML conversion fidelity is not confirmed for this product requirement. | Defer. Do not add until EULA/commercial terms and mapping coverage are approved. |
| Aspose.Words / Cells / Slides | Official licensing docs and NuGet pages, for example https://docs.aspose.com/words/net/licensing/ and https://www.nuget.org/packages/Aspose.Words | Commercial licensing. Trial mode has watermark and size limits. | .NET only, no Office required. | Evaluation watermark and document-size limitations unless licensed. | Product family can cover document conversions, but would require paid licensing and separate packages for Word, Excel, and PowerPoint. | Reject for this prototype. Revisit only with an explicit commercial license decision. |
| Syncfusion Document SDK | Official product and pricing pages: https://www.syncfusion.com/document-sdk, https://www.syncfusion.com/sales/pricing | Commercial subscription/licensing model. Public/commercial use requires license review. | Vendor claims no Microsoft Office or third-party dependency. | Trial/licensing terms need activation/license handling. | Product family advertises Word, Excel, and PowerPoint processing/conversion. | Defer. Do not add until license and redistribution terms are approved. |
| GemBox Document / Spreadsheet / Presentation | Official pages: https://www.gemboxsoftware.com/document, https://www.gemboxsoftware.com/document/free-version, https://www.gemboxsoftware.com/bundle | Commercial product with free mode limits. | .NET only. | Free mode has artificial limits such as paragraph limits. | Product family can read/write legacy and modern office formats, depending on component. | Reject for this prototype because free limits violate requirements. |
| Free Spire.Doc / Spire family | Official pages and NuGet: https://www.e-iceblue.com/Introduce/free-doc-component.html, https://www.nuget.org/packages/FreeSpire.Doc | Free edition has documented artificial limits; commercial edition requires licensing. | .NET package, no Office required. | Free edition has paragraph/table/page limits. | Word conversion support exists in product family; Excel and PowerPoint would require separate products. | Reject for this prototype because artificial limits violate requirements. |
| Telerik Document Processing | Official docs: https://www.telerik.com/document-processing-libraries, https://www.telerik.com/document-processing-libraries/documentation/distribution-and-licensing/license-key/setting-up-license-key | Commercial/trial license key required for current releases. | .NET package. | Requires activation through trial or commercial license key. | Provides document-processing libraries, but legacy binary conversion coverage must be validated. | Defer. License-key requirement does not fit the first portable prototype without a product decision. |

## Mapping Decisions

### DOC to DOCX

- Status: Unsupported.
- Best candidates: Aspose.Words, GemBox.Document, Spire.Doc, Syncfusion Document SDK, Telerik WordsProcessing.
- Reason: available embedded libraries are commercial, limited in free/evaluation mode, or require license activation. Open XML SDK is not a binary DOC converter.
- Prototype action: detect `.doc`, plan `.docx`, show `Unsupported`.

### XLS to XLSX

- Status: Unsupported.
- Best candidates: NPOI, Aspose.Cells, GemBox.Spreadsheet, Syncfusion Document SDK.
- Reason: NPOI commercial/revenue EULA terms require explicit approval, while other candidates require commercial licensing or have free limits.
- Prototype action: detect `.xls`, plan `.xlsx`, show `Unsupported`.

### PPT to PPTX

- Status: Unsupported.
- Best candidates: Aspose.Slides, GemBox.Presentation, Syncfusion Document SDK, Telerik presentation components.
- Reason: no approved MIT-compatible embedded dependency without license activation or evaluation limits was found.
- Prototype action: detect `.ppt`, plan `.pptx`, show `Unsupported`.

## Security Notes

- No document contents are logged.
- No network conversion is allowed.
- No external executable, CLI, Office automation, LibreOffice, Java, Python, or Pandoc is allowed.
- Future adapters must validate output existence, non-zero size, and structural integrity before reporting success.
