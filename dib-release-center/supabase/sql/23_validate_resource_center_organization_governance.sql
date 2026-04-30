-- 23_validate_resource_center_organization_governance.sql
-- 验证资源中心单位授权与站点绑定的运行时约束。

begin;

do $$
declare
    v_org_id uuid;
    v_site_id text := gen_random_uuid()::text;
    v_site_row_id uuid;
    v_plugin_code text := 'validation-resource-plugin-' || left(replace(gen_random_uuid()::text, '-', ''), 8);
    v_resource_id uuid;
    v_result jsonb;
begin
    insert into dib_release.organizations (
        code,
        name,
        organization_type
    )
    values (
        'validation-org-' || left(replace(gen_random_uuid()::text, '-', ''), 8),
        '资源治理验证单位',
        'Hospital'
    )
    returning id into v_org_id;

    insert into dib_release.sites (
        site_id,
        site_name,
        organization_id,
        client_version,
        machine_name,
        installed_plugins_json
    )
    values (
        v_site_id,
        '资源治理验证站点',
        v_org_id,
        '1.0.0',
        'validation-resource-machine',
        jsonb_build_array(v_plugin_code)
    )
    returning id into v_site_row_id;

    insert into dib_release.resource_plugins (
        plugin_code,
        plugin_name,
        status
    )
    values (
        v_plugin_code,
        '资源治理验证插件',
        'Active'
    );

    insert into dib_release.resources (
        resource_code,
        resource_name,
        resource_type,
        owner_organization_id,
        status
    )
    values (
        'validation-resource-' || left(replace(gen_random_uuid()::text, '-', ''), 8),
        '资源治理验证数据库',
        'PostgreSQL',
        v_org_id,
        'Active'
    )
    returning id into v_resource_id;

    insert into dib_release.resource_bindings (
        site_row_id,
        plugin_code,
        resource_id,
        usage_key,
        status
    )
    values (
        v_site_row_id,
        v_plugin_code,
        v_resource_id,
        'registration-db',
        'Active'
    );

    select dib_release.get_site_authorized_resources(
        'stable',
        v_site_id,
        '1.0.0',
        jsonb_build_array(jsonb_build_object('pluginCode', v_plugin_code))
    )
    into v_result;

    if jsonb_array_length(v_result -> 'resources') <> 0 then
        raise exception 'validation failed: resources returned without organization permissions';
    end if;

    insert into dib_release.organization_plugin_permissions (
        organization_id,
        plugin_code,
        status
    )
    values (
        v_org_id,
        v_plugin_code,
        'Active'
    );

    select dib_release.get_site_authorized_resources(
        'stable',
        v_site_id,
        '1.0.0',
        jsonb_build_array(jsonb_build_object('pluginCode', v_plugin_code))
    )
    into v_result;

    if jsonb_array_length(v_result -> 'resources') <> 0 then
        raise exception 'validation failed: resources returned without organization resource permission';
    end if;

    insert into dib_release.organization_resource_permissions (
        organization_id,
        resource_id,
        status
    )
    values (
        v_org_id,
        v_resource_id,
        'Active'
    );

    select dib_release.get_site_authorized_resources(
        'stable',
        v_site_id,
        '1.0.0',
        jsonb_build_array(jsonb_build_object('pluginCode', v_plugin_code))
    )
    into v_result;

    if jsonb_array_length(v_result -> 'resources') <> 1 then
        raise exception 'validation failed: organization permissions and site binding should return one resource';
    end if;

    update dib_release.sites
    set organization_id = null
    where id = v_site_row_id;

    select dib_release.get_site_authorized_resources(
        'stable',
        v_site_id,
        '1.0.0',
        jsonb_build_array(jsonb_build_object('pluginCode', v_plugin_code))
    )
    into v_result;

    if jsonb_array_length(v_result -> 'resources') <> 0 then
        raise exception 'validation failed: resources returned for site without organization';
    end if;
end;
$$;

rollback;
