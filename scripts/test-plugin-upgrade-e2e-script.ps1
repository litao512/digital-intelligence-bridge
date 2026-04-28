[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

function New-AppSettingsJson {
    param([string]$Version)

    return @{
        Application = @{
            Name = 'DIB客户端'
            Version = $Version
        }
        Plugin = @{
            PluginDirectory = 'plugins'
            AutoLoad = $true
        }
        ReleaseCenter = @{
            Enabled = $true
            BaseUrl = 'http://101.42.19.26:8000'
            Channel = 'stable'
        }
    } | ConvertTo-Json -Depth 20
}

function New-PluginJson {
    param(
        [string]$PluginCode,
        [string]$Version
    )

    return @{
        id = $PluginCode
        name = '就诊登记'
        version = $Version
        entryAssembly = 'PatientRegistration.Plugin.dll'
        entryType = 'PatientRegistration.Plugin.PatientRegistrationPlugin, PatientRegistration.Plugin'
    } | ConvertTo-Json -Depth 20
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$projectRoot = Resolve-ProjectRoot
$scriptPath = Join-Path $projectRoot 'scripts\test-plugin-upgrade-e2e.ps1'
$clientVersion = '0.0.1-plugin-e2e-client-test'
$pluginCode = 'patient-registration'
$fromPluginVersion = '0.0.1-plugin-e2e-test'
$toPluginVersion = '0.0.2-plugin-e2e-test'
$clientReleaseRoot = Join-Path $projectRoot "artifacts\releases\$clientVersion"
$clientSourceRoot = Join-Path $clientReleaseRoot 'fake-source'
$clientZipPath = Join-Path $clientReleaseRoot "dib-win-x64-portable-$clientVersion.zip"
$pluginPublishRoot = Join-Path $projectRoot "artifacts\plugin-releases\$pluginCode\$fromPluginVersion\publish"
$sandboxRoot = Join-Path $projectRoot '.tmp\plugin-upgrade-e2e-script-test'

try {
    foreach ($path in @($clientReleaseRoot, (Join-Path $projectRoot "artifacts\plugin-releases\$pluginCode\$fromPluginVersion"), $sandboxRoot)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Path $clientSourceRoot -Force | Out-Null
    Write-Utf8Text -Path (Join-Path $clientSourceRoot 'digital-intelligence-bridge.exe') -Content 'fake client exe'
    Write-Utf8Text -Path (Join-Path $clientSourceRoot 'appsettings.json') -Content (New-AppSettingsJson -Version $clientVersion)
    Compress-Archive -Path (Join-Path $clientSourceRoot '*') -DestinationPath $clientZipPath -Force

    New-Item -ItemType Directory -Path $pluginPublishRoot -Force | Out-Null
    Write-Utf8Text -Path (Join-Path $pluginPublishRoot 'plugin.json') -Content (New-PluginJson -PluginCode $pluginCode -Version $fromPluginVersion)
    Write-Utf8Text -Path (Join-Path $pluginPublishRoot 'PatientRegistration.Plugin.dll') -Content 'old plugin dll'

    & $scriptPath `
        -ClientVersion $clientVersion `
        -PluginCode $pluginCode `
        -FromPluginVersion $fromPluginVersion `
        -ToPluginVersion $toPluginVersion `
        -Prepare `
        -NoLaunch `
        -SandboxRoot $sandboxRoot | Out-Null

    $runtimePluginRoot = Join-Path $sandboxRoot "config-root\plugins\$pluginCode"
    Assert-True (Test-Path -LiteralPath (Join-Path $runtimePluginRoot 'plugin.json')) 'Prepare should seed old plugin.json.'
    Assert-True (Test-Path -LiteralPath (Join-Path $runtimePluginRoot 'PatientRegistration.Plugin.dll')) 'Prepare should seed plugin dll.'

    Write-Utf8Text -Path (Join-Path $runtimePluginRoot 'plugin.json') -Content (New-PluginJson -PluginCode $pluginCode -Version $toPluginVersion)
    Write-Utf8Text -Path (Join-Path $runtimePluginRoot 'PatientRegistration.Plugin.dll') -Content 'new plugin dll'

    & $scriptPath `
        -ClientVersion $clientVersion `
        -PluginCode $pluginCode `
        -FromPluginVersion $fromPluginVersion `
        -ToPluginVersion $toPluginVersion `
        -Collect `
        -SandboxRoot $sandboxRoot | Out-Null

    $summaryPath = Join-Path $sandboxRoot 'evidence\summary.json'
    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-True ([bool]$summary.passed) 'Collect should pass after plugin version is updated.'

    Write-Host 'plugin upgrade e2e script tests passed.' -ForegroundColor Green
}
finally {
    foreach ($path in @($sandboxRoot, $clientReleaseRoot, (Join-Path $projectRoot "artifacts\plugin-releases\$pluginCode\$fromPluginVersion"))) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}
