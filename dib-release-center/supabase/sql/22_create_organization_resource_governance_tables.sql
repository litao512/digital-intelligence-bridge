-- 22_create_organization_resource_governance_tables.sql
-- DIB 中心资源中心一期：单位治理、单位插件授权和单位资源授权。

create table if not exists dib_release.organizations (
    id uuid primary key default gen_random_uuid(),
    code text not null,
    name text not null,
    organization_type text not null default 'Unknown',
    business_tags jsonb not null default '[]'::jsonb,
    status text not null default 'Active' check (
        status in ('Active', 'Inactive')
    ),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint ck_organizations_business_tags_array check (
        jsonb_typeof(business_tags) = 'array'
    )
);

create unique index if not exists ux_organizations_code
    on dib_release.organizations (code);

create index if not exists idx_organizations_type_status
    on dib_release.organizations (organization_type, status);

drop trigger if exists trg_organizations_set_updated_at on dib_release.organizations;
create trigger trg_organizations_set_updated_at
before update on dib_release.organizations
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.organizations to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.organizations to authenticated, service_role;

alter table dib_release.sites
    add column if not exists organization_id uuid null references dib_release.organizations (id) on delete restrict,
    add column if not exists business_tags jsonb not null default '[]'::jsonb;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_sites_business_tags_array'
          and conrelid = 'dib_release.sites'::regclass
    ) then
        alter table dib_release.sites
            add constraint ck_sites_business_tags_array check (
                jsonb_typeof(business_tags) = 'array'
            );
    end if;
end;
$$;

create index if not exists idx_sites_organization_id
    on dib_release.sites (organization_id);

alter table dib_release.resources
    add column if not exists owner_organization_id uuid null references dib_release.organizations (id) on delete restrict,
    add column if not exists business_tags jsonb not null default '[]'::jsonb;

alter table dib_release.resources
    drop column if exists owner_organization_name;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_resources_business_tags_array'
          and conrelid = 'dib_release.resources'::regclass
    ) then
        alter table dib_release.resources
            add constraint ck_resources_business_tags_array check (
                jsonb_typeof(business_tags) = 'array'
            );
    end if;
end;
$$;

create index if not exists idx_resources_owner_organization_id
    on dib_release.resources (owner_organization_id);

create table if not exists dib_release.organization_plugin_permissions (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references dib_release.organizations (id) on delete cascade,
    plugin_code text not null,
    status text not null default 'Active' check (
        status in ('Active', 'Inactive')
    ),
    granted_by text not null default '',
    granted_at timestamptz not null default now(),
    expires_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_org_plugin_permissions_org_plugin
    on dib_release.organization_plugin_permissions (organization_id, plugin_code);

create index if not exists idx_org_plugin_permissions_org_status
    on dib_release.organization_plugin_permissions (organization_id, status);

create index if not exists idx_org_plugin_permissions_plugin_status
    on dib_release.organization_plugin_permissions (plugin_code, status);

drop trigger if exists trg_org_plugin_permissions_set_updated_at on dib_release.organization_plugin_permissions;
create trigger trg_org_plugin_permissions_set_updated_at
before update on dib_release.organization_plugin_permissions
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.organization_plugin_permissions to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.organization_plugin_permissions to authenticated, service_role;

create table if not exists dib_release.organization_resource_permissions (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references dib_release.organizations (id) on delete cascade,
    resource_id uuid not null references dib_release.resources (id) on delete cascade,
    status text not null default 'Active' check (
        status in ('Active', 'Inactive')
    ),
    granted_by text not null default '',
    granted_at timestamptz not null default now(),
    expires_at timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_org_resource_permissions_org_resource
    on dib_release.organization_resource_permissions (organization_id, resource_id);

create index if not exists idx_org_resource_permissions_org_status
    on dib_release.organization_resource_permissions (organization_id, status);

create index if not exists idx_org_resource_permissions_resource_status
    on dib_release.organization_resource_permissions (resource_id, status);

drop trigger if exists trg_org_resource_permissions_set_updated_at on dib_release.organization_resource_permissions;
create trigger trg_org_resource_permissions_set_updated_at
before update on dib_release.organization_resource_permissions
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.organization_resource_permissions to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.organization_resource_permissions to authenticated, service_role;
