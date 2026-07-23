# Third-party notices

## LibreOffice runtime

Zlet Batch Converter is designed to redistribute a local LibreOffice runtime
inside the portable package under `runtime/libreoffice`. No LibreOffice binaries
are committed to this repository.

The repository does not contain LibreOffice binaries. During packaging,
`scripts/publish-portable.ps1`:

1. requires an explicit `-LibreOfficePath`;
2. records the version reported by that runtime in
   `licenses/libreoffice/VERSION.txt`;
3. copies the runtime package's own LICENSE, NOTICE, COPYING, CREDITS, and README
   documents without replacing them with a project-authored summary;
4. records official source-code locations in
   `licenses/libreoffice/SOURCE_INFO.txt`.

LibreOffice's official licensing page states that an installation contains the
applicable LICENSE information and that included components can vary by version:
https://www.libreoffice.org/licenses/

Official source archives and source-code information:

- https://www.libreoffice.org/download-other/
- https://download.documentfoundation.org/libreoffice/src/

The locally verified ZL-041 candidate uses the official LibreOffice 26.2.4
Windows x86-64 package; the bundled runtime reports version 26.2.4.2. No public
release artifact may be published until its package documents, redistribution
terms, corresponding source availability, clean-machine behavior, and exact
final artifact have been reviewed.
