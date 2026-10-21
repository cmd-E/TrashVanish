# Tests

`TrashVanish.Tests` is an xUnit project in `TrashVanish.sln`. It references the production assembly, with internal access granted only to the test assembly. The application still targets .NET Framework 4.6.1; the test host targets .NET Framework 4.8.

On Windows, install the .NET SDK, Visual Studio desktop build tools, and the .NET Framework 4.6.1 and 4.8 targeting packs. From the repository root:

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

## Verified results

The initial suite passed all 72 cases with no failures or skips. Measured production line coverage was 100% for `RulePlanner`, `PlannedRule`, and `currentCulture`; 99.5% for `DatabaseStore`; 91.4% for `GridUpdater`; and 83.7% for `Worker`. Overall application line coverage was 19.9%, including the uncovered forms, generated designer/resource code, and static database facade. The facade is also exercised by the separate SQLite smoke check. These measurements describe the initial run, not a coverage threshold or a promise of complete UI coverage.
