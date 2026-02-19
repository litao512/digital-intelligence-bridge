param(
    [Parameter(Mandatory = $true)]
    [string]$SupabaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$SupabaseAnonKey,

    [string]$Schema = "dib",

    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $env:LOCALAPPDATA "UniversalTrayTool\appsettings.runtime.json"
}

$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$payload = @{
    Supabase = @{
        Url = $SupabaseUrl
        AnonKey = $SupabaseAnonKey
        Schema = $Schema
    }
}

$json = $payload | ConvertTo-Json -Depth 5
Set-Content -Path $OutputPath -Value $json -Encoding utf8

Write-Host "runtime config written: $OutputPath"
