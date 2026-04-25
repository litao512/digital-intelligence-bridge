-- 20_create_discover_site_resources_rpc.sql
-- 返回站点资源中心发现结果。

drop function if exists dib_release.discover_site_resources(text, text, text, jsonb);

create or replace function dib_release.discover_site_resources(
    p_channel_code text,
    p_site_id text,
    p_client_version text default null,
    p_plugins_json jsonb default '[]'::jsonb
)
returns jsonb
language plpgsql
security definer
set search_path = dib_release, public
as $$
declare
    v_site_row_id uuid;
    v_authorized jsonb := '[]'::jsonb;
    v_available jsonb := '[]'::jsonb;
    v_pending jsonb := '[]'::jsonb;
begin
    select s.id
    into v_site_row_id
    from dib_release.sites s
    where s.site_id = p_site_id
      and s.is_active = true
    limit 1;

    if v_site_row_id is null then
        return jsonb_build_object(
            'availableToApply', '[]'::jsonb,
            'authorized', '[]'::jsonb,
            'pendingApplications', '[]'::jsonb
        );
    end if;

    select coalesce(result -> 'resources', '[]'::jsonb)
    into v_authorized
    from (
        select dib_release.get_site_authorized_resources(
            p_channel_code,
            p_site_id,
            p_client_version,
            p_plugins_json
        ) as result
    ) authorized_result;

    with requested_plugins as (
        select distinct
            item ->> 'pluginCode' as plugin_code,
            requirement ->> 'resourceType' as resource_type
        from jsonb_array_elements(coalesce(p_plugins_json, '[]'::jsonb)) as item
        cross join lateral jsonb_array_elements(coalesce(item -> 'requirements', '[]'::jsonb)) as requirement
        where coalesce(item ->> 'pluginCode', '') <> ''
          and coalesce(requirement ->> 'resourceType', '') <> ''
    ),
    available_rows as (
        select
            r.id,
            r.resource_code,
            r.resource_name,
            r.resource_type,
            r.visibility_scope,
            array_agg(distinct requested_plugins.plugin_code order by requested_plugins.plugin_code) as matched_plugins
        from dib_release.resources r
        join requested_plugins on requested_plugins.resource_type = r.resource_type
        where r.status = 'Active'
          and not exists (
              select 1
              from dib_release.resource_bindings rb
              where rb.site_row_id = v_site_row_id
                and rb.plugin_code = requested_plugins.plugin_code
                and rb.resource_id = r.id
                and rb.status in ('PendingActivation', 'Active')
          )
        group by r.id, r.resource_code, r.resource_name, r.resource_type, r.visibility_scope
    )
    select coalesce(
        jsonb_agg(
            jsonb_build_object(
                'resourceId', available_rows.id::text,
                'resourceCode', available_rows.resource_code,
                'resourceName', available_rows.resource_name,
                'resourceType', available_rows.resource_type,
                'visibilityScope', available_rows.visibility_scope,
                'matchedPlugins', to_jsonb(available_rows.matched_plugins)
            )
            order by available_rows.resource_type, available_rows.resource_code
        ),
        '[]'::jsonb
    )
    into v_available
    from available_rows;

    select coalesce(
        jsonb_agg(
            jsonb_build_object(
                'applicationId', ra.id::text,
                'applicationType', 'UseResource',
                'resourceId', ra.resource_id::text,
                'status', ra.status
            )
            order by ra.created_at desc
        ),
        '[]'::jsonb
    )
    into v_pending
    from dib_release.resource_applications ra
    where ra.site_row_id = v_site_row_id
      and ra.status in ('Draft', 'Submitted', 'UnderReview');

    return jsonb_build_object(
        'availableToApply', v_available,
        'authorized', v_authorized,
        'pendingApplications', v_pending
    );
end;
$$;

grant execute on function dib_release.discover_site_resources(text, text, text, jsonb) to anon, authenticated, service_role;
