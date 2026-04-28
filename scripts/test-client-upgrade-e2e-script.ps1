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
        ReleaseCenter = @{
            Enabled = $true
            BaseUrl = 'http://101.42.19.26:8000'
            Channel = 'stable'
        }
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
$scriptPath = Join-Path $projectRoot 'scripts\test-client-upgrade-e2e.ps1'
$fromVersion = '0.0.1-e2e-script-test'
$toVersion = '0.0.2-e2e-script-test'
$releaseRoot = Join-Path $projectRoot "artifacts\releases\$fromVersion"
$packageSourceRoot = Join-Path $releaseRoot 'fake-source'
$zipPath = Join-Path $releaseRoot "dib-win-x64-portable-$fromVersion.zip"
$sandboxRoot = Join-Path $projectRoot '.tmp\client-upgrade-e2e-script-test'

try {
    if (Test-Path -LiteralPath $releaseRoot) {
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }

    if (Test-Path -LiteralPath $sandboxRoot) {
        Remove-Item -LiteralPath $sandboxRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageSourceRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $packageSourceRoot 'plugins') -Force | Out-Null
    Write-Utf8Text -Path (Join-Path $packageSourceRoot 'digital-intelligence-bridge.exe') -Content 'fake exe'
    Write-Utf8Text -Path (Join-Path $packageSourceRoot 'DibClient.Updater.exe') -Content 'fake updater'
    Write-Utf8Text -Path (Join-Path $packageSourceRoot 'appsettings.json') -Content (New-AppSettingsJson -Version $fromVersion)
    Write-Utf8Text -Path (Join-Path $packageSourceRoot 'plugins\.keep') -Content 'keep'
    Compress-Archive -Path (Join-Path $packageSourceRoot '*') -DestinationPath $zipPath -Force

    & $scriptPath -FromVersion $fromVersion -ToVersion $toVersion -Prepare -NoLaunch -SandboxRoot $sandboxRoot | Out-Null
    Assert-True (Test-Path -LiteralPath (Join-Path $sandboxRoot 'current\appsettings.json')) 'Prepare should extract appsettings.json.'
    Assert-True (Test-Path -LiteralPath (Join-Path $sandboxRoot 'current\digital-intelligence-bridge.exe')) 'Prepare should extract main executable.'

    Write-Utf8Text -Path (Join-Path $sandboxRoot 'current\appsettings.json') -Content (New-AppSettingsJson -Version $toVersion)

    & $scriptPath -FromVersion $fromVersion -ToVersion $toVersion -Collect -SandboxRoot $sandboxRoot | Out-Null
    $summaryPath = Join-Path $sandboxRoot 'evidence\summary.json'
    $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-True ([bool]$summary.passed) 'Collect should pass after version is updated.'

    Write-Host 'client upgrade e2e script tests passed.' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $sandboxRoot) {
        Remove-Item -LiteralPath $sandboxRoot -Recurse -Force
    }

    if (Test-Path -LiteralPath $releaseRoot) {
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }
}
