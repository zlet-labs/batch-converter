# Security Policy

## Supported versions

Security fixes are provided for the latest published release. Before the first public release, reports are evaluated against the current `main` branch and active release candidate.

## Reporting a vulnerability

Do not open a public Issue for a vulnerability that could expose files, paths, credentials, or arbitrary code execution.

Report the problem privately through GitHub's security reporting feature when it is available for this repository. If private reporting is not available, open a minimal public Issue that contains no exploit details, secrets, local paths, or source documents and asks the maintainers for a private contact route.

Include only what is necessary:

- affected application version or commit;
- Windows version;
- affected source and target format;
- concise reproduction conditions;
- expected and actual security impact;
- whether the issue can modify, delete, overwrite, upload, or disclose files.

## Never attach real documents

Use a synthetic or sanitized fixture that reproduces the problem.

Do not attach:

- customer or employer documents;
- personal, financial, medical, identity, or legal records;
- credentials, certificates, tokens, or API keys;
- files containing malware or active exploit payloads;
- screenshots exposing private local paths or document contents.

## Security expectations

Zlet Folder Converter is designed to:

- process files locally;
- avoid network uploads;
- preserve source files;
- prevent output path traversal;
- refuse to overwrite existing files or directories;
- isolate per-file failures;
- avoid logging document contents or secrets.

A regression in any of these guarantees should be treated as a security or data-safety issue.

## Disclosure

Please allow maintainers reasonable time to investigate, prepare a fix, and publish a release before public disclosure. Confirmed reports will be credited when desired and when doing so does not reveal sensitive information.
