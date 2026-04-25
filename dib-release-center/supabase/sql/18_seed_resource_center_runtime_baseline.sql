-- 18_seed_resource_center_runtime_baseline.sql
-- 资源中心最小运行时基线数据。

insert into dib_release.resource_plugins (
    plugin_code,
    plugin_name,
    status
)
values (
    'patient-registration',
    '就诊登记',
    'Active'
)
on conflict (plugin_code) do update
set plugin_name = excluded.plugin_name,
    status = excluded.status,
    updated_at = now();

insert into dib_release.resources (
    resource_code,
    resource_name,
    resource_type,
    owner_organization_name,
    visibility_scope,
    config_schema_version,
    config_payload,
    capabilities,
    status,
    description
)
values (
    'patient-registration-postgres-sample',
    '示例就诊登记 PostgreSQL',
    'PostgreSQL',
    '',
    'Private',
    1,
    jsonb_build_object(
        'host', '127.0.0.1',
        'port', 5432,
        'database', 'patient_registration',
        'username', 'patient_app',
        'searchPath', 'public',
        'sslMode', 'Prefer'
    ),
    '["read","write"]'::jsonb,
    'Active',
    '资源中心最小运行时闭环的示例资源，需按站点显式绑定后才会被客户端同步。'
)
on conflict (resource_code) do update
set resource_name = excluded.resource_name,
    resource_type = excluded.resource_type,
    owner_organization_name = excluded.owner_organization_name,
    visibility_scope = excluded.visibility_scope,
    config_schema_version = excluded.config_schema_version,
    config_payload = excluded.config_payload,
    capabilities = excluded.capabilities,
    status = excluded.status,
    description = excluded.description,
    updated_at = now();

insert into dib_release.resource_secrets (
    resource_id,
    secret_payload,
    secret_version,
    encryption_mode
)
select
    r.id,
    jsonb_build_object(
        'password', 'replace-with-real-password'
    ),
    1,
    'AppEncrypted'
from dib_release.resources r
where r.resource_code = 'patient-registration-postgres-sample'
on conflict (resource_id) do update
set secret_payload = excluded.secret_payload,
    secret_version = excluded.secret_version,
    encryption_mode = excluded.encryption_mode,
    updated_at = now();

-- 说明：
-- 当前不默认 seed 站点级绑定，避免在正式环境中把示例资源误授权给已有站点。
-- 本轮联调时，应在目标站点注册完成后手工插入一条 `resource_bindings` 记录：
-- - `plugin_code = 'patient-registration'`
-- - `usage_key = 'registration-db'`
-- - `binding_scope = 'PluginAtSite'`
-- - `status = 'Active'`

insert into dib_release.resource_plugins (
    plugin_code,
    plugin_name,
    status
)
values (
    'medical-drug-import',
    '医保药品导入',
    'Active'
)
on conflict (plugin_code) do update
set plugin_name = excluded.plugin_name,
    status = excluded.status,
    updated_at = now();
