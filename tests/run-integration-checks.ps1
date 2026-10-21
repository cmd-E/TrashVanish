param([string]$BuildDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(!$BuildDirectory){$BuildDirectory=Join-Path $projectRoot '.artifacts/build'}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('trashvanish-integration-'+[Guid]::NewGuid().ToString('N'))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $BuildDirectory 'TrashVanish.exe') -Destination $fixture
    Get-ChildItem -LiteralPath $BuildDirectory -Filter '*.dll' | Copy-Item -Destination $fixture
    foreach($architecture in @('x64','x86')){Copy-Item -LiteralPath (Join-Path $BuildDirectory $architecture) -Destination $fixture -Recurse}
    '<configuration><connectionStrings><add name="Default" connectionString="Data Source=trashVanish.db;Version=3;" /></connectionStrings></configuration>' | Set-Content -LiteralPath (Join-Path $fixture 'Checks.exe.config')
    $references=@('/reference:System.Core.dll','/reference:System.Data.dll',('/reference:'+(Join-Path $fixture 'TrashVanish.exe')),('/reference:'+(Join-Path $fixture 'System.Data.SQLite.dll')))
    $output=Join-Path $fixture 'Checks.exe'
    & $compiler /nologo /target:exe "/out:$output" $references (Join-Path $PSScriptRoot 'DatabaseChecks.cs')
    if($LASTEXITCODE -ne 0){throw 'Integration check compilation failed'}
    & $output
    if($LASTEXITCODE -ne 0){throw 'Integration checks failed'}
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    $temporaryRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if(!$resolved.StartsWith($temporaryRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe fixture cleanup target'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
