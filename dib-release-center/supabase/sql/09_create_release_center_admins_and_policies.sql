-- 09_create_release_center_admins_and_policies.sql
-- 发布中心管理员、RLS 与 Storage policy。

create table if not exists dib_release.release_center_admins (
    id uuid primary key default gen_random_uuid(),
    user_id uuid null unique references auth.users (id) on delete cascade,
    email text not null unique,
    display_name text not null default '',
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    check (email = lower(email))
);

create index if not exists idx_release_center_admins_active
    on dib_release.release_center_admins (is_active);

drop trigger if exists trg_release_center_admins_set_updated_at on dib_release.release_center_admins;
create trigger trg_release_center_admins_set_updated_at
before update on dib_release.release_center_admins
for each row execute function dib_release.set_updated_at();

create or replace function dib_release.is_release_center_admin()
returns boolean
language sql
stable
security definer
set search_path = dib_release, public
as $$
    select exists (
        select 1
        from dib_release.release_center_admins admins
        where admins.is_active = true
          and (
            (auth.uid() is not null and admins.user_id = auth.uid())
            or lower(admins.email) = lower(coalesce(auth.jwt() ->> 'email', ''))
          )
    );
$$;

create or replace function dib_release.link_current_admin_user()
returns void
language plpgsql
security definer
set search_path = dib_release, public
as $$
declare
    v_email text;
begin
    v_email := lower(coalesce(auth.jwt() ->> 'email', ''));

    if auth.uid() is null or v_email = '' then
        raise exception '当前会话缺少可绑定的认证用户';
    end if;

    update dib_release.release_center_admins
    set user_id = auth.uid(),
        updated_at = now()
    where lower(email) = v_email
      and is_active = true
      and (user_id is null or user_id = auth.uid());
end;
$$;

insert into storage.buckets (id, name, public)
values ('dib-releases', 'dib-releases', true)
on conflict (id) do update
set public = excluded.public,
    name = excluded.name;

alter table dib_release.release_channels enable row level security;
alter table dib_release.release_assets enable row level security;
alter table dib_release.plugin_packages enable row level security;
alter table dib_release.plugin_versions enable row level security;
alter table dib_release.client_versions enable row level security;
alter table dib_release.release_center_admins enable row level security;
alter table dib_release.site_groups enable row level security;
alter table dib_release.sites enable row level security;
alter table dib_release.group_plugin_policies enable row level security;
alter table dib_release.site_plugin_overrides enable row level security;
alter table dib_release.site_heartbeats enable row level security;

create or replace view dib_release.release_channel_overview with (security_invoker = true) as
select
    c.id,
    c.channel_code,
    c.channel_name,
    c.description,
    c.sort_order,
    c.is_default,
    c.is_active,
    c.created_at,
    c.updated_at,
    coalesce(plugin_stats.plugin_version_count, 0) as plugin_version_count,
    coalesce(client_stats.client_version_count, 0) as client_version_count
from dib_release.release_channels c
left join (
    select pv.channel_id, count(*) as plugin_version_count
    from dib_release.plugin_versions pv
    group by pv.channel_id
) plugin_stats on plugin_stats.channel_id = c.id
left join (
    select cv.channel_id, count(*) as client_version_count
    from dib_release.client_versions cv
    group by cv.channel_id
) client_stats on client_stats.channel_id = c.id;

create or replace view dib_release.plugin_versions_view with (security_invoker = true) as
select
    pv.id,
    pv.package_id,
    pp.plugin_code,
    pp.plugin_name,
    pv.channel_id,
    rc.channel_code,
    rc.channel_name,
    pv.version,
    pv.dib_min_version,
    pv.dib_max_version,
    pv.release_notes,
    pv.manifest_json,
    pv.is_published,
    pv.is_mandatory,
    pv.published_at,
    pv.created_at,
    pv.updated_at,
    ra.id as asset_id,
    ra.bucket_name,
    ra.storage_path,
    ra.file_name,
    ra.asset_kind,
    ra.sha256,
    ra.size_bytes,
    ra.mime_type,
    '/storage/v1/object/public/' || ra.bucket_name || '/' || ra.storage_path as package_url
from dib_release.plugin_versions pv
join dib_release.plugin_packages pp on pp.id = pv.package_id
join dib_release.release_channels rc on rc.id = pv.channel_id
join dib_release.release_assets ra on ra.id = pv.asset_id;

create or replace view dib_release.client_versions_view with (security_invoker = true) as
select
    cv.id,
    cv.channel_id,
    rc.channel_code,
    rc.channel_name,
    cv.version,
    cv.min_upgrade_version,
    cv.is_published,
    cv.is_mandatory,
    cv.release_notes,
    cv.published_at,
    cv.created_at,
    cv.updated_at,
    ra.id as asset_id,
    ra.bucket_name,
    ra.storage_path,
    ra.file_name,
    ra.asset_kind,
    ra.sha256,
    ra.size_bytes,
    ra.mime_type,
    '/storage/v1/object/public/' || ra.bucket_name || '/' || ra.storage_path as package_url
from dib_release.client_versions cv
join dib_release.release_channels rc on rc.id = cv.channel_id
join dib_release.release_assets ra on ra.id = cv.asset_id;

create or replace view dib_release.release_manifest_view with (security_invoker = true) as
with latest_client_versions as (
    select distinct on (cv.channel_id)
        cv.channel_id,
        cv.version,
        cv.min_upgrade_version,
        cv.is_published,
        cv.is_mandatory,
        cv.release_notes,
        cv.published_at,
        cv.created_at,
        cv.updated_at,
        ra.bucket_name,
        ra.storage_path,
        ra.file_name,
        ra.sha256,
        ra.size_bytes,
        ra.mime_type
    from dib_release.client_versions cv
    join dib_release.release_assets ra on ra.id = cv.asset_id
    where cv.is_published = true and cv.published_at is not null
    order by cv.channel_id, cv.published_at desc, cv.created_at desc, cv.version desc
),
latest_plugin_versions as (
    select distinct on (pv.package_id, pv.channel_id)
        pv.package_id,
        pv.channel_id,
        pv.version,
        pv.dib_min_version,
        pv.dib_max_version,
        pv.release_notes,
        pv.manifest_json,
        pv.is_published,
        pv.is_mandatory,
        pv.published_at,
        pv.created_at,
        pv.updated_at,
        ra.bucket_name,
        ra.storage_path,
        ra.file_name,
        ra.sha256,
        ra.size_bytes,
        ra.mime_type
    from dib_release.plugin_versions pv
    join dib_release.release_assets ra on ra.id = pv.asset_id
    where pv.is_published = true and pv.published_at is not null
    order by pv.package_id, pv.channel_id, pv.published_at desc, pv.created_at desc, pv.version desc
),
channel_published_at as (
    select rc.id as channel_id, greatest(max(lcv.published_at), max(lpv.published_at)) as published_at
    from dib_release.release_channels rc
    left join latest_client_versions lcv on lcv.channel_id = rc.id
    left join latest_plugin_versions lpv on lpv.channel_id = rc.id
    group by rc.id
)
select
    rc.id as channel_id,
    rc.channel_code,
    rc.channel_name,
    rc.sort_order,
    rc.is_default,
    rc.is_active,
    jsonb_build_object(
        'channel', rc.channel_code,
        'latestVersion', lcv.version,
        'mandatory', coalesce(lcv.is_mandatory, false),
        'minUpgradeVersion', lcv.min_upgrade_version,
        'packageUrl', case when lcv.version is null then null else '/storage/v1/object/public/' || lcv.bucket_name || '/' || lcv.storage_path end,
        'sha256', lcv.sha256,
        'fileName', lcv.file_name,
        'releaseNotes', lcv.release_notes,
        'publishedAt', lcv.published_at
    ) as client_manifest,
    jsonb_build_object(
        'channel', rc.channel_code,
        'publishedAt', cpa.published_at,
        'plugins', coalesce((
            select jsonb_agg(
                jsonb_build_object(
                    'pluginId', pp.plugin_code,
                    'name', pp.plugin_name,
                    'version', lpv.version,
                    'mandatory', coalesce(lpv.is_mandatory, false),
                    'dibMinVersion', lpv.dib_min_version,
                    'dibMaxVersion', lpv.dib_max_version,
                    'packageUrl', case when lpv.version is null then null else '/storage/v1/object/public/' || lpv.bucket_name || '/' || lpv.storage_path end,
                    'sha256', lpv.sha256
                )
                order by pp.plugin_code
            )
            from latest_plugin_versions lpv
            join dib_release.plugin_packages pp on pp.id = lpv.package_id
            where lpv.channel_id = rc.id
        ), '[]'::jsonb)
    ) as plugin_manifest,
    cpa.published_at as published_at
from dib_release.release_channels rc
left join latest_client_versions lcv on lcv.channel_id = rc.id
left join channel_published_at cpa on cpa.channel_id = rc.id
where rc.is_active;

create or replace view dib_release.site_overview with (security_invoker = true) as
select
    s.id,
    s.site_id,
    s.site_name,
    s.group_id,
    sg.group_code,
    sg.group_name,
    s.channel_id,
    rc.channel_code,
    rc.channel_name,
    s.client_version,
    s.machine_name,
    s.last_seen_at,
    s.last_update_check_at,
    s.last_plugin_download_at,
    s.last_client_download_at,
    s.installed_plugins_json,
    s.is_active,
    s.created_at,
    s.updated_at
from dib_release.sites s
left join dib_release.site_groups sg on sg.id = s.group_id
left join dib_release.release_channels rc on rc.id = s.channel_id;

create or replace view dib_release.site_group_statistics with (security_invoker = true) as
select
    sg.id as group_id,
    sg.group_code,
    sg.group_name,
    sg.is_active,
    count(s.id) filter (where s.is_active) as site_count,
    count(s.id) filter (
        where s.is_active
          and s.last_seen_at is not null
          and s.last_seen_at >= now() - interval '24 hours'
    ) as active_site_count_24h,
    max(s.last_seen_at) as latest_seen_at
from dib_release.site_groups sg
left join dib_release.sites s on s.group_id = sg.id
group by sg.id, sg.group_code, sg.group_name, sg.is_active;

create or replace view dib_release.site_effective_plugin_policies_view with (security_invoker = true) as
with group_defaults as (
    select
        s.id as site_row_id,
        s.site_id,
        s.site_name,
        s.group_id,
        sg.group_code,
        gpp.package_id,
        pp.plugin_code,
        pp.plugin_name,
        gpp.is_enabled as group_enabled,
        gpp.min_client_version,
        gpp.max_client_version
    from dib_release.sites s
    join dib_release.site_groups sg on sg.id = s.group_id
    join dib_release.group_plugin_policies gpp on gpp.group_id = sg.id
    join dib_release.plugin_packages pp on pp.id = gpp.package_id
),
override_only as (
    select
        s.id as site_row_id,
        s.site_id,
        s.site_name,
        s.group_id,
        sg.group_code,
        spo.package_id,
        pp.plugin_code,
        pp.plugin_name,
        false as group_enabled,
        '0.0.0'::text as min_client_version,
        '9999.9999.9999'::text as max_client_version
    from dib_release.site_plugin_overrides spo
    join dib_release.sites s on s.id = spo.site_id
    left join dib_release.site_groups sg on sg.id = s.group_id
    join dib_release.plugin_packages pp on pp.id = spo.package_id
    where spo.is_active = true
      and not exists (
          select 1
          from group_defaults gd
          where gd.site_row_id = s.id
            and gd.package_id = spo.package_id
      )
),
policy_basis as (
    select * from group_defaults
    union all
    select * from override_only
)
select
    pb.site_row_id,
    pb.site_id,
    pb.site_name,
    pb.group_id,
    pb.group_code,
    pb.package_id,
    pb.plugin_code,
    pb.plugin_name,
    pb.group_enabled,
    pb.min_client_version,
    pb.max_client_version,
    spo.action as override_action,
    spo.reason as override_reason,
    case
        when spo.action = 'deny' then false
        when spo.action = 'allow' then true
        else pb.group_enabled
    end as effective_is_enabled
from policy_basis pb
left join dib_release.site_plugin_overrides spo
    on spo.site_id = pb.site_row_id
   and spo.package_id = pb.package_id
   and spo.is_active = true;

revoke all on table dib_release.release_channels from anon;
revoke all on table dib_release.release_assets from anon;
revoke all on table dib_release.plugin_packages from anon;
revoke all on table dib_release.plugin_versions from anon;
revoke all on table dib_release.client_versions from anon;
revoke all on table dib_release.release_center_admins from anon;
revoke all on table dib_release.release_channel_overview from anon;
revoke all on table dib_release.plugin_versions_view from anon;
revoke all on table dib_release.client_versions_view from anon;
revoke all on table dib_release.release_manifest_view from anon;
revoke all on table dib_release.site_groups from anon;
revoke all on table dib_release.sites from anon;
revoke all on table dib_release.group_plugin_policies from anon;
revoke all on table dib_release.site_plugin_overrides from anon;
revoke all on table dib_release.site_heartbeats from anon;
revoke all on table dib_release.site_overview from anon;
revoke all on table dib_release.site_group_statistics from anon;
revoke all on table dib_release.site_effective_plugin_policies_view from anon;

grant select on table dib_release.release_channels to authenticated, service_role;
grant select on table dib_release.release_assets to authenticated, service_role;
grant select on table dib_release.plugin_packages to authenticated, service_role;
grant select on table dib_release.plugin_versions to authenticated, service_role;
grant select on table dib_release.client_versions to authenticated, service_role;
grant select on table dib_release.release_center_admins to authenticated, service_role;
grant select on table dib_release.release_channel_overview to authenticated, service_role;
grant select on table dib_release.plugin_versions_view to authenticated, service_role;
grant select on table dib_release.client_versions_view to authenticated, service_role;
grant select on table dib_release.release_manifest_view to authenticated, service_role;
grant select on table dib_release.site_groups to authenticated, service_role;
grant select on table dib_release.sites to authenticated, service_role;
grant select on table dib_release.group_plugin_policies to authenticated, service_role;
grant select on table dib_release.site_plugin_overrides to authenticated, service_role;
grant select on table dib_release.site_heartbeats to authenticated, service_role;
grant select on table dib_release.site_overview to authenticated, service_role;
grant select on table dib_release.site_group_statistics to authenticated, service_role;
grant select on table dib_release.site_effective_plugin_policies_view to authenticated, service_role;
grant insert, update, delete on table dib_release.release_channels to authenticated, service_role;
grant insert, update, delete on table dib_release.release_assets to authenticated, service_role;
grant insert, update, delete on table dib_release.plugin_packages to authenticated, service_role;
grant insert, update, delete on table dib_release.plugin_versions to authenticated, service_role;
grant insert, update, delete on table dib_release.client_versions to authenticated, service_role;
grant insert, update, delete on table dib_release.release_center_admins to authenticated, service_role;
grant insert, update, delete on table dib_release.site_groups to authenticated, service_role;
grant insert, update, delete on table dib_release.sites to authenticated, service_role;
grant insert, update, delete on table dib_release.group_plugin_policies to authenticated, service_role;
grant insert, update, delete on table dib_release.site_plugin_overrides to authenticated, service_role;
grant insert, update, delete on table dib_release.site_heartbeats to authenticated, service_role;

drop policy if exists release_channels_admin_all on dib_release.release_channels;
create policy release_channels_admin_all on dib_release.release_channels
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists release_assets_admin_all on dib_release.release_assets;
create policy release_assets_admin_all on dib_release.release_assets
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists plugin_packages_admin_all on dib_release.plugin_packages;
create policy plugin_packages_admin_all on dib_release.plugin_packages
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists plugin_versions_admin_all on dib_release.plugin_versions;
create policy plugin_versions_admin_all on dib_release.plugin_versions
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists client_versions_admin_all on dib_release.client_versions;
create policy client_versions_admin_all on dib_release.client_versions
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists site_groups_admin_all on dib_release.site_groups;
create policy site_groups_admin_all on dib_release.site_groups
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists sites_admin_all on dib_release.sites;
create policy sites_admin_all on dib_release.sites
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists group_plugin_policies_admin_all on dib_release.group_plugin_policies;
create policy group_plugin_policies_admin_all on dib_release.group_plugin_policies
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists site_plugin_overrides_admin_all on dib_release.site_plugin_overrides;
create policy site_plugin_overrides_admin_all on dib_release.site_plugin_overrides
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists site_heartbeats_admin_all on dib_release.site_heartbeats;
create policy site_heartbeats_admin_all on dib_release.site_heartbeats
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists release_center_admins_select_self_or_admin on dib_release.release_center_admins;
create policy release_center_admins_select_self_or_admin on dib_release.release_center_admins
for select to authenticated
using (
    dib_release.is_release_center_admin()
    or lower(email) = lower(coalesce(auth.jwt() ->> 'email', ''))
);

drop policy if exists release_center_admins_admin_manage on dib_release.release_center_admins;
create policy release_center_admins_admin_manage on dib_release.release_center_admins
for all to authenticated
using (dib_release.is_release_center_admin())
with check (dib_release.is_release_center_admin());

drop policy if exists storage_objects_dib_releases_admin_write on storage.objects;
create policy storage_objects_dib_releases_admin_write on storage.objects
for all to authenticated
using (bucket_id = 'dib-releases' and dib_release.is_release_center_admin())
with check (bucket_id = 'dib-releases' and dib_release.is_release_center_admin());
