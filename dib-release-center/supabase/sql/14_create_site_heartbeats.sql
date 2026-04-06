-- 14_create_site_heartbeats.sql
-- 站点心跳与活动记录。

create table if not exists dib_release.site_heartbeats (
    id uuid primary key default gen_random_uuid(),
    site_id uuid not null references dib_release.sites (id) on delete cascade,
    channel_id uuid null references dib_release.release_channels (id) on delete restrict,
    client_version text not null default '0.0.0' check (
        client_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    installed_plugins_json jsonb not null default '[]'::jsonb,
    event_type text not null default 'heartbeat',
    created_at timestamptz not null default now()
);

create index if not exists idx_site_heartbeats_site_created_at
    on dib_release.site_heartbeats (site_id, created_at desc);

create index if not exists idx_site_heartbeats_event_type
    on dib_release.site_heartbeats (event_type);

grant select on table dib_release.site_heartbeats to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.site_heartbeats to authenticated, service_role;
