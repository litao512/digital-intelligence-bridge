-- 19_create_get_site_authorized_resources_rpc.sql
-- 返回站点已授权资源。

drop function if exists dib_release.get_site_authorized_resources(text, text, text, jsonb);

create or replace function dib_release.get_site_authorized_resources(
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
    v_resources jsonb := '[]'::jsonb;
begin
    select s.id
    into v_site_row_id
    from dib_release.sites s
    where s.site_id = p_site_id
      and s.is_active = true
    limit 1;

    if v_site_row_id is null then
        return jsonb_build_object('resources', '[]'::jsonb);
    end if;

    with requested_plugins as (
        select distinct
            item ->> 'pluginCode' as plugin_code
        from jsonb_array_elements(coalesce(p_plugins_json, '[]'::jsonb)) as item
        where coalesce(item ->> 'pluginCode', '') <> ''
    ),
    authorized_rows as (
        select
            rb.id,
            r.id as resource_id,
            r.resource_code,
            r.resource_name,
            r.resource_type,
            rb.plugin_code,
            rb.binding_scope,
            rb.config_version,
            rb.usage_key,
            coalesce(r.capabilities, '[]'::jsonb) as capabilities,
            coalesce(r.config_payload, '{}'::jsonb) || coalesce(rs.secret_payload, '{}'::jsonb) as config_payload
        from dib_release.resource_bindings rb
        join dib_release.resources r on r.id = rb.resource_id
        join dib_release.resource_plugins rp on rp.plugin_code = rb.plugin_code
        left join dib_release.resource_secrets rs on rs.resource_id = r.id
        where rb.site_row_id = v_site_row_id
          and rb.binding_scope = 'PluginAtSite'
          and rb.status = 'Active'
          and r.status = 'Active'
          and rp.status = 'Active'
          and (
              jsonb_array_length(coalesce(p_plugins_json, '[]'::jsonb)) = 0
              or exists (
                  select 1
                  from requested_plugins requested
                  where requested.plugin_code = rb.plugin_code
              )
          )
    )
    select coalesce(
        jsonb_agg(
            jsonb_build_object(
                'resourceId', authorized_rows.resource_id::text,
                'resourceCode', authorized_rows.resource_code,
                'resourceName', authorized_rows.resource_name,
                'resourceType', authorized_rows.resource_type,
                'pluginCode', authorized_rows.plugin_code,
                'bindingScope', authorized_rows.binding_scope,
                'configVersion', authorized_rows.config_version,
                'configPayload', authorized_rows.config_payload,
                'capabilities', authorized_rows.capabilities
            )
            order by authorized_rows.plugin_code, authorized_rows.resource_type, authorized_rows.resource_code, authorized_rows.id
        ),
        '[]'::jsonb
    )
    into v_resources
    from authorized_rows;

    return jsonb_build_object('resources', v_resources);
end;
$$;

grant execute on function dib_release.get_site_authorized_resources(text, text, text, jsonb) to anon, authenticated, service_role;
