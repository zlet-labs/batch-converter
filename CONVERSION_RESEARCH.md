# Conversion engine decision

## Decision for ZL-041

Zlet Folder Converter uses a local bundled LibreOffice runtime for Office and
OpenDocument conversion. LibreOffice is invoked only through
`LibreOfficeConversionAdapter`, `ILibreOfficeRuntimeLocator`, and
`ILibreOfficeProcessRunner`; UI and planning code do not know about
`soffice.exe`.

The application does not use Microsoft Office COM, a cloud service, a network
runtime, or automatic downloads. The portable package keeps LibreOffice in
`runtime/libreoffice` rather than attempting to embed it into one EXE.

## Why this architecture

- one engine covers legacy Office, OOXML, OpenDocument, and PDF output;
- conversion stays on the user's machine;
- a headless process can be isolated with a unique temporary user profile;
- the adapter can be replaced later without changing rules, planner, or
  ViewModel;
- per-file process calls keep failures attributable and allow the batch to
  continue.

## Safety implementation

Each Office operation:

1. resolves only an explicit dev runtime or bundled
   `runtime/libreoffice/program/soffice.exe`;
2. creates unique system-temp output and profile directories;
3. starts LibreOffice hidden in headless mode with a timeout and cancellation;
4. kills the process tree on timeout or cancellation;
5. checks exit code and expected output;
6. validates non-zero output, OOXML base ZIP parts, or PDF signature;
7. verifies the source hash is unchanged;
8. stages the validated file next to the final target and atomically publishes
   it without overwrite;
9. removes temporary output and profile data.

Command lines, document contents, stdout, and stderr are not shown to users or
stored in logs. Diagnostics contain only a stable error code, optional exit
code, and timeout/cancellation flags.

## Licensing and release gate

LibreOffice's official licensing page says that an installation contains
applicable LICENSE information and that included components can differ by
version:

- https://www.libreoffice.org/licenses/

Official source information and release source archives:

- https://www.libreoffice.org/download-other/
- https://download.documentfoundation.org/libreoffice/src/

The repository does not select or commit a LibreOffice distribution.
`publish-portable.ps1` requires a real package, records its reported version,
and copies that package's license/notice documents into the artifact. Packaging
fails if this material cannot be confirmed.

No public release artifact is approved until the chosen build, its complete
third-party documents, redistribution conditions, corresponding source
availability, final ZIP contents, and clean-machine behavior have been manually
reviewed.

## Compatibility limits

LibreOffice conversion is not a promise of pixel-perfect Microsoft Office
fidelity. Macros, embedded objects, fonts, advanced charts, external links,
password-protected files, and damaged documents can be unsupported or render
differently. Output validation confirms container structure and signature, not
visual equivalence.

XLSX → CSV per sheet is intentionally outside ZL-041.
