[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$TargetPath,

    [string]$BackupDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Utf8Text {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Read-JsonConfig {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "config file not found: $Path"
    }

    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    try {
        $json = $raw | ConvertFrom-Json
    }
    catch {
        throw "config is not valid JSON: $Path"
    }

    return @{
        Raw = $raw
        Json = $json
    }
}

function Assert-HttpUrl {
    param(
        [string]$Value,
        [string]$Name
    )

    try {
        $uri = [Uri]$Value
        if ($uri.Scheme -ne 'http' -and $uri.Scheme -ne 'https') {
            throw "invalid scheme: $($uri.Scheme)"
        }
    }
    catch {
        throw "$Name is not a valid http/https URL: $Value"
    }
}

function Test-PropertyExists {
    param(
        [object]$Object,
        [string]$Name
    )

    return $null -ne $Object.PSObject.Properties[$Name]
}

function Assert-RuntimeConfig {
    param(
        [object]$Json,
        [string]$Path
    )

    if (-not (Test-PropertyExists -Object $Json -Name 'ReleaseCenter') -or $null -eq $Json.ReleaseCenter) {
        throw "missing required section: ReleaseCenter ($Path)"
    }

    $releaseCenter = $Json.ReleaseCenter
    $enabled = $false
    if (Test-PropertyExists -Object $releaseCenter -Name 'Enabled') {
        $enabled = [bool]$releaseCenter.Enabled
    }

    if ($enabled) {
        $baseUrl = [string]$releaseCenter.BaseUrl
        $channel = [string]$releaseCenter.Channel
        $anonKey = [string]$releaseCenter.AnonKey

        if ([string]::IsNullOrWhiteSpace($baseUrl)) {
            throw "ReleaseCenter.BaseUrl is required when ReleaseCenter.Enabled is true ($Path)"
        }

        if ([string]::IsNullOrWhiteSpace($channel)) {
            throw "ReleaseCenter.Channel is required when ReleaseCenter.Enabled is true ($Path)"
        }

        if ([string]::IsNullOrWhiteSpace($anonKey)) {
            throw "ReleaseCenter.AnonKey is required when ReleaseCenter.Enabled is true ($Path)"
        }

        Assert-HttpUrl -Value $baseUrl -Name 'ReleaseCenter.BaseUrl'
    }

    if ((Test-PropertyExists -Object $Json -Name 'Supabase') -and $null -ne $Json.Supabase) {
        $supabase = $Json.Supabase
        $url = [string]$supabase.Url
        $schema = [string]$supabase.Schema

        if (-not [string]::IsNullOrWhiteSpace($url)) {
            Assert-HttpUrl -Value $url -Name 'Supabase.Url'
        }

        if (-not [string]::IsNullOrWhiteSpace($schema) -and $schema.Contains(' ')) {
            throw "Supabase.Schema must not contain spaces ($Path)"
        }
    }
}

function Get-ResolvedPathOrFullPath {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    return [System.IO.Path]::GetFullPath($Path)
}

$sourceFullPath = Get-ResolvedPathOrFullPath -Path $SourcePath
$targetFullPath = Get-ResolvedPathOrFullPath -Path $TargetPath
$targetDirectory = Split-Path -Parent $targetFullPath
if ([string]::IsNullOrWhiteSpace($targetDirectory)) {
    $targetDirectory = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    $BackupDirectory = $targetDirectory
}

$backupFullDirectory = Get-ResolvedPathOrFullPath -Path $BackupDirectory

$source = Read-JsonConfig -Path $sourceFullPath
Assert-RuntimeConfig -Json $source.Json -Path $sourceFullPath

New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $backupFullDirectory -Force | Out-Null

$backupPath = $null
if (Test-Path -LiteralPath $targetFullPath) {
    $target = Read-JsonConfig -Path $targetFullPath
    Assert-RuntimeConfig -Json $target.Json -Path $targetFullPath

    $timestamp = Get-Date -Format 'yyyyMMddHHmmss'
    $backupName = "$(Split-Path -Leaf $targetFullPath).bak.$timestamp"
    $backupPath = Join-Path $backupFullDirectory $backupName
    Copy-Item -LiteralPath $targetFullPath -Destination $backupPath -Force
}

$tempPath = Join-Path $targetDirectory "$(Split-Path -Leaf $targetFullPath).tmp.$([Guid]::NewGuid().ToString('N'))"

try {
    Write-Utf8Text -Path $tempPath -Content $source.Raw
    Move-Item -LiteralPath $tempPath -Destination $targetFullPath -Force

    $rotated = Read-JsonConfig -Path $targetFullPath
    Assert-RuntimeConfig -Json $rotated.Json -Path $targetFullPath

    if ($null -ne $backupPath) {
        Write-Host "runtime config rotated: $targetFullPath"
        Write-Host "backup created: $backupPath"
    }
    else {
        Write-Host "runtime config created: $targetFullPath"
    }
}
catch {
    if (Test-Path -LiteralPath $tempPath) {
        Remove-Item -LiteralPath $tempPath -Force
    }

    if ($null -ne $backupPath -and (Test-Path -LiteralPath $backupPath)) {
        Copy-Item -LiteralPath $backupPath -Destination $targetFullPath -Force
    }

    throw
}
