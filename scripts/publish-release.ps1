[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$Version,
    [string]$Channel = 'stable',
    [switch]$SkipRestore,
    [switch]$SkipZip,
    [switch]$SkipPluginBuild,
    [switch]$SkipPlugins,
    [string]$ReleaseCenterAnonKey
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

function Get-DotEnvValue {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return ''
    }

    $line = Get-Content -LiteralPath $Path -Encoding UTF8 |
        Where-Object { $_ -match "^\s*$([regex]::Escape($Name))\s*=" } |
        Select-Object -Last 1

    if ([string]::IsNullOrWhiteSpace($line)) {
        return ''
    }

    $value = ($line -split '=', 2)[1].Trim()
    if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
        return $value.Substring(1, $value.Length - 2)
    }

    return $value
}

function Resolve-ReleaseCenterAnonKey {
    param(
        [string]$ProjectRoot,
        [string]$ExplicitValue
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitValue)) {
        return $ExplicitValue
    }

    $candidateNames = @(
        'RELEASE_CENTER_ANON_KEY',
        'VITE_SUPABASE_ANON_KEY',
        'SUPABASE_ANON_KEY'
    )

    foreach ($name in $candidateNames) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    $envLocalPath = Join-Path $ProjectRoot 'dib-release-center\.env.local'
    foreach ($name in $candidateNames) {
        $value = Get-DotEnvValue -Path $envLocalPath -Name $name
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return ''
}

function Write-ReleaseAppSettings {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$ReleaseVersion,
        [string]$ReleaseChannel,
        [string]$ResolvedReleaseCenterAnonKey
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Default appsettings.json not found: $SourcePath"
    }

    $settings = Get-Content -LiteralPath $SourcePath -Raw -Encoding UTF8 | ConvertFrom-Json

    if ($null -eq $settings.Application) {
        throw 'Application section is missing from appsettings.json.'
    }

    if ($null -eq $settings.ReleaseCenter) {
        throw 'ReleaseCenter section is missing from appsettings.json.'
    }

    $settings.Application.Version = $ReleaseVersion
    $settings.ReleaseCenter.Enabled = $true
    $settings.ReleaseCenter.Channel = $ReleaseChannel
    if (-not [string]::IsNullOrWhiteSpace($ResolvedReleaseCenterAnonKey)) {
        $settings.ReleaseCenter.AnonKey = $ResolvedReleaseCenterAnonKey
    }

    if ([bool]$settings.ReleaseCenter.Enabled -and [string]::IsNullOrWhiteSpace([string]$settings.ReleaseCenter.AnonKey)) {
        throw 'ReleaseCenter.AnonKey is required for release packaging. Provide -ReleaseCenterAnonKey or set RELEASE_CENTER_ANON_KEY/VITE_SUPABASE_ANON_KEY/SUPABASE_ANON_KEY.'
    }

    $json = $settings | ConvertTo-Json -Depth 100
    Write-Utf8Text -Path $DestinationPath -Content $json
}

function Get-PluginProjects {
    param(
        [string]$Root,
        [string]$BuildConfiguration
    )

    $pluginSourceRoot = Join-Path $Root 'plugins-src'
    if (-not (Test-Path -LiteralPath $pluginSourceRoot)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $pluginSourceRoot -Directory |
        Where-Object { $_.Name -like '*.Plugin' } |
        ForEach-Object {
            $projectName = $_.Name
            $pluginId = $projectName -replace '\.Plugin$', ''
            $projectPath = Join-Path $_.FullName "$projectName.csproj"

            if (-not (Test-Path -LiteralPath $projectPath)) {
                throw "Plugin project file not found: $projectPath"
            }

            [PSCustomObject]@{
                PluginId = $pluginId
                ProjectName = $projectName
                ProjectPath = $projectPath
                OutputPath = Join-Path $_.FullName "bin\$BuildConfiguration\net10.0"
                RepoPluginPath = Join-Path $Root "plugins\$pluginId"
            }
        })
}

function Sync-PluginOutput {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    $settingsBackup = $null
    $settingsPath = Join-Path $DestinationPath 'plugin.settings.json'
    if (Test-Path -LiteralPath $settingsPath) {
        $settingsBackup = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8
    }

    Reset-Directory -Path $DestinationPath
    Copy-DirectoryContent -Source $SourcePath -Destination $DestinationPath

    if ($null -ne $settingsBackup) {
        Write-Utf8Text -Path $settingsPath -Content $settingsBackup
    }
}

$projectRoot = Resolve-ProjectRoot
Set-Location $projectRoot

$mainProjectPath = Join-Path $projectRoot 'digital-intelligence-bridge\digital-intelligence-bridge.csproj'
$defaultConfigPath = Join-Path $projectRoot 'digital-intelligence-bridge\appsettings.json'
$pluginsRoot = Join-Path $projectRoot 'plugins'
$artifactsRoot = Join-Path $projectRoot 'artifacts\releases'
$resolvedReleaseCenterAnonKey = Resolve-ReleaseCenterAnonKey -ProjectRoot $projectRoot -ExplicitValue $ReleaseCenterAnonKey

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format 'yyyyMMdd-HHmmss'
}

$releaseRoot = Join-Path $artifactsRoot $Version
$publishRoot = Join-Path $releaseRoot 'publish'
$zipName = "dib-$RuntimeIdentifier-portable-$Version.zip"
$zipPath = Join-Path $releaseRoot $zipName

Write-Step "Prepare release directory: $releaseRoot"
Reset-Directory -Path $releaseRoot
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

$pluginProjects = @()
if (-not $SkipPlugins) {
    $pluginProjects = Get-PluginProjects -Root $projectRoot -BuildConfiguration $Configuration
}

if (-not $SkipRestore) {
    Write-Step 'Restore main project dependencies'
    dotnet restore $mainProjectPath -r $RuntimeIdentifier -m:1
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore failed for main project.'
    }

    if ((-not $SkipPluginBuild) -and $pluginProjects.Count -gt 0) {
        foreach ($plugin in $pluginProjects) {
            Write-Step "Restore plugin dependencies: $($plugin.ProjectName)"
            dotnet restore $plugin.ProjectPath -m:1
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet restore failed for plugin: $($plugin.ProjectName)"
            }
        }
    }
}

if ((-not $SkipPluginBuild) -and (-not $SkipPlugins) -and $pluginProjects.Count -gt 0) {
    foreach ($plugin in $pluginProjects) {
        Write-Step "Clean plugin output directory: $($plugin.ProjectName)"
        Reset-Directory -Path $plugin.OutputPath

        Write-Step "Build plugin and refresh repo plugin folder: $($plugin.ProjectName)"
        dotnet build $plugin.ProjectPath -c $Configuration --no-restore -m:1 -v minimal
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for plugin: $($plugin.ProjectName)"
        }

        if (-not (Test-Path -LiteralPath $plugin.OutputPath)) {
            throw "Plugin output directory not found: $($plugin.OutputPath)"
        }

        Sync-PluginOutput -SourcePath $plugin.OutputPath -DestinationPath $plugin.RepoPluginPath
        Write-Info ("Plugin synced: {0} -> {1}" -f $plugin.PluginId, $plugin.RepoPluginPath)
    }
}

Write-Step 'Publish main application'
dotnet publish $mainProjectPath -c $Configuration -r $RuntimeIdentifier --self-contained false --no-restore -m:1 -o $publishRoot -v minimal -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed for main application.'
}

Write-Step 'Copy default appsettings.json'
Write-ReleaseAppSettings `
    -SourcePath $defaultConfigPath `
    -DestinationPath (Join-Path $publishRoot 'appsettings.json') `
    -ReleaseVersion $Version `
    -ReleaseChannel $Channel `
    -ResolvedReleaseCenterAnonKey $resolvedReleaseCenterAnonKey

if (-not $SkipPlugins) {
    if (-not (Test-Path -LiteralPath $pluginsRoot)) {
        throw "Repo plugins directory not found: $pluginsRoot"
    }

    Write-Step ("Copy repo plugins into publish directory (Channel={0})" -f $Channel)
    Copy-DirectoryContent -Source $pluginsRoot -Destination (Join-Path $publishRoot 'plugins')
}

Write-Step 'Write release manifest'
$manifest = [PSCustomObject]@{
    version = $Version
    channel = $Channel
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    generatedAt = (Get-Date).ToString('s')
    pluginsIncluded = (-not $SkipPlugins)
    pluginsBuilt = ((-not $SkipPluginBuild) -and (-not $SkipPlugins))
}
Write-Utf8Text -Path (Join-Path $releaseRoot 'release-manifest.json') -Content ($manifest | ConvertTo-Json)

if (-not $SkipZip) {
    Write-Step "Create zip package: $zipName"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
}

$mainExePath = Join-Path $publishRoot 'digital-intelligence-bridge.exe'
if (-not (Test-Path -LiteralPath $mainExePath)) {
    throw "Published executable not found: $mainExePath"
}

if ((-not $SkipPlugins) -and (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'plugins')))) {
    throw 'Published output is missing the plugins directory.'
}

if ((-not $SkipZip) -and (-not (Test-Path -LiteralPath $zipPath))) {
    throw "Zip package not found: $zipPath"
}

Write-Step 'Release publish completed'
Write-Host ("Publish directory: {0}" -f $publishRoot) -ForegroundColor Green
if (-not $SkipZip) {
    Write-Host ("Zip package: {0}" -f $zipPath) -ForegroundColor Green
}
