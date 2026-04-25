[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [string]$PluginId,
    [string]$Version,
    [string]$Channel = 'stable',
    [switch]$SkipRestore,
    [switch]$SkipZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "`n[STEP] $Message" -ForegroundColor Cyan
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor DarkGray
}

function Write-Utf8Text {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Resolve-ProjectRoot {
    if ($PSScriptRoot) {
        return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    }

    return (Get-Location).Path
}

function Reset-Directory {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-DirectoryContent {
    param(
        [string]$Source,
        [string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Write-PluginJsonVersion {
    param(
        [string]$Path,
        [string]$ReleaseVersion
    )

    $pluginManifest = Read-JsonFile -Path $Path
    $pluginManifest.version = $ReleaseVersion
    Write-Utf8Text -Path $Path -Content ($pluginManifest | ConvertTo-Json -Depth 100)
}

function Assert-RequiredFile {
    param(
        [string]$Root,
        [string]$RelativePath
    )

    $path = Join-Path $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Plugin package is missing required file: $path"
    }
}

function Get-RequiredPluginFiles {
    param(
        [string]$ResolvedPluginId
    )

    $files = @(
        'plugin.json',
        'plugin.settings.json',
        "$ResolvedPluginId.Plugin.dll",
        "$ResolvedPluginId.Plugin.deps.json"
    )

    if ($ResolvedPluginId -eq 'PatientRegistration') {
        $files += @(
            'QRCoder.dll',
            'Npgsql.dll'
        )
    }

    return $files
}

if ([string]::IsNullOrWhiteSpace($PluginId)) {
    throw 'PluginId is required. Example: -PluginId PatientRegistration'
}

$projectRoot = Resolve-ProjectRoot
Set-Location $projectRoot

$pluginSourceDirectory = Join-Path $projectRoot "plugins-src\$PluginId.Plugin"
$pluginProjectPath = Join-Path $pluginSourceDirectory "$PluginId.Plugin.csproj"
$sourcePluginJsonPath = Join-Path $pluginSourceDirectory 'plugin.json'

if (-not (Test-Path -LiteralPath $pluginSourceDirectory)) {
    throw "Plugin source directory not found: $pluginSourceDirectory"
}

if (-not (Test-Path -LiteralPath $pluginProjectPath)) {
    throw "Plugin project file not found: $pluginProjectPath"
}

$sourcePluginJson = Read-JsonFile -Path $sourcePluginJsonPath
$pluginCode = [string]$sourcePluginJson.id
if ([string]::IsNullOrWhiteSpace($pluginCode)) {
    throw "plugin.json is missing id: $sourcePluginJsonPath"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = [string]$sourcePluginJson.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw 'Version is required because plugin.json does not define version.'
}

$pluginOutputPath = Join-Path $pluginSourceDirectory "bin\$Configuration\net10.0"
$repoPluginPath = Join-Path $projectRoot "plugins\$PluginId"
$releaseRoot = Join-Path $projectRoot "artifacts\plugin-releases\$pluginCode\$Version"
$publishRoot = Join-Path $releaseRoot 'publish'
$zipName = "$pluginCode-$Version.zip"
$zipPath = Join-Path $releaseRoot $zipName
$releaseManifestPath = Join-Path $releaseRoot 'plugin-release-manifest.json'
$storagePath = "plugins/$pluginCode/$Channel/$Version/$zipName"

Write-Step "Prepare plugin release directory: $releaseRoot"
Reset-Directory -Path $releaseRoot
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

if (-not $SkipRestore) {
    Write-Step "Restore plugin dependencies: $PluginId.Plugin"
    dotnet restore $pluginProjectPath -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for plugin: $PluginId.Plugin"
    }
}

Write-Step "Clean plugin output directory: $PluginId.Plugin"
Reset-Directory -Path $pluginOutputPath

Write-Step "Build plugin: $PluginId.Plugin"
dotnet build $pluginProjectPath -c $Configuration --no-restore -m:1 -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed for plugin: $PluginId.Plugin"
}

if (-not (Test-Path -LiteralPath $pluginOutputPath)) {
    throw "Plugin output directory not found: $pluginOutputPath"
}

Write-Step "Copy plugin output into publish directory"
Copy-DirectoryContent -Source $pluginOutputPath -Destination $publishRoot
Write-PluginJsonVersion -Path (Join-Path $publishRoot 'plugin.json') -ReleaseVersion $Version

Write-Step "Refresh repo plugin staging directory"
Reset-Directory -Path $repoPluginPath
Copy-DirectoryContent -Source $publishRoot -Destination $repoPluginPath
Write-Info ("Plugin synced: {0} -> {1}" -f $PluginId, $repoPluginPath)

Write-Step "Verify plugin package files"
foreach ($requiredFile in (Get-RequiredPluginFiles -ResolvedPluginId $PluginId)) {
    Assert-RequiredFile -Root $publishRoot -RelativePath $requiredFile
}

if (-not $SkipZip) {
    Write-Step "Create plugin zip package: $zipName"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
}

$sha256 = $null
$sizeBytes = 0
if (-not $SkipZip) {
    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "Plugin zip package not found: $zipPath"
    }

    $sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $sizeBytes = (Get-Item -LiteralPath $zipPath).Length
}

Write-Step 'Write plugin release manifest'
$releaseManifest = [PSCustomObject]@{
    pluginId = $PluginId
    pluginCode = $pluginCode
    version = $Version
    channel = $Channel
    configuration = $Configuration
    generatedAt = (Get-Date).ToString('s')
    zipFileName = if ($SkipZip) { $null } else { $zipName }
    storagePath = $storagePath
    sha256 = $sha256
    sizeBytes = $sizeBytes
}

Write-Utf8Text -Path $releaseManifestPath -Content ($releaseManifest | ConvertTo-Json -Depth 100)

Write-Step 'Plugin release publish completed'
Write-Host ("Publish directory: {0}" -f $publishRoot) -ForegroundColor Green
if (-not $SkipZip) {
    Write-Host ("Zip package: {0}" -f $zipPath) -ForegroundColor Green
    Write-Host ("SHA256: {0}" -f $sha256) -ForegroundColor Green
    Write-Host ("Size bytes: {0}" -f $sizeBytes) -ForegroundColor Green
}
Write-Host ("Release manifest: {0}" -f $releaseManifestPath) -ForegroundColor Green
