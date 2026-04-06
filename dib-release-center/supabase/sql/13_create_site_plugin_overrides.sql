-- 13_create_site_plugin_overrides.sql
-- 站点级插件授权覆盖规则。

create table if not exists dib_release.site_plugin_overrides (
    id uuid primary key default gen_random_uuid(),
    site_id uuid not null references dib_release.sites (id) on delete cascade,
    package_id uuid not null references dib_release.plugin_packages (id) on delete cascade,
    action text not null check (action in ('allow', 'deny')),
    reason text not null default '',
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_site_plugin_overrides_site_package
    on dib_release.site_plugin_overrides (site_id, package_id);

create index if not exists idx_site_plugin_overrides_package_id
    on dib_release.site_plugin_overrides (package_id);

drop trigger if exists trg_site_plugin_overrides_set_updated_at on dib_release.site_plugin_overrides;
create trigger trg_site_plugin_overrides_set_updated_at
before update on dib_release.site_plugin_overrides
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.site_plugin_overrides to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.site_plugin_overrides to authenticated, service_role;
