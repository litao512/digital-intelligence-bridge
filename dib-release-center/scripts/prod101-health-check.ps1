[CmdletBinding()]
param(
  [string]$SshTarget = 'prod-101',
  [string]$BaseUrl = 'http://101.42.19.26:8000',
  [switch]$SkipHttpCheck
)

$ErrorActionPreference = 'Stop'

function Invoke-Ssh {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Command
  )

  $ssh = 'C:\Windows\System32\OpenSSH\ssh.exe'
  & $ssh $SshTarget $Command
}

function Write-Section {
  param([string]$Title)
  Write-Host ''
  Write-Host "=== $Title ==="
}

$bucketSql = "select id, public, file_size_limit from storage.buckets where id = 'dib-releases';"
$adminSql = "select email, is_active, coalesce(user_id::text, 'NULL') as user_id from dib_release.release_center_admins order by created_at desc;"
$schemaSql = "select table_name from information_schema.tables where table_schema = 'dib_release' order by table_name;"

Write-Section '容器状态'
Invoke-Ssh "docker ps --format '{{.Names}}|{{.Status}}' | grep supabase"

Write-Section 'REST schema'
Invoke-Ssh "docker inspect supabase-rest --format '{{range .Config.Env}}{{println .}}{{end}}' | grep PGRST_DB_SCHEMAS"

Write-Section 'Storage 后端'
Invoke-Ssh "docker inspect supabase-storage --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E 'STORAGE_BACKEND|FILE_STORAGE_BACKEND_PATH|GLOBAL_S3_ENDPOINT'"

Write-Section 'Bucket 检查'
Invoke-Ssh ('docker exec -i supabase-db psql -U postgres -d postgres -c "{0}"' -f $bucketSql)

Write-Section '管理员检查'
Invoke-Ssh ('docker exec -i supabase-db psql -U postgres -d postgres -c "{0}"' -f $adminSql)

Write-Section 'Schema 检查'
Invoke-Ssh ('docker exec -i supabase-db psql -U postgres -d postgres -c "{0}"' -f $schemaSql)

if (-not $SkipHttpCheck) {
  Write-Section 'Manifest HTTP 检查'
  $urls = @(
    "$BaseUrl/storage/v1/object/public/dib-releases/manifests/stable/client-manifest.json",
    "$BaseUrl/storage/v1/object/public/dib-releases/manifests/stable/plugin-manifest.json"
  )

  foreach ($url in $urls) {
    try {
      $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 15
      Write-Host "OK  $($response.StatusCode)  $url"
    }
    catch {
      Write-Host "ERR $url"
      throw
    }
  }
}

Write-Section '检查完成'
Write-Host 'prod101 发布中心关键链路检查通过。'
