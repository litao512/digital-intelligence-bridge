-- 12_create_group_plugin_policies.sql
-- 站点组级插件授权规则。

create table if not exists dib_release.group_plugin_policies (
    id uuid primary key default gen_random_uuid(),
    group_id uuid not null references dib_release.site_groups (id) on delete cascade,
    package_id uuid not null references dib_release.plugin_packages (id) on delete cascade,
    is_enabled boolean not null default true,
    min_client_version text not null default '0.0.0' check (
        min_client_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    max_client_version text not null default '9999.9999.9999' check (
        max_client_version ~ '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$'
    ),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_group_plugin_policies_group_package
    on dib_release.group_plugin_policies (group_id, package_id);

create index if not exists idx_group_plugin_policies_package_id
    on dib_release.group_plugin_policies (package_id);

drop trigger if exists trg_group_plugin_policies_set_updated_at on dib_release.group_plugin_policies;
create trigger trg_group_plugin_policies_set_updated_at
before update on dib_release.group_plugin_policies
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.group_plugin_policies to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.group_plugin_policies to authenticated, service_role;
