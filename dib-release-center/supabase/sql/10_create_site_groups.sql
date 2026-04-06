-- 10_create_site_groups.sql
-- DIB 站点分组定义。

create table if not exists dib_release.site_groups (
    id uuid primary key default gen_random_uuid(),
    group_code text not null check (group_code ~ '^[a-z][a-z0-9-]*$'),
    group_name text not null,
    description text not null default '',
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists ux_site_groups_group_code
    on dib_release.site_groups (group_code);

create index if not exists idx_site_groups_is_active
    on dib_release.site_groups (is_active);

drop trigger if exists trg_site_groups_set_updated_at on dib_release.site_groups;
create trigger trg_site_groups_set_updated_at
before update on dib_release.site_groups
for each row execute function dib_release.set_updated_at();

insert into dib_release.site_groups (
    group_code,
    group_name,
    description,
    is_active
)
values (
    'unassigned',
    '未分组',
    'DIB 首次接入后的默认分组',
    true
)
on conflict (group_code) do update
set group_name = excluded.group_name,
    description = excluded.description,
    is_active = excluded.is_active,
    updated_at = now();

grant select on table dib_release.site_groups to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.site_groups to authenticated, service_role;
