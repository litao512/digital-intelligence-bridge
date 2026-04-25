[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$RunTests,
    [switch]$ResetRuntimeConfig,
    [switch]$ResetPluginSettings,
    [switch]$CleanRepoArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "`n[STEP] $Message" -ForegroundColor Cyan
}

function Resolve-ProjectRoot {
    if ($PSScriptRoot) {
        return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    }

    return (Get-Location).Path
}

function Write-Utf8Text {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Get-ConfigRoot {
    $override = [Environment]::GetEnvironmentVariable('DIB_CONFIG_ROOT')
    if (-not [string]::IsNullOrWhiteSpace($override)) {
        return $override
    }

    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    return (Join-Path $localAppData 'DibClient')
}

function Reset-Directory {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

$projectRoot = Resolve-ProjectRoot
Set-Location $projectRoot

$configRoot = Get-ConfigRoot
$runtimePluginsRoot = Join-Path $configRoot 'plugins'
$releaseCacheRoot = Join-Path $configRoot 'release-cache'
$releaseStagingRoot = Join-Path $configRoot 'release-staging'
$releaseBackupsRoot = Join-Path $configRoot 'release-backups'
$resourceCacheRoot = Join-Path $configRoot 'resource-cache'
$logsRoot = Join-Path $configRoot 'logs'

$runtimeConfigPath = Join-Path $configRoot 'appsettings.json'
$defaultConfigPath = Join-Path $projectRoot 'digital-intelligence-bridge\appsettings.json'

$pluginProjects = @(
    @{
        Name = 'PatientRegistration'
        Project = 'plugins-src\PatientRegistration.Plugin\PatientRegistration.Plugin.csproj'
        Output = 'plugins-src\PatientRegistration.Plugin\bin'
    },
    @{
        Name = 'MedicalDrugImport'
        Project = 'plugins-src\MedicalDrugImport.Plugin\MedicalDrugImport.Plugin.csproj'
        Output = 'plugins-src\MedicalDrugImport.Plugin\bin'
    }
)

$pluginRuntimeConfigBackup = @{}
if (-not $ResetPluginSettings -and (Test-Path -LiteralPath $runtimePluginsRoot)) {
    foreach ($plugin in $pluginProjects) {
        $existingPluginDir = Join-Path $runtimePluginsRoot $plugin.Name
        $settingsPath = Join-Path $existingPluginDir 'plugin.settings.json'
        $devSettingsPath = Join-Path $existingPluginDir 'plugin.development.json'
        $pluginRuntimeConfigBackup[$plugin.Name] = @{
            Settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -LiteralPath $settingsPath -Raw } else { $null }
            DevelopmentSettings = if (Test-Path -LiteralPath $devSettingsPath) { Get-Content -LiteralPath $devSettingsPath -Raw } else { $null }
        }
    }
}

Write-Step "准备运行时目录：$configRoot"
New-Item -ItemType Directory -Path $configRoot -Force | Out-Null

Write-Step '清理运行时插件与发布缓存目录'
Reset-Directory -Path $runtimePluginsRoot
Reset-Directory -Path (Join-Path $releaseCacheRoot 'plugins')
Reset-Directory -Path (Join-Path $releaseStagingRoot 'plugins')
Reset-Directory -Path (Join-Path $releaseBackupsRoot 'plugins')
Reset-Directory -Path $resourceCacheRoot
Reset-Directory -Path $logsRoot

if ($ResetRuntimeConfig) {
    Write-Step '重置运行时 appsettings.json（先备份）'
    if (Test-Path -LiteralPath $runtimeConfigPath) {
        $backupPath = "$runtimeConfigPath.bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
        Copy-Item -LiteralPath $runtimeConfigPath -Destination $backupPath -Force
        Write-Host "已备份：$backupPath" -ForegroundColor Yellow
    }

    Copy-Item -LiteralPath $defaultConfigPath -Destination $runtimeConfigPath -Force
}
else {
    Write-Step '保留当前运行时 appsettings.json（未启用 -ResetRuntimeConfig）'
}

if ($CleanRepoArtifacts) {
    Write-Step '清理仓库内构建产物（bin/obj）'
    Get-ChildItem -Path $projectRoot -Directory -Recurse -Force |
        Where-Object { $_.Name -in @('bin', 'obj') } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }
}

Write-Step '还原依赖（串行）'
dotnet restore digital-intelligence-bridge.sln

foreach ($plugin in $pluginProjects) {
    $pluginOutputDir = Join-Path $projectRoot "plugins-src\$($plugin.Name).Plugin\bin\$Configuration\net10.0"

    Write-Step "清理插件输出目录：$pluginOutputDir"
    Reset-Directory -Path $pluginOutputDir

    Write-Step "构建插件（串行）：$($plugin.Project)"
    dotnet build $plugin.Project -c $Configuration --no-restore -m:1 -v minimal

    if (-not (Test-Path -LiteralPath $pluginOutputDir)) {
        throw "未找到插件输出目录：$pluginOutputDir"
    }

    $runtimePluginDir = Join-Path $runtimePluginsRoot $plugin.Name
    Reset-Directory -Path $runtimePluginDir

    Write-Step "同步插件到运行时目录：$runtimePluginDir"
    Copy-Item -Path (Join-Path $pluginOutputDir '*') -Destination $runtimePluginDir -Recurse -Force

    if (-not $ResetPluginSettings) {
        $backup = $pluginRuntimeConfigBackup[$plugin.Name]
        $runtimeSettingsPath = Join-Path $runtimePluginDir 'plugin.settings.json'
        if ($null -ne $backup -and $null -ne $backup.Settings) {
            Write-Utf8Text -Path $runtimeSettingsPath -Content $backup.Settings
        }

        $runtimeDevelopmentSettings = Join-Path $runtimePluginDir 'plugin.development.json'
        if ($null -ne $backup -and $null -ne $backup.DevelopmentSettings) {
            Write-Utf8Text -Path $runtimeDevelopmentSettings -Content $backup.DevelopmentSettings
        }
    }

    if ($ResetPluginSettings) {
        $sourceSettingsPath = Join-Path $projectRoot "plugins-src\$($plugin.Name).Plugin\plugin.settings.json"
        if (Test-Path -LiteralPath $sourceSettingsPath) {
            Copy-Item -LiteralPath $sourceSettingsPath -Destination (Join-Path $runtimePluginDir 'plugin.settings.json') -Force
        }
    }

    $developmentSettingsExample = Join-Path $projectRoot "plugins-src\$($plugin.Name).Plugin\plugin.development.json.example"
    $runtimeDevelopmentSettings = Join-Path $runtimePluginDir 'plugin.development.json'
    if ((-not (Test-Path -LiteralPath $runtimeDevelopmentSettings)) -and (Test-Path -LiteralPath $developmentSettingsExample)) {
        Copy-Item -LiteralPath $developmentSettingsExample -Destination $runtimeDevelopmentSettings -Force
    }
}

Write-Step '构建 DIB 客户端（串行）'
dotnet build digital-intelligence-bridge\digital-intelligence-bridge.csproj -c $Configuration --no-restore -m:1 -v minimal

if ($RunTests) {
    Write-Step '构建 UnitTests（串行）'
    dotnet build digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal

    Write-Step '执行 UnitTests（串行）'
    dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-build -m:1 -v minimal
}

Write-Step '完成：运行时环境已清理并重建'
Write-Host "运行时根目录：$configRoot" -ForegroundColor Green
Write-Host "插件目录：$runtimePluginsRoot" -ForegroundColor Green
if ($ResetRuntimeConfig) {
    Write-Host "运行时配置已重置：$runtimeConfigPath" -ForegroundColor Green
}
