-- 16_seed_base_site_group_and_default_policy.sql
-- 增量迁移：补正式默认分组 base，并让首注册站点默认进入 base。

insert into dib_release.site_groups (
    group_code,
    group_name,
    description,
    is_active
)
values (
    'base',
    '基础授权',
    'DIB 新站点首次注册时默认进入的基础分组',
    true
)
on conflict (group_code) do update
set group_name = excluded.group_name,
    description = excluded.description,
    is_active = excluded.is_active,
    updated_at = now();

update dib_release.site_groups
set description = 'DIB 首次接入后的待人工分组兜底分组',
    updated_at = now()
where group_code = 'unassigned'
  and description is distinct from 'DIB 首次接入后的待人工分组兜底分组';

insert into dib_release.group_plugin_policies (
    group_id,
    package_id,
    is_enabled,
    min_client_version,
    max_client_version
)
select
    sg.id,
    pp.id,
    true,
    '1.0.0',
    '9999.9999.9999'
from dib_release.site_groups sg
join dib_release.plugin_packages pp on pp.plugin_code = 'patient-registration'
where sg.group_code = 'base'
on conflict (group_id, package_id) do update
set is_enabled = excluded.is_enabled,
    min_client_version = excluded.min_client_version,
    max_client_version = excluded.max_client_version,
    updated_at = now();

drop function if exists dib_release.register_site_heartbeat(text, text, text, text, text, jsonb, text);

create or replace function dib_release.register_site_heartbeat(
    p_site_id text,
    p_site_name text,
    p_channel_code text,
    p_client_version text,
    p_machine_name text,
    p_installed_plugins_json jsonb default '[]'::jsonb,
    p_event_type text default 'heartbeat'
)
returns jsonb
language plpgsql
security definer
set search_path = dib_release, public
as $$
declare
    v_site_row_id uuid;
    v_channel_id uuid;
    v_group_id uuid;
begin
    select id into v_channel_id
    from dib_release.release_channels
    where release_channels.channel_code = register_site_heartbeat.p_channel_code
    limit 1;

    select id into v_group_id
    from dib_release.site_groups
    where group_code = 'base'
      and is_active = true
    limit 1;

    if v_group_id is null then
        select id into v_group_id
        from dib_release.site_groups
        where group_code = 'unassigned'
          and is_active = true
        limit 1;
    end if;

    insert into dib_release.sites (
        site_id,
        site_name,
        group_id,
        channel_id,
        client_version,
        machine_name,
        last_seen_at,
        last_update_check_at,
        last_plugin_download_at,
        last_client_download_at,
        installed_plugins_json,
        is_active
    )
    values (
        register_site_heartbeat.p_site_id,
        coalesce(nullif(register_site_heartbeat.p_site_name, ''), register_site_heartbeat.p_machine_name),
        v_group_id,
        v_channel_id,
        register_site_heartbeat.p_client_version,
        register_site_heartbeat.p_machine_name,
        now(),
        case when register_site_heartbeat.p_event_type = 'update_check' then now() else null end,
        case when register_site_heartbeat.p_event_type = 'plugin_download' then now() else null end,
        case when register_site_heartbeat.p_event_type = 'client_download' then now() else null end,
        coalesce(register_site_heartbeat.p_installed_plugins_json, '[]'::jsonb),
        true
    )
    on conflict (site_id) do update
    set site_name = excluded.site_name,
        group_id = coalesce(dib_release.sites.group_id, excluded.group_id),
        channel_id = excluded.channel_id,
        client_version = excluded.client_version,
        machine_name = excluded.machine_name,
        last_seen_at = now(),
        last_update_check_at = case
            when register_site_heartbeat.p_event_type = 'update_check' then now()
            else dib_release.sites.last_update_check_at
        end,
        last_plugin_download_at = case
            when register_site_heartbeat.p_event_type = 'plugin_download' then now()
            else dib_release.sites.last_plugin_download_at
        end,
        last_client_download_at = case
            when register_site_heartbeat.p_event_type = 'client_download' then now()
            else dib_release.sites.last_client_download_at
        end,
        installed_plugins_json = coalesce(register_site_heartbeat.p_installed_plugins_json, '[]'::jsonb),
        is_active = true,
        updated_at = now()
    returning id into v_site_row_id;

    insert into dib_release.site_heartbeats (
        site_id,
        channel_id,
        client_version,
        installed_plugins_json,
        event_type
    )
    values (
        v_site_row_id,
        v_channel_id,
        register_site_heartbeat.p_client_version,
        coalesce(register_site_heartbeat.p_installed_plugins_json, '[]'::jsonb),
        register_site_heartbeat.p_event_type
    );

    return jsonb_build_object(
        'siteId', register_site_heartbeat.p_site_id,
        'siteName', coalesce(nullif(register_site_heartbeat.p_site_name, ''), register_site_heartbeat.p_machine_name),
        'channelCode', register_site_heartbeat.p_channel_code,
        'eventType', register_site_heartbeat.p_event_type
    );
end;
$$;

grant execute on function dib_release.register_site_heartbeat(text, text, text, text, text, jsonb, text) to anon, authenticated, service_role;
