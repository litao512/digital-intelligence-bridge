-- 11_create_sites.sql
-- DIB 安装实例站点表。

create table if not exists dib_release.sites (
    id uuid primary key default gen_random_uuid(),
    site_id text not null check (
        site_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    ),
    site_name text not null default '',
    group_id uuid null references dib_release.site_groups (id) on delete restrict,
    channel_id uuid null references dib_release.release_channels (id) on delete restrict,
    client_version text not null default '0.0.0' check (
        client_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    machine_name text not null default '',
    last_seen_at timestamptz null,
    last_update_check_at timestamptz null,
    last_plugin_download_at timestamptz null,
    last_client_download_at timestamptz null,
    installed_plugins_json jsonb not null default '[]'::jsonb,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_sites_site_id
    on dib_release.sites (site_id);

create index if not exists idx_sites_group_id
    on dib_release.sites (group_id);

create index if not exists idx_sites_channel_id
    on dib_release.sites (channel_id);

create index if not exists idx_sites_last_seen_at
    on dib_release.sites (last_seen_at desc nulls last);

drop trigger if exists trg_sites_set_updated_at on dib_release.sites;
create trigger trg_sites_set_updated_at
before update on dib_release.sites
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.sites to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.sites to authenticated, service_role;
