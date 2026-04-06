-- 08_validate_release_center_schema.sql
-- 最小失败验证脚本：在建表后执行，任何约束不满足都会抛错。

begin;

do $$
declare
    v_stable_channel_id uuid;
    v_plugin_asset_id uuid;
    v_client_asset_id uuid;
    v_manifest_asset_id uuid;
    v_plugin_package_id uuid;
    v_plugin_version_id uuid;
    v_client_version_id uuid;
    v_site_group_id uuid;
    v_site_row_id uuid;
    v_heartbeat_id uuid;
    v_client_manifest jsonb;
    v_plugin_manifest jsonb;
    v_site_total_count integer;
    v_effective_enabled boolean;
    v_suffix text := replace(gen_random_uuid()::text, '-', '');
    v_plugin_code text;
    v_validation_version text;
begin
    v_plugin_code := 'validation-patient-registration-' || left(v_suffix, 8);
    v_validation_version := '1.0.' || (90000 + (abs(hashtext(v_suffix)) % 9999))::text;

    select id
    into v_stable_channel_id
    from dib_release.release_channels
    where channel_code = 'stable';

    if v_stable_channel_id is null then
        raise exception 'validation failed: stable channel missing';
    end if;

    if to_regclass('dib_release.site_groups') is null then
        raise exception 'validation failed: dib_release.site_groups missing';
    end if;

    if to_regclass('dib_release.sites') is null then
        raise exception 'validation failed: dib_release.sites missing';
    end if;

    if to_regclass('dib_release.group_plugin_policies') is null then
        raise exception 'validation failed: dib_release.group_plugin_policies missing';
    end if;

    if to_regclass('dib_release.site_plugin_overrides') is null then
        raise exception 'validation failed: dib_release.site_plugin_overrides missing';
    end if;

    if to_regclass('dib_release.site_heartbeats') is null then
        raise exception 'validation failed: dib_release.site_heartbeats missing';
    end if;

    if to_regclass('dib_release.site_overview') is null then
        raise exception 'validation failed: dib_release.site_overview missing';
    end if;

    if to_regclass('dib_release.site_group_statistics') is null then
        raise exception 'validation failed: dib_release.site_group_statistics missing';
    end if;

    if to_regclass('dib_release.site_effective_plugin_policies_view') is null then
        raise exception 'validation failed: dib_release.site_effective_plugin_policies_view missing';
    end if;

    if to_regprocedure('dib_release.register_site_heartbeat(text,text,text,text,text,jsonb,text)') is null then
        raise exception 'validation failed: register_site_heartbeat missing';
    end if;

    if to_regprocedure('dib_release.get_site_plugin_manifest(text,text,text)') is null then
        raise exception 'validation failed: get_site_plugin_manifest missing';
    end if;

    if not exists (
        select 1
        from pg_indexes
        where schemaname = 'dib_release'
          and indexname = 'ux_sites_site_id'
    ) then
        raise exception 'validation failed: ux_sites_site_id missing';
    end if;

    if not exists (
        select 1
        from pg_indexes
        where schemaname = 'dib_release'
          and indexname = 'idx_sites_group_id'
    ) then
        raise exception 'validation failed: idx_sites_group_id missing';
    end if;

    if not exists (
        select 1
        from pg_indexes
        where schemaname = 'dib_release'
          and indexname = 'idx_site_heartbeats_site_created_at'
    ) then
        raise exception 'validation failed: idx_site_heartbeats_site_created_at missing';
    end if;

    if not exists (
        select 1
        from pg_indexes
        where schemaname = 'dib_release'
          and indexname = 'ux_group_plugin_policies_group_package'
    ) then
        raise exception 'validation failed: ux_group_plugin_policies_group_package missing';
    end if;

    if not exists (
        select 1
        from pg_indexes
        where schemaname = 'dib_release'
          and indexname = 'ux_site_plugin_overrides_site_package'
    ) then
        raise exception 'validation failed: ux_site_plugin_overrides_site_package missing';
    end if;

    insert into dib_release.release_assets (
        bucket_name,
        storage_path,
        file_name,
        asset_kind,
        sha256,
        size_bytes,
        mime_type
    )
    values (
        'dib-releases',
        'plugins/' || v_plugin_code || '/stable/' || v_validation_version || '/' || v_plugin_code || '-' || v_validation_version || '.zip',
        v_plugin_code || '-' || v_validation_version || '.zip',
        'plugin_package',
        repeat('1', 64),
        1024,
        'application/zip'
    )
    returning id into v_plugin_asset_id;

    insert into dib_release.release_assets (
        bucket_name,
        storage_path,
        file_name,
        asset_kind,
        sha256,
        size_bytes,
        mime_type
    )
    values (
        'dib-releases',
        'clients/stable/' || v_validation_version || '/dib-win-x64-portable-validation-' || left(v_suffix, 8) || '.zip',
        'dib-win-x64-portable-validation-' || left(v_suffix, 8) || '.zip',
        'client_package',
        repeat('2', 64),
        2048,
        'application/zip'
    )
    returning id into v_client_asset_id;

    insert into dib_release.release_assets (
        bucket_name,
        storage_path,
        file_name,
        asset_kind,
        sha256,
        size_bytes,
        mime_type
    )
    values (
        'dib-releases',
        'manifests/stable/client-manifest-validation-' || left(v_suffix, 8) || '.json',
        'client-manifest-validation-' || left(v_suffix, 8) || '.json',
        'manifest',
        repeat('3', 64),
        256,
        'application/json'
    )
    returning id into v_manifest_asset_id;

    insert into dib_release.plugin_packages (
        plugin_code,
        plugin_name,
        entry_type,
        author,
        description
    )
    values (
        v_plugin_code,
        '就诊登记验证插件',
        'module',
        'dib-team',
        '验证用插件'
    )
    returning id into v_plugin_package_id;

    insert into dib_release.site_groups (
        group_code,
        group_name,
        description
    )
    values (
        'validation-group-' || left(v_suffix, 8),
        '验证分组',
        '验证站点分组'
    )
    returning id into v_site_group_id;

    insert into dib_release.sites (
        site_id,
        site_name,
        group_id,
        channel_id,
        client_version,
        machine_name,
        last_seen_at,
        last_update_check_at,
        installed_plugins_json
    )
    values (
        gen_random_uuid()::text,
        '验证站点',
        v_site_group_id,
        v_stable_channel_id,
        '1.0.0',
        'validation-machine',
        now(),
        now(),
        jsonb_build_array(v_plugin_code)
    )
    returning id into v_site_row_id;

    insert into dib_release.plugin_versions (
        package_id,
        channel_id,
        asset_id,
        version,
        dib_min_version,
        dib_max_version,
        release_notes,
        manifest_json,
        is_published,
        is_mandatory,
        published_at
    )
    values (
        v_plugin_package_id,
        v_stable_channel_id,
        v_plugin_asset_id,
        v_validation_version,
        '1.0.0',
        '1.9.99',
        '初始插件发布',
        jsonb_build_object('entry', 'PatientRegistration.Plugin'),
        true,
        false,
        now()
    )
    returning id into v_plugin_version_id;

    insert into dib_release.client_versions (
        channel_id,
        asset_id,
        version,
        min_upgrade_version,
        is_published,
        is_mandatory,
        release_notes,
        published_at
    )
    values (
        v_stable_channel_id,
        v_client_asset_id,
        v_validation_version,
        '0.9.0',
        true,
        false,
        '初始客户端发布',
        now()
    )
    returning id into v_client_version_id;

    insert into dib_release.group_plugin_policies (
        group_id,
        package_id,
        is_enabled,
        min_client_version,
        max_client_version
    )
    values (
        v_site_group_id,
        v_plugin_package_id,
        true,
        '1.0.0',
        '1.9.99'
    );

    insert into dib_release.site_plugin_overrides (
        site_id,
        package_id,
        action,
        reason
    )
    values (
        v_site_row_id,
        v_plugin_package_id,
        'deny',
        '验证覆盖'
    );

    insert into dib_release.site_heartbeats (
        site_id,
        channel_id,
        client_version,
        installed_plugins_json,
        event_type
    )
    values (
        v_site_row_id,
        v_stable_channel_id,
        '1.0.0',
        jsonb_build_array(v_plugin_code),
        'update_check'
    )
    returning id into v_heartbeat_id;

    begin
        insert into dib_release.plugin_versions (
            package_id,
            channel_id,
            asset_id,
            version,
            is_published
        )
        values (
            v_plugin_package_id,
            v_stable_channel_id,
            v_client_asset_id,
            '1.0.1',
            false
        );
        raise exception 'validation failed: plugin_versions accepted client_package asset';
    exception
        when others then
            if position('plugin_versions.asset_id must reference a plugin_package asset' in sqlerrm) = 0 then
                raise;
            end if;
    end;

    begin
        insert into dib_release.client_versions (
            channel_id,
            asset_id,
            version,
            is_published
        )
        values (
            v_stable_channel_id,
            v_plugin_asset_id,
            '1.0.1',
            false
        );
        raise exception 'validation failed: client_versions accepted plugin_package asset';
    exception
        when others then
            if position('client_versions.asset_id must reference a client_package asset' in sqlerrm) = 0 then
                raise;
            end if;
    end;

    select client_manifest, plugin_manifest
    into v_client_manifest, v_plugin_manifest
    from dib_release.release_manifest_view
    where channel_code = 'stable';

    if v_client_manifest ->> 'channel' <> 'stable' then
        raise exception 'validation failed: client_manifest.channel mismatch';
    end if;

    if v_client_manifest ->> 'latestVersion' <> v_validation_version then
        raise exception 'validation failed: client_manifest.latestVersion mismatch';
    end if;

    if v_plugin_manifest ->> 'channel' <> 'stable' then
        raise exception 'validation failed: plugin_manifest.channel mismatch';
    end if;

    if jsonb_array_length(coalesce(v_plugin_manifest -> 'plugins', '[]'::jsonb)) < 1 then
        raise exception 'validation failed: plugin_manifest.plugins missing';
    end if;

    if not exists (
        select 1
        from jsonb_array_elements(v_plugin_manifest -> 'plugins') plugin_item
        where plugin_item ->> 'pluginId' = v_plugin_code
    ) then
        raise exception 'validation failed: plugin_manifest.pluginId mismatch';
    end if;

    if v_manifest_asset_id is null then
        raise exception 'validation failed: manifest asset insert missing';
    end if;

    select count(*)
    into v_site_total_count
    from dib_release.site_overview
    where id = v_site_row_id;

    if v_site_total_count <> 1 then
        raise exception 'validation failed: site_overview missing inserted site';
    end if;

    if not exists (
        select 1
        from dib_release.site_group_statistics
        where group_id = v_site_group_id
          and site_count >= 1
    ) then
        raise exception 'validation failed: site_group_statistics mismatch';
    end if;

    select effective_is_enabled
    into v_effective_enabled
    from dib_release.site_effective_plugin_policies_view
    where site_row_id = v_site_row_id
      and plugin_code = v_plugin_code;

    if v_effective_enabled is distinct from false then
        raise exception 'validation failed: site override did not disable plugin';
    end if;

    if v_heartbeat_id is null then
        raise exception 'validation failed: site heartbeat insert missing';
    end if;

    perform dib_release.register_site_heartbeat(
        gen_random_uuid()::text,
        'RPC 验证站点',
        'stable',
        '1.0.0',
        'validation-rpc-machine',
        jsonb_build_array(v_plugin_code),
        'update_check'
    );

    select jsonb_array_length(coalesce((dib_release.get_site_plugin_manifest('stable', (
        select site_id from dib_release.sites where id = v_site_row_id
    ), '1.0.0') -> 'plugins'), '[]'::jsonb))
    into v_site_total_count;

    if v_site_total_count <> 0 then
        raise exception 'validation failed: site manifest should be empty after deny override';
    end if;

    delete from dib_release.site_heartbeats where id = v_heartbeat_id;
    delete from dib_release.site_plugin_overrides where site_id = v_site_row_id;
    delete from dib_release.group_plugin_policies
    where group_id = v_site_group_id
      and package_id = v_plugin_package_id;
    delete from dib_release.client_versions where id = v_client_version_id;
    delete from dib_release.plugin_versions where id = v_plugin_version_id;
    delete from dib_release.sites where id = v_site_row_id;
    delete from dib_release.site_groups where id = v_site_group_id;
    delete from dib_release.plugin_packages where id = v_plugin_package_id;
    delete from dib_release.release_assets
    where id in (v_plugin_asset_id, v_client_asset_id, v_manifest_asset_id);
end;
$$;

rollback;

