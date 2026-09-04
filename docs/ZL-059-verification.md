# ZL-059 verification

Base: `f5ef38d01ccdc8eb5612c5b83416932e5f863b9d` (`origin/main`).
Branch: `zl-059-settings-update-check`.

## Automated checks and source review

| Check | Result |
| --- | --- |
| Existing main-window Settings entry retained | PASS |
| Existing LocalizationService and AppSettingsStore reused; bootstrap unchanged | PASS |
| Running version from ProductIdentity; no version bump | PASS |
| No update request on construction, opening, startup, timer or background schedule | PASS |
| Requests only from explicit Check button; canonical Releases API | PASS |
| Drafts, prereleases, malformed tags and unusable entries ignored | PASS |
| Numeric semantic comparison; equal/older versions not offered | PASS |
| Selected release retains its own validated GitHub tag-page URL | PASS |
| Persistent localized checking/current/available/error states | PASS |
| Offline, connection/DNS failures, timeout, HTTP/rate limit, invalid JSON/schema and empty data handled | PASS |
| No download/install/elevation or executable replacement | PASS |
| Diagnostics uses allowlisted fields and existing Office capability detector | PASS |
| Clipboard copies the same generated text displayed in Settings | PASS |
| No usernames, machine names, paths, document data, environment values, tokens or logs in diagnostics | PASS |
| About URLs match request; LICENSE verified as MIT | PASS |
| Local conversion and explicit GitHub network disclosure visible | PASS |
| Reset defaults to No; cancellation never deletes settings | PASS |
| Reset removes only store file, including corrupt JSON; locked-file failure handled | PASS |
| Reset restores existing next-launch language chooser semantics | PASS |
| Unrelated JSON keys preserved by normal writes | PASS |
| User files, results, ZIP files, reports and Downloads untouched | PASS |
| RU/EN key parity and nonempty values | PASS |
| Minimum-size WPF layout construction and scrolling | PASS (automated only) |
| Conversion engine, Stop, cancellation, Office ownership and ZIP code unchanged | PASS |
| No dependencies/backend/cloud/telemetry/remote config added | PASS |
| git diff --check | PASS |
| Release restore/build | PASS: 0 warnings, 0 errors |
| Normal tests | PASS: 317 passed; 4 opt-in Office tests skipped |
| GitHub CI | See PR checks for final status |

Update fetching uses a 15-second overall cancellation deadline and at most ten pages of 100 releases. An incomplete listing produces an error rather than an up-to-date claim. Tests use deterministic HTTP handlers with no live GitHub traffic.

The PATH dotnet installation has no SDK. Verification used the existing .NET SDK 8.0.424 at `%TEMP%\zlet-dotnet-sdk-8\dotnet.exe` with the requested restore/build/test arguments.

## Exact files touched

- `src/Zlet.FolderConverter.App/SettingsWindow.xaml`
- `src/Zlet.FolderConverter.App/SettingsWindow.xaml.cs`
- `src/Zlet.FolderConverter.App/Settings/AppSettingsStore.cs`
- `src/Zlet.FolderConverter.App/Settings/GitHubUpdateChecker.cs`
- `src/Zlet.FolderConverter.App/Settings/DiagnosticsText.cs`
- `src/Zlet.FolderConverter.App/Resources/Strings.en-US.xaml`
- `src/Zlet.FolderConverter.App/Resources/Strings.ru-RU.xaml`
- `tests/Zlet.FolderConverter.Tests/SettingsUpdateTests.cs`
- `docs/ZL-059-verification.md`

## Problems found and fixes

- Standalone Settings construction initially relied on application-level button styles. Settings now merges the existing shared style dictionary; the construction test passes.
- A test-lambda syntax error was corrected before the successful build and full test run.
- No outstanding implementation defects found in automated checks/source review.

## Manual QA: PENDING

Full e2e not run by agent. Local verification required before merge.

Interactively verify RU/EN switching with results already displayed; window resizing and 125%/150% Windows scaling; keyboard access and scrolling to all actions; clipboard and default-browser handoff; a live manual update check and an offline retry; reset No/Yes and next-launch language selection. Do not mark these checks passed until actually performed.
