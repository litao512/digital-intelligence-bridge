[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Repo = "litao512/digital-intelligence-bridge",
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $env:LOCALAPPDATA "UniversalTrayTool\appsettings.json"
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) not found. Install gh first."
}

if (-not (Test-Path $ConfigPath)) {
    throw "Config file not found: $ConfigPath"
}

$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$url = [string]$cfg.Supabase.Url
$anonKey = [string]$cfg.Supabase.AnonKey
$schema = [string]$cfg.Supabase.Schema

if ([string]::IsNullOrWhiteSpace($url) -or [string]::IsNullOrWhiteSpace($anonKey)) {
    throw "Supabase Url/AnonKey missing in config: $ConfigPath"
}

if ([string]::IsNullOrWhiteSpace($schema)) {
    $schema = "dib"
}

$supabaseHost = ([uri]$url).Host
Write-Host "Target repo: $Repo"
Write-Host "Config path: $ConfigPath"
Write-Host "Supabase host: $supabaseHost"
Write-Host "Schema: $schema"

if ($PSCmdlet.ShouldProcess($Repo, "Set SUPABASE_URL secret")) {
    $url | gh secret set SUPABASE_URL --repo $Repo
}

if ($PSCmdlet.ShouldProcess($Repo, "Set SUPABASE_ANON_KEY secret")) {
    $anonKey | gh secret set SUPABASE_ANON_KEY --repo $Repo
}

if ($PSCmdlet.ShouldProcess($Repo, "Set SUPABASE_SCHEMA secret")) {
    $schema | gh secret set SUPABASE_SCHEMA --repo $Repo
}

Write-Host "Secrets sync completed."
