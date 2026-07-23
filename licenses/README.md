# Packaged license material

This repository intentionally does not contain a LibreOffice binary distribution
or copied license text from an unselected build.

The locally verified ZL-041 candidate used the official LibreOffice 26.2.4
Windows x86-64 package (reported runtime version 26.2.4.2). This record does not
add its binaries to Git or approve a public release.

For a portable release, `scripts/publish-portable.ps1 -LibreOfficePath <path>`
copies the exact license and notice documents from the selected LibreOffice
package into `licenses/libreoffice/package-documents`, records the runtime's
reported version, and adds official source-code locations.

Packaging fails when the runtime, `soffice.exe`, version information, or a
license document from the selected package cannot be confirmed. A public
artifact remains blocked until those generated materials are reviewed.
