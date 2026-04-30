-- 21_create_apply_site_resource_rpc.sql
-- 提交站点资源申请。

drop function if exists dib_release.apply_site_resource(text, text, text, text, text, text);

create or replace function dib_release.apply_site_resource(
    p_channel_code text,
    p_site_id text,
    p_client_version text default null,
    p_resource_id text default null,
    p_plugin_code text default null,
    p_reason text default null
)
returns jsonb
language plpgsql
security definer
set search_path = dib_release, public
as $$
declare
    v_site_row_id uuid;
    v_organization_id uuid;
    v_resource_uuid uuid;
    v_existing_application_id uuid;
    v_active_binding_exists boolean := false;
begin
    select s.id, s.organization_id
    into v_site_row_id, v_organization_id
    from dib_release.sites s
    where s.site_id = p_site_id
      and s.is_active = true
    limit 1;

    if v_site_row_id is null then
        return jsonb_build_object(
            'success', false,
            'message', '站点不存在或未激活',
            'applicationId', '',
            'status', ''
        );
    end if;

    if v_organization_id is null then
        return jsonb_build_object(
            'success', false,
            'message', '站点未绑定单位，无法申请资源',
            'applicationId', '',
            'status', ''
        );
    end if;

    if coalesce(p_plugin_code, '') = '' then
        return jsonb_build_object(
            'success', false,
            'message', '插件编码不能为空',
            'applicationId', '',
            'status', ''
        );
    end if;

    perform 1
    from dib_release.resource_plugins rp
    where rp.plugin_code = p_plugin_code
      and rp.status = 'Active';

    if not found then
        return jsonb_build_object(
            'success', false,
            'message', '插件未注册或已停用',
            'applicationId', '',
            'status', ''
        );
    end if;

    perform 1
    from dib_release.organization_plugin_permissions opp
    where opp.organization_id = v_organization_id
      and opp.plugin_code = p_plugin_code
      and opp.status = 'Active'
      and (opp.expires_at is null or opp.expires_at > now());

    if not found then
        return jsonb_build_object(
            'success', false,
            'message', '站点所属单位未获得插件授权',
            'applicationId', '',
            'status', ''
        );
    end if;

    begin
        v_resource_uuid := p_resource_id::uuid;
    exception
        when invalid_text_representation then
            return jsonb_build_object(
                'success', false,
                'message', '资源标识格式无效',
                'applicationId', '',
                'status', ''
            );
    end;

    perform 1
    from dib_release.resources r
    where r.id = v_resource_uuid
      and r.status = 'Active';

    if not found then
        return jsonb_build_object(
            'success', false,
            'message', '资源不存在或未激活',
            'applicationId', '',
            'status', ''
        );
    end if;

    select exists(
        select 1
        from dib_release.resource_bindings rb
        where rb.site_row_id = v_site_row_id
          and rb.plugin_code = p_plugin_code
          and rb.resource_id = v_resource_uuid
          and rb.status = 'Active'
    )
    into v_active_binding_exists;

    if v_active_binding_exists then
        return jsonb_build_object(
            'success', true,
            'message', '资源已授权',
            'applicationId', '',
            'status', 'Approved'
        );
    end if;

    select ra.id
    into v_existing_application_id
    from dib_release.resource_applications ra
    where ra.site_row_id = v_site_row_id
      and ra.plugin_code = p_plugin_code
      and ra.resource_id = v_resource_uuid
      and ra.status in ('Draft', 'Submitted', 'UnderReview')
    order by ra.created_at desc
    limit 1;

    if v_existing_application_id is not null then
        return jsonb_build_object(
            'success', true,
            'message', '资源申请已存在',
            'applicationId', v_existing_application_id::text,
            'status', 'Submitted'
        );
    end if;

    insert into dib_release.resource_applications (
        site_row_id,
        plugin_code,
        resource_id,
        reason,
        status
    )
    values (
        v_site_row_id,
        p_plugin_code,
        v_resource_uuid,
        coalesce(p_reason, ''),
        'Submitted'
    )
    returning id into v_existing_application_id;

    return jsonb_build_object(
        'success', true,
        'message', '资源申请已提交',
        'applicationId', v_existing_application_id::text,
        'status', 'Submitted'
    );
end;
$$;

grant execute on function dib_release.apply_site_resource(text, text, text, text, text, text) to anon, authenticated, service_role;
