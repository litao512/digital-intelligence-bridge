-- 05_create_client_versions.sql
-- 客户端版本表。

create table if not exists dib_release.client_versions (
    id uuid primary key default gen_random_uuid(),
    channel_id uuid not null references dib_release.release_channels (id) on delete restrict,
    asset_id uuid not null references dib_release.release_assets (id) on delete restrict,
    version text not null check (
        version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    min_upgrade_version text not null default '0.0.0' check (
        min_upgrade_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    is_published boolean not null default false,
    is_mandatory boolean not null default false,
    release_notes text not null default '',
    published_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (channel_id, version)
);

create index if not exists idx_client_versions_channel_published_at
    on dib_release.client_versions (channel_id, published_at desc nulls last, created_at desc);

create index if not exists idx_client_versions_asset_id
    on dib_release.client_versions (asset_id);

create or replace function dib_release.validate_client_version_asset()
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

    if v_asset_kind is distinct from 'client_package' then
        raise exception 'client_versions.asset_id must reference a client_package asset';
    end if;

    return new;
end;
$$;

drop trigger if exists trg_client_versions_set_updated_at on dib_release.client_versions;
create trigger trg_client_versions_set_updated_at
before update on dib_release.client_versions
for each row execute function dib_release.set_updated_at();

drop trigger if exists trg_client_versions_validate_asset on dib_release.client_versions;
create trigger trg_client_versions_validate_asset
before insert or update on dib_release.client_versions
for each row execute function dib_release.validate_client_version_asset();

grant select on table dib_release.client_versions to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.client_versions to authenticated, service_role;

