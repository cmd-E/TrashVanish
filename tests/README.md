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

The job uploads TRX test results and Cobertura coverage reports as `windows-test-results`, retained for 14 days. Upload is attempted even after test failure. No secrets or write permissions are needed, and the workflow does not publish releases. Once it has run on GitHub, maintainers can make `Windows build and tests` a required check in repository branch protection or rulesets.

## Verified results

The initial suite passed all 72 cases with no failures or skips. Measured production line coverage was 100% for `RulePlanner`, `PlannedRule`, and `currentCulture`; 99.5% for `DatabaseStore`; 91.4% for `GridUpdater`; and 83.7% for `Worker`. Overall application line coverage was 19.9%, including the uncovered forms, generated designer/resource code, and static database facade. The facade is also exercised by the separate SQLite smoke check. These measurements describe the initial run, not a coverage threshold or a promise of complete UI coverage.
