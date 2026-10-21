$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path ([IO.Path]::GetTempPath()) ('trashvanish-checks-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $sources = @(
        (Join-Path $project 'TrashVanish/Classes/Worker.cs'),
        (Join-Path $project 'TrashVanish/Classes/Models/RuleModel.cs'),
        (Join-Path $project 'TrashVanish/Classes/Models/SetModel.cs'),
        (Join-Path $project 'TrashVanish/Classes/Models/setExtensionModel.cs'),
        (Join-Path $PSScriptRoot 'CoreChecks.cs')
    )
    & $compiler /nologo /target:exe "/out:$output" /reference:System.Core.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    & $output
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
} finally { if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output } }
