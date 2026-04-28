[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClientVersion,

    [string]$PluginCode = 'patient-registration',

    [string]$FromPluginVersion,

    [Parameter(Mandatory = $true)]
    [string]$ToPluginVersion,

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
        return (Join-Path $ProjectRoot '.tmp\plugin-upgrade-e2e')
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
        if ($leaf -notmatch 'plugin|upgrade|e2e|sandbox|test') {
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
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Read-PluginVersion {
    param([string]$PluginJsonPath)

    $pluginJson = Read-JsonFile -Path $PluginJsonPath
    if ($null -eq $pluginJson) {
        return $null
    }

    return [string]$pluginJson.version
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n[STEP] $Message" -ForegroundColor Cyan
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor DarkGray
}

function Get-DirectoryFileCount {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0
    }

    return @(Get-ChildItem -LiteralPath $Path -Recurse -File).Count
}

$projectRoot = Resolve-ProjectRoot
$sandboxRoot = Resolve-SandboxRoot -ProjectRoot $projectRoot -RequestedSandboxRoot $SandboxRoot
$currentRoot = Join-Path $sandboxRoot 'current'
$configRoot = Join-Path $sandboxRoot 'config-root'
$evidenceRoot = Join-Path $sandboxRoot 'evidence'
$summaryPath = Join-Path $evidenceRoot 'summary.json'
$runtimePluginRoot = Join-Path $configRoot 'plugins'
$runtimePluginDirectory = Join-Path $runtimePluginRoot $PluginCode
$runtimePluginJsonPath = Join-Path $runtimePluginDirectory 'plugin.json'
$cacheRoot = Join-Path $configRoot 'release-cache\plugins\stable'
$stagingRoot = Join-Path $configRoot 'release-staging\plugins\stable'
$backupRoot = Join-Path $configRoot 'release-backups\plugins'
$logsRoot = Join-Path $configRoot 'logs'

if (-not $Prepare -and -not $Collect) {
    $Prepare = $true
}

if ($Prepare) {
    Write-Step "准备插件升级沙盒：$sandboxRoot"
    Reset-Directory -Path $sandboxRoot -ProjectRoot $projectRoot
    New-Item -ItemType Directory -Path $currentRoot, $configRoot, $evidenceRoot, $runtimePluginRoot -Force | Out-Null

    $clientZipName = "dib-win-x64-portable-$ClientVersion.zip"
    $clientZipPath = Join-Path $projectRoot "artifacts\releases\$ClientVersion\$clientZipName"
    if (-not (Test-Path -LiteralPath $clientZipPath)) {
        throw "未找到客户端包：$clientZipPath"
    }

    Write-Step "解压客户端包：$clientZipPath"
    Expand-Archive -LiteralPath $clientZipPath -DestinationPath $currentRoot -Force

    $exePath = Join-Path $currentRoot 'digital-intelligence-bridge.exe'
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "客户端包缺少主程序：$exePath"
    }

    if (-not [string]::IsNullOrWhiteSpace($FromPluginVersion)) {
        $oldPluginPublishRoot = Join-Path $projectRoot "artifacts\plugin-releases\$PluginCode\$FromPluginVersion\publish"
        if (-not (Test-Path -LiteralPath $oldPluginPublishRoot)) {
            throw "未找到旧插件发布目录：$oldPluginPublishRoot"
        }

        Write-Step "植入旧插件版本：$PluginCode $FromPluginVersion"
        Copy-DirectoryContent -Source $oldPluginPublishRoot -Destination $runtimePluginDirectory
        $actualOldPluginVersion = Read-PluginVersion -PluginJsonPath $runtimePluginJsonPath
        if ($actualOldPluginVersion -ne $FromPluginVersion) {
            throw "旧插件版本不匹配。期望=$FromPluginVersion 实际=$actualOldPluginVersion"
        }
    }

    $prepareSummary = [PSCustomObject]@{
        mode = 'prepare'
        clientVersion = $ClientVersion
        pluginCode = $PluginCode
        fromPluginVersion = $FromPluginVersion
        toPluginVersion = $ToPluginVersion
        sandboxRoot = $sandboxRoot
        currentRoot = $currentRoot
        configRoot = $configRoot
        runtimePluginDirectory = $runtimePluginDirectory
        executablePath = $exePath
        preparedAt = (Get-Date).ToString('s')
    }
    $prepareSummary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

    Write-Info "客户端目录：$currentRoot"
    Write-Info "配置根目录：$configRoot"
    Write-Info "目标插件版本：$PluginCode $ToPluginVersion"

    if ($NoLaunch) {
        Write-Host '已准备插件升级沙盒。请手动启动 current 目录中的客户端，或去掉 -NoLaunch 让脚本启动。' -ForegroundColor Green
        return
    }

    Write-Step '启动沙盒客户端'
    Start-Process -FilePath $exePath -WorkingDirectory $currentRoot -Environment @{ DIB_CONFIG_ROOT = $configRoot }
    Write-Host '客户端已启动。请在 UI 中执行插件更新流程，完成后运行本脚本 -Collect。' -ForegroundColor Green
    return
}

if ($Collect) {
    Write-Step "收集插件升级验收证据：$sandboxRoot"
    New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null

    $actualPluginVersion = Read-PluginVersion -PluginJsonPath $runtimePluginJsonPath
    $cacheZipFiles = @(if (Test-Path -LiteralPath $cacheRoot) { Get-ChildItem -LiteralPath $cacheRoot -Filter '*.zip' -File } else { @() })
    $stagedPluginJsonFiles = @(if (Test-Path -LiteralPath $stagingRoot) { Get-ChildItem -LiteralPath $stagingRoot -Filter 'plugin.json' -Recurse -File } else { @() })
    $backupPluginDirectories = @(if (Test-Path -LiteralPath $backupRoot) { Get-ChildItem -LiteralPath $backupRoot -Directory -Recurse | Where-Object { $_.Name -eq $PluginCode } } else { @() })
    $logFiles = @(if (Test-Path -LiteralPath $logsRoot) { Get-ChildItem -LiteralPath $logsRoot -Filter '*.log' -Recurse -File } else { @() })

    $logEvidencePath = Join-Path $evidenceRoot 'logs-tail.txt'
    $logTailLines = @()
    foreach ($logFile in $logFiles) {
        $logTailLines += "===== $($logFile.FullName) ====="
        $logTailLines += @(Get-Content -LiteralPath $logFile.FullName -Tail $LogTail -Encoding UTF8)
    }
    $logTailLines | Set-Content -LiteralPath $logEvidencePath -Encoding UTF8

    $runtimePluginExists = Test-Path -LiteralPath $runtimePluginDirectory
    $runtimePluginJsonExists = Test-Path -LiteralPath $runtimePluginJsonPath
    $pluginDllExists = @(if ($runtimePluginExists) { Get-ChildItem -LiteralPath $runtimePluginDirectory -Filter '*.Plugin.dll' -File } else { @() }).Count -gt 0
    $runtimeFileCount = Get-DirectoryFileCount -Path $runtimePluginDirectory
    $logHasFailure = $logTailLines | Where-Object {
        $_ -match '失败|Exception|error|Error|依赖缺失|加载失败|初始化失败'
    } | Select-Object -First 1

    $passed = ($actualPluginVersion -eq $ToPluginVersion) -and $runtimePluginJsonExists -and $pluginDllExists -and (-not $logHasFailure)

    $collectSummary = [PSCustomObject]@{
        mode = 'collect'
        clientVersion = $ClientVersion
        pluginCode = $PluginCode
        fromPluginVersion = $FromPluginVersion
        toPluginVersion = $ToPluginVersion
        actualPluginVersion = $actualPluginVersion
        passed = $passed
        sandboxRoot = $sandboxRoot
        configRoot = $configRoot
        runtimePluginDirectory = $runtimePluginDirectory
        runtimePluginExists = $runtimePluginExists
        runtimePluginJsonExists = $runtimePluginJsonExists
        pluginDllExists = $pluginDllExists
        runtimeFileCount = $runtimeFileCount
        cacheZipCount = $cacheZipFiles.Count
        stagedPluginJsonCount = $stagedPluginJsonFiles.Count
        backupPluginDirectoryCount = $backupPluginDirectories.Count
        logFileCount = $logFiles.Count
        logHasFailure = [bool]$logHasFailure
        logsTailPath = $logEvidencePath
        collectedAt = (Get-Date).ToString('s')
    }

    $collectSummary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    Write-Host ($collectSummary | ConvertTo-Json -Depth 20)

    if (-not $passed) {
        throw "插件升级验收未通过。摘要：$summaryPath 日志片段：$logEvidencePath"
    }

    Write-Host '插件升级验收通过。' -ForegroundColor Green
}
