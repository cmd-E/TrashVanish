# Tests

`TrashVanish.Tests` is an xUnit project in `TrashVanish.sln`. It references the production assembly, with internal access granted only to the test assembly. The application still targets .NET Framework 4.6.1; the test host targets .NET Framework 4.8.

On Windows, install the .NET SDK, Visual Studio desktop build tools, and .NET Framework 4.8. Reference assemblies for compilation are restored from NuGet, so the old 4.6.1 targeting pack does not need to be installed. From the repository root:

```powershell
./tests/run-tests.ps1
./tests/run-tests.ps1 -UnitOnly
./tests/run-tests.ps1 -Coverage
```

The runner restores NuGet dependencies and builds the application into ignored `.artifacts` directories. It disables ClickOnce manifest signing for this development build. Visual Studio Test Explorer also discovers the tests. The production SQLite package remains in `packages` for compatibility with the existing project.

## Scope and FIRST

- **Fast:** pure planner and culture tests run without disk, database, network, timers, or UI. Each case checks one behavior, using theories for related inputs.
- **Independent:** each file/SQLite integration test owns a fresh disposable directory. Database stores receive explicit connection strings. Grid tests receive loader delegates and create/dispose their own controls on an STA thread. Tests never clean the desktop or open the application's database.
- **Repeatable:** fixed inputs, no sleeps or external services. Culture tests restore the thread culture in `finally`. Filesystem tests run only on Windows, matching the product's platform.
- **Self-validating:** assertions check results, preserved source/destination contents, progress outcomes, and transaction rollback. No manual output inspection is required.
- **Timely:** regression cases accompany the culture and grid changes; the existing behavior receives characterization coverage. These are retrospective tests, not a claim that the original implementation was developed test-first.

The pure unit suite covers planner precedence, numeric ID ordering, path normalization, exact extensions, case sensitivity, immutable plans, validation, and automatic regional/parent/fallback culture selection. Bundled resources are checked for every operation outcome.

Tests tagged `Category=Integration` cover real filesystem copy/move/overwrite, fallback, locked source preservation, deletion failure, cancellation, async execution, SQLite CRUD/duplicates/SQL parameters/transaction rollback/orphan detection, and grid refresh failure/success. Separate them with `--filter Category=Integration` or `--filter Category!=Integration` when using `dotnet test --no-build --no-restore` directly.

WinForms event handlers, dialogs, designer-generated code, settings persistence, and full interactive window flows are not covered by these tests. They still need UI testing; the suite does not claim complete application coverage. Cobertura reports from `-Coverage` are written under `.artifacts/test-results` and include these uncovered application areas.

The older `run-core-checks.ps1` and `run-integration-checks.ps1` smoke checks remain available. The latter expects a development build in `.artifacts/build`.

## GitHub Actions

`.github/workflows/ci.yml` runs on pushes and pull requests, and supports manual runs through the Actions tab. The `Windows build and tests` job restores dependencies, builds the application and test project, runs the complete xUnit suite with coverage, and runs both smoke checks. Failed commands fail the job. New commits cancel older runs for the same branch or pull request.

CI uses `windows-2022` and the latest patched .NET 8.0.4xx SDK for compatibility with Visual Studio 2022. A temporary `global.json` on the runner prevents preinstalled newer SDKs from being selected. The application remains on .NET Framework 4.6.1 and the tests on 4.8. The development-only reference assembly package supplies the old framework at compile time and is not shipped with the application.

The test job uploads TRX results, Cobertura reports, and Markdown/JSON coverage summaries as `windows-test-results`, retained for 14 days. Upload is attempted even after failure. It needs only read access. The separate `CodeQL (C#)` job uses `security-events: write` to upload security results. The workflow does not publish releases or require stored secrets.

### Coverage tracking

Each test run publishes a class coverage table in the Actions job summary. `.github/coverage-baseline.json` gates production classes: 100% line coverage for planning, matching, and culture selection; 99% for persistence; 91% for grid updates; and 83% for the worker. These are conservative minimums based on the first measured suite. Lowering them should be an explicit reviewed policy change. Missing classes or reports fail the gate. Overall application coverage is reported without a minimum because it includes uncovered forms and generated code.

Run the gate locally after `./tests/run-tests.ps1 -Coverage`:

```powershell
$report = Get-ChildItem .artifacts/test-results -Recurse -Filter '*.cobertura.xml' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
./tests/check-coverage.ps1 -ReportPath $report.FullName
./tests/test-coverage-check.ps1
```

The checker has self-tests for threshold boundaries, missing/duplicate classes, malformed reports, XML external entities, and invalid policies. JSON artifacts record the commit and measured coverage for comparison across runs; no external coverage service is required.

### CodeQL and merge protection

CodeQL traces a clean Windows rebuild using the same `tests/build.ps1` helper as the tests, then runs the C# `security-extended` query suite. Findings appear in GitHub's code scanning alerts. A successful analysis job means the scan completed, not that it found no alerts; alerts still require review.

`.github/branch-ruleset.json` declares the repository's default-branch policy. It requires pull requests, resolved conversations, an up-to-date branch, and passing `Windows build and tests` (including the coverage gate) and `CodeQL (C#)` checks from GitHub Actions. Force pushes and deletion are blocked, with no bypass actors. It deliberately requires zero approving reviews so a sole maintainer can merge their own passing pull request. This JSON is a policy record, not an automatically executed workflow; rules must be applied through GitHub's ruleset API/settings. Existing rules must be inspected before changing them.

## Verified results

The initial suite passed all 72 cases with no failures or skips. Measured production line coverage was 100% for `RulePlanner`, `PlannedRule`, and `currentCulture`; 99.5% for `DatabaseStore`; 91.4% for `GridUpdater`; and 83.7% for `Worker`. Overall application line coverage was 19.9%, including the uncovered forms, generated designer/resource code, and static database facade. The facade is also exercised by the separate SQLite smoke check. These measurements describe the initial run, not a coverage threshold or a promise of complete UI coverage.
