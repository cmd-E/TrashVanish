param(
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [string]$BaselinePath = (Join-Path $PSScriptRoot '../.github/coverage-baseline.json'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../.artifacts/coverage'),
    [string]$SummaryPath = $env:GITHUB_STEP_SUMMARY
)
$ErrorActionPreference = 'Stop'
$baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
$thresholds = @($baseline.minimumLinePercent.PSObject.Properties)
if (!$baseline.assembly -or $thresholds.Count -eq 0) { throw 'Coverage baseline must name an assembly and at least one class.' }
foreach ($threshold in $thresholds) {
    if ($null -eq $threshold.Value -or $threshold.Value -isnot [ValueType] -or
        [double]$threshold.Value -lt 0 -or [double]$threshold.Value -gt 100) {
        throw "Invalid coverage threshold for $($threshold.Name)."
    }
}

# Never resolve external entities from a coverage report.
$settings = New-Object System.Xml.XmlReaderSettings
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$reader = [System.Xml.XmlReader]::Create([IO.Path]::GetFullPath($ReportPath), $settings)
try {
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    $document.Load($reader)
} finally { $reader.Dispose() }
$packages = @($document.coverage.packages.package | Where-Object { $_.name -eq $baseline.assembly })
if ($packages.Count -ne 1) { throw "Expected one coverage package named $($baseline.assembly)." }
$package = $packages[0]
$overall = 100 * [double]::Parse($package.'line-rate', [Globalization.CultureInfo]::InvariantCulture)
$rows = foreach ($threshold in $thresholds) {
    $classes = @($package.classes.class | Where-Object { $_.name -eq $threshold.Name })
    $covered = 0
    $total = 0
    if ($classes.Count -eq 1) {
        foreach ($line in @($classes[0].lines.line)) {
            if ($null -eq $line) { continue }
            if ($line.hits -notmatch '^\d+$') { throw "Invalid hit count for $($threshold.Name)." }
            $total++
            if ([long]$line.hits -gt 0) { $covered++ }
        }
    }
    $percent = if ($total -gt 0) { 100.0 * $covered / $total } else { 0.0 }
    $passed = $classes.Count -eq 1 -and $total -gt 0 -and $percent + 0.000001 -ge [double]$threshold.Value
    [pscustomobject]@{
        class = $threshold.Name
        coveredLines = $covered
        totalLines = $total
        linePercent = [Math]::Round($percent, 3)
        minimumLinePercent = [double]$threshold.Value
        passed = $passed
    }
}
$failed = @($rows | Where-Object { !$_.passed })
$result = [ordered]@{
    assembly = $baseline.assembly
    commit = $env:GITHUB_SHA
    overallLinePercent = [Math]::Round($overall, 3)
    passed = $failed.Count -eq 0
    classes = @($rows)
}
$culture = [Globalization.CultureInfo]::InvariantCulture
$markdown = @(
    '## Core coverage',
    '',
    'Class | Covered lines | Line coverage | Minimum | Result',
    '--- | ---: | ---: | ---: | ---'
)
foreach ($row in $rows) {
    $markdown += '{0} | {1}/{2} | {3}% | {4}% | {5}' -f $row.class, $row.coveredLines, $row.totalLines,
        $row.linePercent.ToString('0.###', $culture), $row.minimumLinePercent.ToString('0.###', $culture),
        $(if ($row.passed) { 'PASS' } else { 'FAIL (missing or below minimum)' })
}
$markdown += @('', ('Overall application line coverage: {0}% (reported, not gated; includes UI/generated code).' -f
    $result.overallLinePercent.ToString('0.###', $culture)), '')
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'coverage-summary.json') -Encoding utf8
$markdown | Set-Content -LiteralPath (Join-Path $OutputDirectory 'coverage-summary.md') -Encoding utf8
if ($SummaryPath) { $markdown | Add-Content -LiteralPath $SummaryPath -Encoding utf8 }
$markdown | Write-Output
if ($failed.Count -gt 0) { throw ('Core coverage gate failed: ' + (($failed | ForEach-Object { $_.class }) -join ', ')) }
