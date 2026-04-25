-- 17_create_resource_center_runtime_tables.sql
-- 资源中心最小运行时表结构。

create table if not exists dib_release.resource_plugins (
    id uuid primary key default gen_random_uuid(),
    plugin_code text not null,
    plugin_name text not null default '',
    status text not null default 'Active' check (
        status in ('Active', 'Disabled')
    ),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_resource_plugins_plugin_code
    on dib_release.resource_plugins (plugin_code);

drop trigger if exists trg_resource_plugins_set_updated_at on dib_release.resource_plugins;
create trigger trg_resource_plugins_set_updated_at
before update on dib_release.resource_plugins
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.resource_plugins to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.resource_plugins to authenticated, service_role;

create table if not exists dib_release.resources (
    id uuid primary key default gen_random_uuid(),
    resource_code text not null,
    resource_name text not null default '',
    resource_type text not null check (
        resource_type in ('PostgreSQL', 'SqlServer', 'Supabase', 'HttpService')
    ),
    owner_organization_name text not null default '',
    visibility_scope text not null default 'Private' check (
        visibility_scope in ('Private', 'Shared', 'Platform')
    ),
    config_schema_version integer not null default 1,
    config_payload jsonb not null default '{}'::jsonb,
    capabilities jsonb not null default '[]'::jsonb,
    status text not null default 'Active' check (
        status in ('Draft', 'PendingApproval', 'Active', 'Disabled', 'Archived')
    ),
    description text not null default '',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_resources_resource_code
    on dib_release.resources (resource_code);

create index if not exists idx_resources_resource_type
    on dib_release.resources (resource_type);

create index if not exists idx_resources_status
    on dib_release.resources (status);

drop trigger if exists trg_resources_set_updated_at on dib_release.resources;
create trigger trg_resources_set_updated_at
before update on dib_release.resources
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.resources to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.resources to authenticated, service_role;

create table if not exists dib_release.resource_secrets (
    id uuid primary key default gen_random_uuid(),
    resource_id uuid not null references dib_release.resources (id) on delete cascade,
    secret_payload jsonb not null default '{}'::jsonb,
    secret_version integer not null default 1,
    encryption_mode text not null default 'AppEncrypted',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_resource_secrets_resource_id
    on dib_release.resource_secrets (resource_id);

drop trigger if exists trg_resource_secrets_set_updated_at on dib_release.resource_secrets;
create trigger trg_resource_secrets_set_updated_at
before update on dib_release.resource_secrets
for each row execute function dib_release.set_updated_at();

grant select, insert, update, delete on table dib_release.resource_secrets to service_role;

create table if not exists dib_release.resource_bindings (
    id uuid primary key default gen_random_uuid(),
    site_row_id uuid not null references dib_release.sites (id) on delete cascade,
    plugin_code text not null references dib_release.resource_plugins (plugin_code) on delete restrict,
    resource_id uuid not null references dib_release.resources (id) on delete cascade,
    binding_scope text not null default 'PluginAtSite' check (
        binding_scope in ('PluginAtSite')
    ),
    status text not null default 'Active' check (
        status in ('PendingActivation', 'Active', 'Suspended', 'Revoked')
    ),
    usage_key text not null,
    config_version integer not null default 1,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_resource_bindings_site_plugin_usage
    on dib_release.resource_bindings (site_row_id, plugin_code, usage_key);

create index if not exists idx_resource_bindings_site_plugin_status
    on dib_release.resource_bindings (site_row_id, plugin_code, status);

drop trigger if exists trg_resource_bindings_set_updated_at on dib_release.resource_bindings;
create trigger trg_resource_bindings_set_updated_at
before update on dib_release.resource_bindings
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.resource_bindings to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.resource_bindings to authenticated, service_role;

create table if not exists dib_release.resource_applications (
    id uuid primary key default gen_random_uuid(),
    site_row_id uuid not null references dib_release.sites (id) on delete cascade,
    plugin_code text not null references dib_release.resource_plugins (plugin_code) on delete restrict,
    resource_id uuid not null references dib_release.resources (id) on delete cascade,
    reason text not null default '',
    status text not null default 'Submitted' check (
        status in ('Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Returned', 'Cancelled')
    ),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists idx_resource_applications_site_plugin_status
    on dib_release.resource_applications (site_row_id, plugin_code, status);

drop trigger if exists trg_resource_applications_set_updated_at on dib_release.resource_applications;
create trigger trg_resource_applications_set_updated_at
before update on dib_release.resource_applications
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.resource_applications to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.resource_applications to authenticated, service_role;
