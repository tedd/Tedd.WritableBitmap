param(
    [string[]]$Filter = @('*'),
    [string]$ResultsDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $ResultsDirectory) {
    $ResultsDirectory = Join-Path $PSScriptRoot ('results/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Force -Path "$ResultsDirectory/tests" | Out-Null
& "$PSScriptRoot/Verify-Archives.ps1"

Push-Location $root
try {
    # V05 fails collection tests; V06 was superseded after a new-frame benchmark regression.
    foreach ($version in 'V00', 'V01', 'V02', 'V03', 'V04', 'V07', 'V08') {
        & dotnet test tests/Tedd.WriteableBitmap.Maui.Tests/Tedd.WriteableBitmap.Maui.Tests.csproj `
            -c Release -f net10.0 "-p:BitmapArchive=$version" `
            --logger "trx;LogFileName=$version.trx" --results-directory "$ResultsDirectory/tests"
        if ($LASTEXITCODE -ne 0) { throw "Unit-test gate failed: $version" }
    }
    foreach ($project in 'Maui', 'Wpf') {
        & dotnet test "tests/Tedd.WriteableBitmap.$project.Tests/Tedd.WriteableBitmap.$project.Tests.csproj" `
            -c Release --logger "trx;LogFilePrefix=production-$project" --results-directory "$ResultsDirectory/tests"
        if ($LASTEXITCODE -ne 0) { throw "Production unit-test gate failed: $project" }
    }
    $benchmarkArguments = @('run', '--project', "$PSScriptRoot/Tedd.WriteableBitmap.Benchmarks.csproj", '-c', 'Release', '--', '--filter') + $Filter + @('--artifacts', $ResultsDirectory)
    & dotnet @benchmarkArguments
    if ($LASTEXITCODE -ne 0) { throw 'BenchmarkDotNet failed.' }
    # BenchmarkDotNet can exit successfully after a generated-project build failure.
    $reports = @(Get-ChildItem -LiteralPath "$ResultsDirectory/results" -Filter '*-report.csv' -ErrorAction SilentlyContinue)
    if ($reports.Count -eq 0) { throw 'No benchmark result reports were produced.' }
    foreach ($report in $reports) {
        foreach ($row in Import-Csv -LiteralPath $report.FullName) {
            if (-not $row.Mean -or $row.Mean -eq 'NA') { throw "Missing measurement: $($report.Name)/$($row.Method)" }
        }
    }
    & "$PSScriptRoot/Verify-Archives.ps1"
} finally {
    Pop-Location
}
