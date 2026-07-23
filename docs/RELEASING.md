# Releasing Zlet Folder Converter

Public binaries are distributed through GitHub Releases. Do not commit portable ZIP files, installers, LibreOffice runtimes, or generated artifacts to the repository history.

## Release location

User downloads belong here:

- https://github.com/zlet-labs/folder-converter/releases

The README and CHANGELOG link to this page. Before the first verified package exists, the repository must state that no public release has been published.

## Release prerequisites

A release candidate is not ready until:

- the implementation Issue is complete;
- the PR is reviewed and merged into `main`;
- build and tests pass from a clean checkout;
- the portable package is generated from the merged commit;
- manual Windows verification is complete;
- source files remain unchanged during conversion tests;
- conflict, invalid-file, Unicode-path, and subfolder scenarios are checked;
- the package contains no secrets, private documents, absolute user paths, source code, `bin`, `obj`, or test fixtures;
- required third-party licenses and notices are included;
- README supported-format claims match the actual release.

## Versioning

Use a semantic version such as:

```text
v0.1.0
```

Use pre-release labels when appropriate:

```text
v0.1.0-alpha.1
v0.1.0-beta.1
v0.1.0-rc.1
```

Do not call a build stable merely because it successfully produced a ZIP. Compression is not quality assurance, despite decades of industry tradition.

## Build

From a clean checkout of the release commit:

```powershell
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/publish-portable.ps1 -Version "0.1.0-alpha.1"
```

Record the exact commands, test count, commit SHA, artifact path, and package size.

## Package audit

Confirm the ZIP includes:

- `ZletFolderConverter.exe`;
- `README_PORTABLE.txt`;
- `LICENSE` or `LICENSE.txt`;
- all required self-contained .NET runtime files;
- third-party notices and licenses when applicable.

Confirm it excludes:

- repository source files;
- `.git`, `bin`, and `obj` directories;
- tests and fixtures;
- user documents;
- local absolute paths;
- tokens, credentials, certificates, and API keys;
- debug-only files that are not required to diagnose the release.

## Manual verification

Test the extracted package on Windows 10 or Windows 11 x64, preferably on a clean VM without the .NET SDK.

Verify at minimum:

- application launch without administrator rights;
- paths containing spaces and Cyrillic characters;
- supported conversions;
- invalid input isolation;
- file and directory conflicts;
- repeated scan and conversion;
- preservation of original files;
- 1366×768 layout;
- Windows scaling at 125%;
- opening the `_converted` result folder.

Document checks that were not performed. Never convert an unchecked assumption into a green checkbox through the power of optimism.

## Create the GitHub release

1. Open the repository's **Releases** page.
2. Choose **Draft a new release**.
3. Create the version tag from the verified `main` commit.
4. Use the version as the release title.
5. Generate release notes, then edit them for clarity and accuracy.
6. Attach the verified portable ZIP.
7. Include SHA-256 checksums for downloadable binaries.
8. Mark alpha, beta, or release candidate builds as pre-releases.
9. Publish only after the final package audit.

## Release notes structure

```markdown
## What's included
- ...

## Supported conversions
- ...

## Safety and privacy
- ...

## Known limitations
- ...

## Verification
- commit: ...
- tests: ...
- manual checks: ...

## Downloads
- Windows portable ZIP
- SHA-256 checksums
```

## After publishing

- update `CHANGELOG.md`;
- verify README download links;
- download the public asset and compare its checksum;
- launch the downloaded package once more;
- close the release Issue only after the public artifact is verified.
