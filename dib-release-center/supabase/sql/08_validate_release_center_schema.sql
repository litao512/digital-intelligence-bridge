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
    v_client_manifest jsonb;
    v_plugin_manifest jsonb;
    v_suffix text := replace(gen_random_uuid()::text, '-', '');
    v_plugin_code text;
begin
    v_plugin_code := 'validation-patient-registration-' || left(v_suffix, 8);

    select id
    into v_stable_channel_id
    from dib_release.release_channels
    where channel_code = 'stable';

    if v_stable_channel_id is null then
        raise exception 'validation failed: stable channel missing';
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
        'plugins/' || v_plugin_code || '/stable/1.0.0/' || v_plugin_code || '-1.0.0.zip',
        v_plugin_code || '-1.0.0.zip',
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
        'clients/stable/1.0.0/dib-win-x64-portable-validation-' || left(v_suffix, 8) || '.zip',
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
        '1.0.0',
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
        '1.0.0',
        '0.9.0',
        true,
        false,
        '初始客户端发布',
        now()
    )
    returning id into v_client_version_id;

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

    if v_client_manifest ->> 'latestVersion' <> '1.0.0' then
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

    delete from dib_release.client_versions where id = v_client_version_id;
    delete from dib_release.plugin_versions where id = v_plugin_version_id;
    delete from dib_release.plugin_packages where id = v_plugin_package_id;
    delete from dib_release.release_assets
    where id in (v_plugin_asset_id, v_client_asset_id, v_manifest_asset_id);
end;
$$;

rollback;

