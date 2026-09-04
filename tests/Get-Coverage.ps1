[CmdletBinding()]
param(
    [string] $ResultsDirectory = (Join-Path $PSScriptRoot '../artifacts/coverage'),
    [switch] $ShowUncovered
)

$ErrorActionPreference = 'Stop'
$reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter coverage.cobertura.xml -Recurse)
if ($reports.Count -eq 0) {
    throw "No coverage.cobertura.xml reports found in $ResultsDirectory. Run the coverage command in tests/README.md first."
}

$seenReports = @{}
foreach ($report in $reports) {
    # TRX logging can copy the same attachment into an additional results folder.
    $hash = (Get-FileHash -LiteralPath $report.FullName).Hash
    if ($seenReports.ContainsKey($hash)) { continue }
    $seenReports[$hash] = $true
    [xml] $coverage = Get-Content -LiteralPath $report.FullName -Raw
    foreach ($package in $coverage.coverage.packages.package) {
        $lines = @($package.classes.class | ForEach-Object { $_.lines.line })
        $coveredLines = @($lines | Where-Object { [long] $_.hits -gt 0 }).Count
        $coveredBranches = 0
        $totalBranches = 0
        foreach ($line in $lines) {
            if ($line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
                $coveredBranches += [int] $Matches[1]
                $totalBranches += [int] $Matches[2]
            }
        }
        [pscustomobject] @{
            Assembly = $package.name
            'Lines %' = [math]::Round(100 * $coveredLines / $lines.Count, 2)
            'Branches %' = if ($totalBranches) { [math]::Round(100 * $coveredBranches / $totalBranches, 2) } else { 100 }
            'Covered lines' = "$coveredLines/$($lines.Count)"
            'Covered branches' = "$coveredBranches/$totalBranches"
            Report = $report.FullName
        } | Format-List

        if ($ShowUncovered) {
            foreach ($class in $package.classes.class) {
                $uncovered = @($class.lines.line | Where-Object { [long] $_.hits -eq 0 })
                if ($uncovered.Count -gt 0) {
                    [pscustomobject] @{
                        Class = $class.name
                        File = $class.filename
                        'Uncovered lines' = ($uncovered.number -join ', ')
                    } | Format-List
                }
            }
        }
    }
}
