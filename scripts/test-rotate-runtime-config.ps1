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

function New-TestConfigJson {
    param(
        [string]$BaseUrl,
        [string]$AnonKey,
        [bool]$Enabled = $true
    )

    return @{
        Application = @{
            Name = 'DIB Client'
            Version = '1.0.0'
        }
        Supabase = @{
            Url = 'http://localhost:54321'
            AnonKey = ''
            Schema = 'dib'
        }
        ReleaseCenter = @{
            Enabled = $Enabled
            BaseUrl = $BaseUrl
            Channel = 'stable'
            AnonKey = $AnonKey
        }
    } | ConvertTo-Json -Depth 20
}

function Assert-Equal {
    param(
        [object]$Expected,
        [object]$Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
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
$scriptPath = Join-Path $projectRoot 'scripts\rotate-runtime-config.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dib-config-rotation-tests-$([Guid]::NewGuid().ToString('N'))"

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    $targetPath = Join-Path $testRoot 'appsettings.json'
    $sourcePath = Join-Path $testRoot 'appsettings.new.json'
    $backupDirectory = Join-Path $testRoot 'backups'

    Write-Utf8Text -Path $targetPath -Content (New-TestConfigJson -BaseUrl 'https://old.example.test' -AnonKey 'old-key')
    Write-Utf8Text -Path $sourcePath -Content (New-TestConfigJson -BaseUrl 'https://new.example.test' -AnonKey 'new-key')

    & $scriptPath -SourcePath $sourcePath -TargetPath $targetPath -BackupDirectory $backupDirectory | Out-Null

    $rotated = Get-Content -LiteralPath $targetPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal 'https://new.example.test' ([string]$rotated.ReleaseCenter.BaseUrl) 'valid config should replace target config.'
    Assert-Equal 'new-key' ([string]$rotated.ReleaseCenter.AnonKey) 'valid config should write new key.'

    $backups = @(Get-ChildItem -LiteralPath $backupDirectory -Filter 'appsettings.json.bak.*')
    Assert-Equal 1 $backups.Count 'rotation should create one backup.'

    $backup = Get-Content -LiteralPath $backups[0].FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal 'https://old.example.test' ([string]$backup.ReleaseCenter.BaseUrl) 'backup should keep old config.'

    $invalidSourcePath = Join-Path $testRoot 'appsettings.invalid.json'
    Write-Utf8Text -Path $invalidSourcePath -Content (New-TestConfigJson -BaseUrl '' -AnonKey 'bad-key')

    $failed = $false
    try {
        & $scriptPath -SourcePath $invalidSourcePath -TargetPath $targetPath -BackupDirectory $backupDirectory | Out-Null
    }
    catch {
        $failed = $true
    }

    Assert-True $failed 'invalid config should fail rotation.'

    $unchanged = Get-Content -LiteralPath $targetPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal 'https://new.example.test' ([string]$unchanged.ReleaseCenter.BaseUrl) 'target config should stay unchanged after invalid source.'

    Write-Host 'rotate-runtime-config tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
