Run ./tests/run-core-checks.ps1 in PowerShell on Windows. Checks compile production planner/executor code and operate only inside a uniquely named disposable temporary directory. They never clean the user's desktop.

The test covers deterministic precedence, fallback after self-copy failure, exact extension matching, staged overwrites, source deletion after finalization, and cancellation. Native SQLite/UI integration additionally requires the project's declared SQLite dependency.
