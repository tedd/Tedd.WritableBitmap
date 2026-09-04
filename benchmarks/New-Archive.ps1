param([Parameter(Mandatory)][string]$Name, [string]$From = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = if ($From) { Join-Path $PSScriptRoot "archives/$From" } else { Join-Path $root 'src/Tedd.WriteableBitmap.Maui' }
$destination = Join-Path $PSScriptRoot "archives/$Name"
if (Test-Path -LiteralPath $destination) { throw "Archive already exists: $destination" }
New-Item -ItemType Directory -Path "$destination/Properties" -Force | Out-Null
Get-ChildItem -LiteralPath $source -Filter '*.cs' | Copy-Item -Destination $destination
Copy-Item -LiteralPath "$source/Properties/AssemblyInfo.cs" -Destination "$destination/Properties/AssemblyInfo.cs"
$assemblyInfo = "$destination/Properties/AssemblyInfo.cs"
if (-not (Select-String -LiteralPath $assemblyInfo -SimpleMatch 'Tedd.WriteableBitmap.Benchmarks' -Quiet)) {
    Add-Content -LiteralPath $assemblyInfo -Value '[assembly: InternalsVisibleTo("Tedd.WriteableBitmap.Benchmarks")]'
}
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>Tedd.WriteableBitmap.Archive.ARCHIVE_NAME</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SkiaSharp.Views.Maui.Controls" Version="4.151.2" />
  </ItemGroup>
</Project>
'@
$project.Replace('ARCHIVE_NAME', $Name) | Set-Content -LiteralPath "$destination/$Name.csproj"
