# Supabase Schema Setup (`dib`)

Date: 2026-02-19

## Purpose
Use an isolated schema `dib` for this project and avoid mixing tables with other projects.

## Apply migration
1. Open Supabase SQL Editor for your self-hosted instance.
2. Run: `database/migrations/2026-02-19-create-dib-schema-up.sql`

## Rollback (if needed)
Run: `database/migrations/2026-02-19-create-dib-schema-down.sql`

## Verify via REST
- Endpoint: `GET /rest/v1/todos?select=id&limit=1`
- Headers:
  - `apikey: <anon key>`
  - `Authorization: Bearer <anon key>`
  - `Accept-Profile: dib`

Expected:
- `200` with array response (empty array is OK)
