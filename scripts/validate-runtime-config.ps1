param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) {
    throw "runtime config file not found: $Path"
}

$raw = Get-Content $Path -Raw
if (-not ($raw | Test-Json)) {
    throw "runtime config is not valid JSON: $Path"
}

$json = $raw | ConvertFrom-Json

if ($null -eq $json.Supabase) {
    throw "missing required section: Supabase"
}

$url = [string]$json.Supabase.Url
$anonKey = [string]$json.Supabase.AnonKey
$schema = [string]$json.Supabase.Schema

if ([string]::IsNullOrWhiteSpace($url)) {
    throw "Supabase.Url is required"
}

if ([string]::IsNullOrWhiteSpace($anonKey)) {
    throw "Supabase.AnonKey is required"
}

try {
    $uri = [Uri]$url
    if ($uri.Scheme -ne "http" -and $uri.Scheme -ne "https") {
        throw "invalid scheme: $($uri.Scheme)"
    }
}
catch {
    throw "Supabase.Url is not a valid http/https URL: $url"
}

if (-not [string]::IsNullOrWhiteSpace($schema) -and $schema.Contains(" ")) {
    throw "Supabase.Schema must not contain spaces"
}

Write-Host "runtime config ok: $Path"
