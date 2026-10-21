param(
    [string]$MSBuildPath,
    [switch]$UnitOnly,
    [switch]$Coverage
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path $projectRoot 'TrashVanish.Tests/TrashVanish.Tests.csproj'

# The application retains .NET Framework 4.6.1; only the test host targets 4.8.
# Development builds do not need the original publisher's signing certificate.
& (Join-Path $PSScriptRoot 'build.ps1') -MSBuildPath $MSBuildPath

$testArgs = @('test', $testProject, '--no-build', '--no-restore', '--results-directory',
    (Join-Path $projectRoot '.artifacts/test-results'), '--logger', 'trx;LogFileName=tests.trx')
if ($UnitOnly) { $testArgs += @('--filter', 'Category!=Integration') }
if ($Coverage) { $testArgs += @('--collect', 'Code Coverage;Format=Cobertura') }
& dotnet @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
