-- 04_create_plugin_versions.sql
-- 插件版本表。

create table if not exists dib_release.plugin_versions (
    id uuid primary key default gen_random_uuid(),
    package_id uuid not null references dib_release.plugin_packages (id) on delete cascade,
    channel_id uuid not null references dib_release.release_channels (id) on delete restrict,
    asset_id uuid not null references dib_release.release_assets (id) on delete restrict,
    version text not null check (
        version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    dib_min_version text not null default '0.0.0' check (
        dib_min_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    dib_max_version text not null default '9999.9999.9999' check (
        dib_max_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    release_notes text not null default '',
    manifest_json jsonb not null default '{}'::jsonb,
    is_published boolean not null default false,
    is_mandatory boolean not null default false,
    published_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (package_id, channel_id, version)
);

create index if not exists idx_plugin_versions_package_channel
    on dib_release.plugin_versions (package_id, channel_id);

create index if not exists idx_plugin_versions_channel_published_at
    on dib_release.plugin_versions (channel_id, published_at desc nulls last, created_at desc);

create index if not exists idx_plugin_versions_asset_id
    on dib_release.plugin_versions (asset_id);

create or replace function dib_release.validate_plugin_version_asset()
returns trigger
language plpgsql
as $$
declare
    v_asset_kind text;
begin
    select asset_kind
    into v_asset_kind
    from dib_release.release_assets
    where id = new.asset_id;

    if v_asset_kind is distinct from 'plugin_package' then
        raise exception 'plugin_versions.asset_id must reference a plugin_package asset';
    end if;

    return new;
end;
$$;

drop trigger if exists trg_plugin_versions_set_updated_at on dib_release.plugin_versions;
create trigger trg_plugin_versions_set_updated_at
before update on dib_release.plugin_versions
for each row execute function dib_release.set_updated_at();

drop trigger if exists trg_plugin_versions_validate_asset on dib_release.plugin_versions;
create trigger trg_plugin_versions_validate_asset
before insert or update on dib_release.plugin_versions
for each row execute function dib_release.validate_plugin_version_asset();

grant select on table dib_release.plugin_versions to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.plugin_versions to authenticated, service_role;

