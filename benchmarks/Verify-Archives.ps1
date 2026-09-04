param([switch]$Seal)
$ErrorActionPreference = 'Stop'
foreach ($archive in Get-ChildItem -LiteralPath "$PSScriptRoot/archives" -Directory) {
    $manifestPath = Join-Path $archive.FullName 'sha256.json'
    $entries = @(Get-ChildItem -LiteralPath $archive.FullName -Recurse -File |
        Where-Object { $_.Extension -in '.cs', '.csproj' -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Sort-Object FullName | ForEach-Object {
            [ordered]@{ path = [IO.Path]::GetRelativePath($archive.FullName, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
    if ($Seal -and -not (Test-Path -LiteralPath $manifestPath)) {
        ConvertTo-Json -InputObject $entries -Depth 3 | Set-Content -LiteralPath $manifestPath
    } else {
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Unsealed archive: $($archive.Name)" }
        $expected = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
        if ($entries.Count -ne $expected.Count) { throw "File count changed: $($archive.Name)" }
        for ($i = 0; $i -lt $entries.Count; $i++) {
            if ($entries[$i].path -ne $expected[$i].path -or $entries[$i].sha256 -ne $expected[$i].sha256) {
                throw "Archive changed: $($archive.Name)/$($entries[$i].path)"
            }
        }
    }
    Write-Output "Verified $($archive.Name)"
}
