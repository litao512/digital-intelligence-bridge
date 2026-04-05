-- 02_create_release_assets.sql
-- 发布资产元数据表。

create table if not exists dib_release.release_assets (
    id uuid primary key default gen_random_uuid(),
    bucket_name text not null default 'dib-releases',
    storage_path text not null,
    file_name text not null,
    asset_kind text not null check (asset_kind in ('plugin_package', 'client_package', 'manifest')),
    sha256 text not null check (sha256 ~ '^[a-f0-9]{64}$'),
    size_bytes bigint not null default 0 check (size_bytes >= 0),
    mime_type text not null default '',
    uploaded_by uuid null references auth.users (id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (bucket_name, storage_path)
);

create index if not exists idx_release_assets_asset_kind
    on dib_release.release_assets (asset_kind);

create index if not exists idx_release_assets_created_at
    on dib_release.release_assets (created_at desc);

drop trigger if exists trg_release_assets_set_updated_at on dib_release.release_assets;
create trigger trg_release_assets_set_updated_at
before update on dib_release.release_assets
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.release_assets to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.release_assets to authenticated, service_role;
