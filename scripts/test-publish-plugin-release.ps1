[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scriptPath = Join-Path $projectRoot 'scripts\publish-plugin-release.ps1'
$version = '0.1.0-test'
$pluginCode = 'patient-registration'
$releaseRoot = Join-Path $projectRoot "artifacts\plugin-releases\$pluginCode\$version"
$zipPath = Join-Path $releaseRoot "$pluginCode-$version.zip"
$manifestPath = Join-Path $releaseRoot 'plugin-release-manifest.json'
$publishRoot = Join-Path $releaseRoot 'publish'
$loaderRoot = Join-Path $releaseRoot 'manifest-loader'

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

& 'C:\Program Files\PowerShell\7\pwsh.exe' -File $scriptPath `
    -PluginId PatientRegistration `
    -Version $version `
    -Channel stable `
    -SkipRestore

if ($LASTEXITCODE -ne 0) {
    throw "publish-plugin-release.ps1 failed with exit code $LASTEXITCODE"
}

$requiredFiles = @(
    'plugin.json',
    'plugin.settings.json',
    'PatientRegistration.Plugin.dll',
    'PatientRegistration.Plugin.deps.json',
    'QRCoder.dll',
    'Npgsql.dll'
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $publishRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing expected plugin package file: $path"
    }
}

if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Missing plugin zip: $zipPath"
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Missing plugin release manifest: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.pluginCode -ne $pluginCode) {
    throw "Unexpected pluginCode: $($manifest.pluginCode)"
}

if ($manifest.version -ne $version) {
    throw "Unexpected version: $($manifest.version)"
}

if ([string]::IsNullOrWhiteSpace($manifest.sha256)) {
    throw 'Manifest sha256 is empty.'
}

$packagedPluginJsonPath = Join-Path $publishRoot 'plugin.json'
$packagedPluginJson = Get-Content -LiteralPath $packagedPluginJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($packagedPluginJson.version -ne $version) {
    throw "Unexpected packaged plugin.json version: $($packagedPluginJson.version)"
}

New-Item -ItemType Directory -Path $loaderRoot -Force | Out-Null
$loaderProjectPath = Join-Path $loaderRoot 'PluginManifestLoader.csproj'
$loaderProgramPath = Join-Path $loaderRoot 'Program.cs'
$abstractionsPath = Join-Path $publishRoot 'DigitalIntelligenceBridge.Plugin.Abstractions.dll'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="DigitalIntelligenceBridge.Plugin.Abstractions">
      <HintPath>$abstractionsPath</HintPath>
    </Reference>
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $loaderProjectPath -Encoding UTF8

@'
using System.Reflection;
using System.Runtime.Loader;
using DigitalIntelligenceBridge.Plugin.Abstractions;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: PluginManifestLoader <publishRoot> <expectedVersion>");
    return 1;
}

var publishRoot = args[0];
var expectedVersion = args[1];
var pluginAssemblyPath = Path.Combine(publishRoot, "PatientRegistration.Plugin.dll");
var resolver = new AssemblyDependencyResolver(pluginAssemblyPath);

AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
{
    var resolvedPath = resolver.ResolveAssemblyToPath(assemblyName);
    return resolvedPath is null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(resolvedPath);
};

var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginAssemblyPath);
var pluginType = assembly.GetType("PatientRegistration.Plugin.PatientRegistrationPlugin", throwOnError: true)
    ?? throw new InvalidOperationException("PatientRegistration plugin type not found.");
var module = (IPluginModule)(Activator.CreateInstance(pluginType)
    ?? throw new InvalidOperationException("PatientRegistration plugin instance could not be created."));
var moduleManifest = module.GetManifest();

if (moduleManifest.Version != expectedVersion)
{
    Console.Error.WriteLine($"Unexpected module manifest version: {moduleManifest.Version}; expected: {expectedVersion}");
    return 2;
}

Console.WriteLine($"Module manifest version: {moduleManifest.Version}");
return 0;
'@ | Set-Content -LiteralPath $loaderProgramPath -Encoding UTF8

dotnet run --project $loaderProjectPath -c Release -- $publishRoot $version
if ($LASTEXITCODE -ne 0) {
    throw "Plugin manifest loader failed with exit code $LASTEXITCODE"
}

Write-Host 'publish-plugin-release.ps1 test passed.' -ForegroundColor Green
