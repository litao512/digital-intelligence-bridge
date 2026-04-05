-- 03_create_plugin_packages.sql
-- 插件定义表。

create table if not exists dib_release.plugin_packages (
    id uuid primary key default gen_random_uuid(),
    plugin_code text not null unique check (plugin_code ~ '^[a-z][a-z0-9-]*$'),
    plugin_name text not null,
    entry_type text not null,
    author text not null default '',
    description text not null default '',
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists idx_plugin_packages_is_active
    on dib_release.plugin_packages (is_active);

create index if not exists idx_plugin_packages_created_at
    on dib_release.plugin_packages (created_at desc);

drop trigger if exists trg_plugin_packages_set_updated_at on dib_release.plugin_packages;
create trigger trg_plugin_packages_set_updated_at
before update on dib_release.plugin_packages
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.plugin_packages to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.plugin_packages to authenticated, service_role;
