[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(Mandatory = $true)]
    [string]$FromVersion,

    [Parameter(Mandatory = $true)]
    [string]$ToVersion,

    [Parameter(ParameterSetName = 'Prepare')]
    [switch]$Prepare,

    [Parameter(ParameterSetName = 'Collect')]
    [switch]$Collect,

    [string]$SandboxRoot,

    [switch]$NoLaunch,

    [int]$LogTail = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-ProjectRoot {
    if ($PSScriptRoot) {
        return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    }

    return (Get-Location).Path
}

function Resolve-SandboxRoot {
    param(
        [string]$ProjectRoot,
        [string]$RequestedSandboxRoot
    )

    if ([string]::IsNullOrWhiteSpace($RequestedSandboxRoot)) {
        return (Join-Path $ProjectRoot '.tmp\client-upgrade-e2e')
    }

    return [System.IO.Path]::GetFullPath($RequestedSandboxRoot)
}

function Assert-SandboxPath {
    param(
        [string]$ProjectRoot,
        [string]$ResolvedSandboxRoot
    )

    $defaultTmpRoot = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot '.tmp'))
    $fullSandboxRoot = [System.IO.Path]::GetFullPath($ResolvedSandboxRoot)

    if (-not $fullSandboxRoot.StartsWith($defaultTmpRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $leaf = Split-Path -Leaf $fullSandboxRoot
        if ($leaf -notmatch 'upgrade|e2e|sandbox|test') {
            throw "SandboxRoot 看起来不像测试目录，拒绝清理：$fullSandboxRoot"
        }
    }
}

function Reset-Directory {
    param(
        [string]$Path,
        [string]$ProjectRoot
    )

    Assert-SandboxPath -ProjectRoot $ProjectRoot -ResolvedSandboxRoot $Path

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Read-AppVersion {
    param([string]$AppSettingsPath)

    if (-not (Test-Path -LiteralPath $AppSettingsPath)) {
        return $null
    }

    $settings = Get-Content -LiteralPath $AppSettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    return [string]$settings.Application.Version
}

function Get-UpdaterLogPath {
    return Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'DibClient\logs\client-updater.log'
}

function Get-LogTimestamp {
    param([string]$Line)

    if ($Line -match '^\[(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]') {
        return [DateTime]::ParseExact($Matches.timestamp, 'yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
    }

    return $null
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n[STEP] $Message" -ForegroundColor Cyan
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor DarkGray
}

$projectRoot = Resolve-ProjectRoot
$sandboxRoot = Resolve-SandboxRoot -ProjectRoot $projectRoot -RequestedSandboxRoot $SandboxRoot
$currentRoot = Join-Path $sandboxRoot 'current'
$downloadsRoot = Join-Path $sandboxRoot 'downloads'
$evidenceRoot = Join-Path $sandboxRoot 'evidence'
$summaryPath = Join-Path $evidenceRoot 'summary.json'

if (-not $Prepare -and -not $Collect) {
    $Prepare = $true
}

if ($Prepare) {
    Write-Step "准备客户端升级沙盒：$sandboxRoot"
    Reset-Directory -Path $sandboxRoot -ProjectRoot $projectRoot
    New-Item -ItemType Directory -Path $currentRoot, $downloadsRoot, $evidenceRoot -Force | Out-Null

    $zipName = "dib-win-x64-portable-$FromVersion.zip"
    $zipPath = Join-Path $projectRoot "artifacts\releases\$FromVersion\$zipName"
    if (-not (Test-Path -LiteralPath $zipPath)) {
        throw "未找到旧版客户端包：$zipPath"
    }

    Write-Step "解压旧版客户端包：$zipPath"
    Expand-Archive -LiteralPath $zipPath -DestinationPath $currentRoot -Force

    $appSettingsPath = Join-Path $currentRoot 'appsettings.json'
    $exePath = Join-Path $currentRoot 'digital-intelligence-bridge.exe'
    $currentVersion = Read-AppVersion -AppSettingsPath $appSettingsPath

    if ($currentVersion -ne $FromVersion) {
        throw "旧版包版本不匹配。期望=$FromVersion 实际=$currentVersion"
    }

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "旧版包缺少主程序：$exePath"
    }

    $prepareSummary = [PSCustomObject]@{
        mode = 'prepare'
        fromVersion = $FromVersion
        toVersion = $ToVersion
        sandboxRoot = $sandboxRoot
        currentRoot = $currentRoot
        executablePath = $exePath
        preparedAt = (Get-Date).ToString('s')
    }
    $prepareSummary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

    Write-Info "旧版客户端目录：$currentRoot"
    Write-Info "目标升级版本：$ToVersion"

    if ($NoLaunch) {
        Write-Host '已准备沙盒。请手动启动 current 目录中的客户端，或去掉 -NoLaunch 让脚本启动。' -ForegroundColor Green
        return
    }

    Write-Step '启动旧版客户端'
    Start-Process -FilePath $exePath -WorkingDirectory $currentRoot
    Write-Host '客户端已启动。请在 UI 中执行：检查更新 -> 下载客户端更新包 -> 立即升级。升级后运行本脚本 -Collect。' -ForegroundColor Green
    return
}

if ($Collect) {
    Write-Step "收集客户端升级验收证据：$sandboxRoot"
    New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null

    $appSettingsPath = Join-Path $currentRoot 'appsettings.json'
    $actualVersion = Read-AppVersion -AppSettingsPath $appSettingsPath
    $prepareStartedAt = $null
    if (Test-Path -LiteralPath $summaryPath) {
        $previousSummary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $preparedAtProperty = $previousSummary.PSObject.Properties['preparedAt']
        if ($null -ne $preparedAtProperty -and $preparedAtProperty.Value) {
            $prepareStartedAt = [DateTime]::Parse([string]$preparedAtProperty.Value)
        }
    }

    $logPath = Get-UpdaterLogPath
    $logEvidencePath = Join-Path $evidenceRoot 'client-updater-tail.log'
    $logTailLines = @()

    if (Test-Path -LiteralPath $logPath) {
        $allTailLines = @(Get-Content -LiteralPath $logPath -Tail $LogTail -Encoding UTF8)
        if ($null -ne $prepareStartedAt) {
            $logTailLines = @($allTailLines | Where-Object {
                    $timestamp = Get-LogTimestamp -Line $_
                    ($null -eq $timestamp) -or ($timestamp -ge $prepareStartedAt.AddSeconds(-2))
                })
        }
        else {
            $logTailLines = $allTailLines
        }

        $logTailLines | Set-Content -LiteralPath $logEvidencePath -Encoding UTF8
    }

    $mainExeExists = Test-Path -LiteralPath (Join-Path $currentRoot 'digital-intelligence-bridge.exe')
    $updaterExists = Test-Path -LiteralPath (Join-Path $currentRoot 'DibClient.Updater.exe')
    $pluginsExist = Test-Path -LiteralPath (Join-Path $currentRoot 'plugins')
    $logHasFailure = $logTailLines | Where-Object { $_ -like '*升级失败*' } | Select-Object -First 1
    $passed = ($actualVersion -eq $ToVersion) -and $mainExeExists -and $updaterExists -and $pluginsExist -and (-not $logHasFailure)

    $collectSummary = [PSCustomObject]@{
        mode = 'collect'
        fromVersion = $FromVersion
        toVersion = $ToVersion
        actualVersion = $actualVersion
        passed = $passed
        sandboxRoot = $sandboxRoot
        currentRoot = $currentRoot
        mainExeExists = $mainExeExists
        updaterExists = $updaterExists
        pluginsExist = $pluginsExist
        updaterLogPath = $logPath
        updaterLogTailPath = $logEvidencePath
        logHasFailure = [bool]$logHasFailure
        collectedAt = (Get-Date).ToString('s')
    }

    $collectSummary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

    Write-Host ($collectSummary | ConvertTo-Json -Depth 20)
    if (-not $passed) {
        throw "客户端升级验收未通过。摘要：$summaryPath 日志片段：$logEvidencePath"
    }

    Write-Host '客户端升级验收通过。' -ForegroundColor Green
}
