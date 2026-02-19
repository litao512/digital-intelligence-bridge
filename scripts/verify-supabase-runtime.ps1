param(
    [string]$SupabaseUrl,
    [string]$SupabaseAnonKey,
    [string]$Schema = "dib",
    [string]$RuntimeConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RuntimeConfigPath)) {
    $RuntimeConfigPath = Join-Path $env:LOCALAPPDATA "UniversalTrayTool\appsettings.runtime.json"
}

if (([string]::IsNullOrWhiteSpace($SupabaseUrl) -or [string]::IsNullOrWhiteSpace($SupabaseAnonKey)) -and (Test-Path $RuntimeConfigPath)) {
    $runtime = Get-Content $RuntimeConfigPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($SupabaseUrl)) {
        $SupabaseUrl = [string]$runtime.Supabase.Url
    }
    if ([string]::IsNullOrWhiteSpace($SupabaseAnonKey)) {
        $SupabaseAnonKey = [string]$runtime.Supabase.AnonKey
    }
    if ($PSBoundParameters.ContainsKey("Schema") -eq $false -or [string]::IsNullOrWhiteSpace($Schema)) {
        $Schema = [string]$runtime.Supabase.Schema
    }
}

if ([string]::IsNullOrWhiteSpace($SupabaseUrl) -or [string]::IsNullOrWhiteSpace($SupabaseAnonKey)) {
    throw "SupabaseUrl/SupabaseAnonKey are required. Pass params or provide $RuntimeConfigPath."
}

if ([string]::IsNullOrWhiteSpace($Schema)) {
    $Schema = "public"
}

$url = $SupabaseUrl.TrimEnd("/") + "/rest/v1/todos?select=id&limit=1"
$headers = @{
    "apikey" = $SupabaseAnonKey
    "Authorization" = "Bearer $SupabaseAnonKey"
    "Accept-Profile" = $Schema
}

try {
    $resp = Invoke-WebRequest -Method Get -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 10
    Write-Host "status: $($resp.StatusCode)"
    Write-Host "schema: $Schema"
    Write-Host "url: $url"
    if ($resp.Content) {
        Write-Host "body: $($resp.Content)"
    }
}
catch {
    if ($_.Exception.Response -ne $null) {
        $statusCode = [int]$_.Exception.Response.StatusCode
        Write-Host "status: $statusCode"
    }
    Write-Error $_
    exit 1
}
