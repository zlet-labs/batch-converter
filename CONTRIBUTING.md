# Contributing to Zlet Folder Converter

Thanks for helping improve a small local utility instead of inventing another platform with twelve dashboards.

## Before you start

- Search existing Issues and Pull Requests.
- Every implementation change must have a GitHub Issue.
- Use the issue's `ZL-XXX` identifier in the issue title, branch, PR title, commit, and merge message.
- Do not reuse an existing Zlet Labs task identifier.
- Keep changes focused. Do not mix application logic, repository presentation, dependencies, and release tooling without an explicit issue scope.

## Branch and PR naming

```text
Branch: zl-xxx-short-description
PR title: CU-ZL-XXX Short description
Merge message: CU-ZL-XXX[complete] Short description
```

Preferred merge method: **Squash and merge**.

## Development setup

Requirements:

- Windows 10 or Windows 11 x64;
- .NET 8 SDK;
- PowerShell.

```powershell
git fetch origin main
git checkout -B zl-xxx-short-description origin/main
dotnet restore FolderConverter.sln
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
```

## Product constraints

- Keep the tool local and self-serve.
- Do not add accounts, backend storage, cloud conversion, analytics, telemetry, remote config, or forced onboarding unless an Issue explicitly requests it.
- Do not silently migrate, delete, move, or overwrite user files.
- Do not store passwords, API keys, tokens, or document contents in logs or exports.
- Do not claim a conversion mapping works until it is implemented and verified.
- Preserve mobile-independent desktop usability at 1366×768 and common Windows scaling levels.
- Keep important actions visible. A toast must not be the only source of important file status.

## Tests and verification

Run at minimum:

```powershell
dotnet build FolderConverter.sln -c Release
dotnet test FolderConverter.sln -c Release
git diff --check
git diff --stat
git status
```

Visible UI, conversion, storage, export, packaging, or configuration changes also require a manual self-check.

Do not claim full end-to-end testing unless the exact commands were executed successfully. When it was not run, state:

```text
Full e2e not run by agent. Local verification required before merge.
```

## Test data

Use synthetic or sanitized fixtures only.

Never commit or attach:

- customer or employer documents;
- identity, financial, medical, or legal records;
- passwords, tokens, API keys, or certificates;
- absolute local user paths;
- generated portable artifacts or third-party runtime binaries unless an Issue explicitly defines the release process.

## Pull request checklist

- [ ] Linked GitHub Issue exists.
- [ ] Scope matches the Issue.
- [ ] Public product claims match actual behavior.
- [ ] Originals are preserved and conflicts are safe.
- [ ] Build and tests pass.
- [ ] Manual verification is documented where required.
- [ ] Exact files touched are listed.
- [ ] Known limitations are stated honestly.
- [ ] No secrets or private documents are included.

## Self-check report

Use this structure in the PR or final task report:

```text
Pass/Fail Checklist:
- ...

Exact Files Touched:
- ...

Problems Found:
- ...

If Fixes Are Needed:
- ...
```

## Code of conduct

Be direct, specific, and respectful. Critique code and behavior, not people. Security reports and private documents do not belong in public Issues.
