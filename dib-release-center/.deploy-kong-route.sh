#!/usr/bin/env bash
set -euo pipefail
FILE=/data/supabase/volumes/api/kong.yml
ROUTE=/data/dib-release-center/kong-route.yml
if ! grep -q "name: dib-release-center" "$FILE"; then
  line=$(grep -n "  ## Block access to /api/mcp" "$FILE" | cut -d: -f1)
  head -n $((line-1)) "$FILE" > "$FILE.new"
  cat "$ROUTE" >> "$FILE.new"
  tail -n +$line "$FILE" >> "$FILE.new"
  mv "$FILE.new" "$FILE"
fi
sed -n '170,240p' "$FILE"
docker restart supabase-kong
