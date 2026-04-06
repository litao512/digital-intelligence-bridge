-- 15_create_site_runtime_rpcs.sql
-- DIB 客户端运行时使用的站点注册/心跳与按站点插件清单 RPC。

drop function if exists dib_release.register_site_heartbeat(text, text, text, text, text, jsonb, text);

create or replace function dib_release.semver_cmp(left_version text, right_version text)
returns integer
language plpgsql
immutable
as $$
declare
    left_core text := split_part(coalesce(left_version, '0.0.0'), '-', 1);
    right_core text := split_part(coalesce(right_version, '0.0.0'), '-', 1);
    left_parts text[];
    right_parts text[];
    idx integer;
    left_value integer;
    right_value integer;
begin
    left_parts := string_to_array(left_core, '.');
    right_parts := string_to_array(right_core, '.');

    for idx in 1..greatest(coalesce(array_length(left_parts, 1), 0), coalesce(array_length(right_parts, 1), 0)) loop
        left_value := coalesce(left_parts[idx], '0')::integer;
        right_value := coalesce(right_parts[idx], '0')::integer;

        if left_value <> right_value then
            return case when left_value > right_value then 1 else -1 end;
        end if;
    end loop;

    return 0;
end;
$$;

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
    where group_code = 'unassigned'
    limit 1;

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

create or replace function dib_release.get_site_plugin_manifest(
    p_channel_code text,
    p_site_id text,
    p_client_version text default null
)
returns jsonb
language plpgsql
security definer
set search_path = dib_release, public
as $$
declare
    v_site_row_id uuid;
    v_effective_client_version text;
    v_published_at timestamptz;
    v_plugins jsonb;
begin
    select s.id, coalesce(nullif(p_client_version, ''), s.client_version)
    into v_site_row_id, v_effective_client_version
    from dib_release.sites s
    where s.site_id = p_site_id
    limit 1;

    if v_site_row_id is null then
        return jsonb_build_object(
            'channel', p_channel_code,
            'siteId', p_site_id,
            'publishedAt', null,
            'plugins', '[]'::jsonb
        );
    end if;

    with latest_plugin_versions as (
        select distinct on (pv.package_id)
            pv.package_id,
            pv.version,
            pv.dib_min_version,
            pv.dib_max_version,
            pv.is_mandatory,
            pv.published_at,
            ra.bucket_name,
            ra.storage_path,
            ra.sha256
        from dib_release.plugin_versions pv
        join dib_release.release_channels rc on rc.id = pv.channel_id
        join dib_release.release_assets ra on ra.id = pv.asset_id
        where rc.channel_code = p_channel_code
          and pv.is_published = true
          and pv.published_at is not null
        order by pv.package_id, pv.published_at desc, pv.created_at desc, pv.version desc
    ),
    authorized_plugins as (
        select
            sep.site_row_id,
            sep.plugin_code,
            sep.plugin_name,
            sep.package_id,
            lpv.version,
            lpv.dib_min_version,
            lpv.dib_max_version,
            lpv.is_mandatory,
            lpv.published_at,
            '/storage/v1/object/public/' || lpv.bucket_name || '/' || lpv.storage_path as package_url,
            lpv.sha256
        from dib_release.site_effective_plugin_policies_view sep
        join latest_plugin_versions lpv on lpv.package_id = sep.package_id
        where sep.site_row_id = v_site_row_id
          and sep.effective_is_enabled = true
          and dib_release.semver_cmp(v_effective_client_version, sep.min_client_version) >= 0
          and dib_release.semver_cmp(v_effective_client_version, sep.max_client_version) <= 0
    )
    select
        max(published_at),
        coalesce(
            jsonb_agg(
                jsonb_build_object(
                    'pluginId', plugin_code,
                    'name', plugin_name,
                    'version', version,
                    'mandatory', coalesce(is_mandatory, false),
                    'dibMinVersion', dib_min_version,
                    'dibMaxVersion', dib_max_version,
                    'packageUrl', package_url,
                    'sha256', sha256
                )
                order by plugin_code
            ),
            '[]'::jsonb
        )
    into v_published_at, v_plugins
    from authorized_plugins;

    return jsonb_build_object(
        'channel', p_channel_code,
        'siteId', p_site_id,
        'publishedAt', v_published_at,
        'plugins', coalesce(v_plugins, '[]'::jsonb)
    );
end;
$$;

grant execute on function dib_release.semver_cmp(text, text) to anon, authenticated, service_role;
grant execute on function dib_release.register_site_heartbeat(text, text, text, text, text, jsonb, text) to anon, authenticated, service_role;
grant execute on function dib_release.get_site_plugin_manifest(text, text, text) to anon, authenticated, service_role;
