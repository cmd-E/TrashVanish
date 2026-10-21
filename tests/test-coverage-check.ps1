$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('TrashVanish.CoverageChecks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$report = Join-Path $fixture 'report.xml'
$baseline = Join-Path $fixture 'baseline.json'
$output = Join-Path $fixture 'output'
$summary = Join-Path $fixture 'summary.md'
$checks = 0

function Verify-Case([string]$Name, [string]$Xml, [string]$Policy, [bool]$ShouldPass) {
    $Xml | Set-Content -LiteralPath $report
    $Policy | Set-Content -LiteralPath $baseline
    $failure = $null
    try {
        & (Join-Path $PSScriptRoot 'check-coverage.ps1') -ReportPath $report -BaselinePath $baseline `
            -OutputDirectory $output -SummaryPath $summary | Out-Null
    } catch { $failure = $_ }
    if (($null -eq $failure) -ne $ShouldPass) { throw "Unexpected coverage result for $Name`: $failure" }
    $script:checks++
}

try {
    $policy = '{"assembly":"TrashVanish","minimumLinePercent":{"Example.Core":80}}'
    $lines = '<line number="1" hits="1"/><line number="2" hits="1"/><line number="3" hits="2"/><line number="4" hits="1"/><line number="5" hits="0"/>'
    $xml = '<coverage><packages><package name="TrashVanish" line-rate="0.2"><classes><class name="Example.Core"><lines>' + $lines + '</lines></class></classes></package></packages></coverage>'
    Verify-Case 'exact boundary passes despite low overall coverage' $xml $policy $true
    $result = Get-Content (Join-Path $output 'coverage-summary.json') -Raw | ConvertFrom-Json
    if ($result.classes[0].linePercent -ne 80 -or $result.overallLinePercent -ne 20 -or !$result.passed) {
        throw 'Summary counts or overall coverage are incorrect.'
    }
    if ((Get-Content $summary -Raw) -notmatch 'Example.Core.*80%.*PASS') { throw 'Step summary was not written.' }
    Verify-Case 'fractional threshold is not rounded down' $xml ($policy.Replace(':80', ':80.01')) $false
    $result = Get-Content (Join-Path $output 'coverage-summary.json') -Raw | ConvertFrom-Json
    if ($result.passed) { throw 'Failure summary incorrectly passed.' }
    Verify-Case 'missing monitored class' ($xml.Replace('Example.Core', 'Other.Class')) $policy $false
    Verify-Case 'missing production assembly' ($xml.Replace('name="TrashVanish"', 'name="Tests"')) $policy $false
    Verify-Case 'duplicate monitored class' ($xml.Replace('</classes>', '<class name="Example.Core"/></classes>')) $policy $false
    Verify-Case 'empty class' ($xml.Replace($lines, '')) $policy $false
    Verify-Case 'invalid hit count' ($xml.Replace('hits="0"', 'hits="NaN"')) $policy $false
    Verify-Case 'malformed XML' '<coverage>' $policy $false
    Verify-Case 'external entity prohibited' ('<!DOCTYPE coverage [<!ENTITY x SYSTEM "file:///does-not-exist">]>' + $xml) $policy $false
    Verify-Case 'empty baseline' $xml '{"assembly":"TrashVanish","minimumLinePercent":{}}' $false
    Verify-Case 'out-of-range threshold' $xml ($policy.Replace(':80', ':101')) $false
    Write-Output "Coverage checker: $checks checks passed."
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) {
        throw 'Unsafe coverage fixture cleanup path.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
