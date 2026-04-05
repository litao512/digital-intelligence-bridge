#!/usr/bin/env bash
set -euo pipefail

SUPABASE_DIR="${SUPABASE_DIR:-/data/supabase}"

usage() {
  cat <<'EOF'
用法：
  ./prod101-supabase-ops.sh status
  ./prod101-supabase-ops.sh rest-env
  ./prod101-supabase-ops.sh storage-env
  ./prod101-supabase-ops.sh restart-rest
  ./prod101-supabase-ops.sh restart-storage
  ./prod101-supabase-ops.sh storage-logs
  ./prod101-supabase-ops.sh check-bucket
  ./prod101-supabase-ops.sh check-admins
  ./prod101-supabase-ops.sh check-schema
EOF
}

require_dir() {
  cd "$SUPABASE_DIR"
}

case "${1:-}" in
  status)
    require_dir
    docker ps --format '{{.Names}}|{{.Image}}|{{.Status}}' | grep supabase || true
    ;;
  rest-env)
    docker inspect supabase-rest --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E 'PGRST_DB_SCHEMAS|PGRST_DB_URI'
    ;;
  storage-env)
    docker inspect supabase-storage --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E 'STORAGE_BACKEND|FILE_STORAGE_BACKEND_PATH|GLOBAL_S3_ENDPOINT'
    ;;
  restart-rest)
    require_dir
    docker compose restart rest
    ;;
  restart-storage)
    require_dir
    docker compose up -d storage
    ;;
  storage-logs)
    docker logs supabase-storage --tail 120
    ;;
  check-bucket)
    docker exec -i supabase-db psql -U postgres -d postgres -c "select id, public, file_size_limit from storage.buckets where id = 'dib-releases';"
    ;;
  check-admins)
    docker exec -i supabase-db psql -U postgres -d postgres -c "select email, is_active, coalesce(user_id::text, 'NULL') as user_id from dib_release.release_center_admins order by created_at desc;"
    ;;
  check-schema)
    docker exec -i supabase-db psql -U postgres -d postgres -c "select table_name from information_schema.tables where table_schema = 'dib_release' order by table_name;"
    ;;
  *)
    usage
    exit 1
    ;;
esac
