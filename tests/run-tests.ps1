param(
    [string]$MSBuildPath,
    [switch]$UnitOnly,
    [switch]$Coverage
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $projectRoot 'TrashVanish.sln'
$testProject = Join-Path $projectRoot 'TrashVanish.Tests/TrashVanish.Tests.csproj'

if (!$MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio Build Tools with .NET desktop build tools, or supply -MSBuildPath.' }
    $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild Microsoft.NetCore.Component.SDK -find 'MSBuild\**\Bin\MSBuild.exe' |
        Select-Object -First 1
}
if (!$MSBuildPath) { throw 'MSBuild was not found.' }

# The application retains .NET Framework 4.6.1; only the test host targets 4.8.
# Development builds do not need the original publisher's signing certificate.
& $MSBuildPath $solution /t:Restore /p:RestorePackagesConfig=true /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
& $MSBuildPath $testProject /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$testArgs = @('test', $testProject, '--no-build', '--no-restore', '--results-directory',
    (Join-Path $projectRoot '.artifacts/test-results'))
if ($UnitOnly) { $testArgs += @('--filter', 'Category!=Integration') }
if ($Coverage) { $testArgs += @('--collect', 'Code Coverage;Format=Cobertura') }
& dotnet @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
