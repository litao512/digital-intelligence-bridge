# prod101 registration-db 资源更新 Runbook

## 1. 适用范围

本文档用于在 `prod101` 上更新 `patient-registration` 插件的 `usage_key = registration-db` 资源配置，并完成最小回归验证。

- 服务器：`101.42.19.26`
- SSH：`ssh prod-101`
- 数据库容器：`supabase-db`
- schema：`dib_release`
- 目标站点：`3eb54a19-2897-423b-8098-539a373dcce2`

## 2. 前置确认

执行前先确认：

1. 客户端已启用 `ReleaseCenter.Enabled = true`
2. 站点 `SiteId` 正确
3. 已存在绑定：
   - `plugin_code = patient-registration`
   - `usage_key = registration-db`
   - `binding_scope = PluginAtSite`
   - `status = Active`

## 3. 更新 SQL 模板

说明：

- 优先写入 `resources.config_payload.connectionString`
- `resource_secrets` 可置空对象 `{}`（当前连接串已包含敏感信息）
- 递增绑定 `config_version`，确保客户端触发配置刷新

执行方式（PowerShell here-string）：

```powershell
@'
begin;

update dib_release.resources
set config_payload = jsonb_build_object(
    'connectionString',
    '<替换为真实 PostgreSQL 连接串>'
),
updated_at = now()
where resource_code = 'patient-registration-postgres-sample';

update dib_release.resource_secrets rs
set secret_payload = '{}'::jsonb,
    secret_version = coalesce(rs.secret_version, 0) + 1,
    updated_at = now()
from dib_release.resources r
where rs.resource_id = r.id
  and r.resource_code = 'patient-registration-postgres-sample';

update dib_release.resource_bindings rb
set config_version = coalesce(rb.config_version, 0) + 1,
    updated_at = now()
from dib_release.sites s
where rb.site_row_id = s.id
  and s.site_id = '3eb54a19-2897-423b-8098-539a373dcce2'
  and rb.plugin_code = 'patient-registration'
  and rb.usage_key = 'registration-db'
  and rb.status = 'Active';

commit;
'@ | ssh prod-101 "docker exec -i supabase-db psql -v ON_ERROR_STOP=1 -U postgres -d postgres -At"
```

## 4. 数据库校验 SQL

```powershell
@'
select
  r.resource_code,
  (r.config_payload ->> 'connectionString') as connection_string,
  rb.config_version,
  rb.usage_key,
  s.site_id
from dib_release.resources r
join dib_release.resource_bindings rb on rb.resource_id = r.id
join dib_release.sites s on s.id = rb.site_row_id
where r.resource_code = 'patient-registration-postgres-sample'
  and s.site_id = '3eb54a19-2897-423b-8098-539a373dcce2'
  and rb.plugin_code = 'patient-registration'
  and rb.usage_key = 'registration-db';
'@ | ssh prod-101 "docker exec -i supabase-db psql -U postgres -d postgres -At"
```

预期：

1. `connection_string` 为最新值
2. `config_version` 比更新前大 1

## 5. 客户端回归步骤

1. 重启客户端进程（建议完全退出后再启动）
2. 检查授权缓存：
   - `C:\Users\Administrator\AppData\Local\DibClient\resource-cache\authorized-resources.json`
3. 检查日志：
   - `C:\Users\Administrator\AppData\Local\DibClient\logs\app-YYYYMMDD.log`
4. 打开 `PatientRegistration` 页面

预期：

1. 缓存中 `patient-registration / registration-db` 已更新
2. 日志出现：`已应用宿主下发的 registration-db 资源配置`
3. 功能端到端可连库

## 6. 回滚方案

当新连接不可用时，按以下最小回滚：

1. 把 `connectionString` 改回上一版本可用值
2. 再次递增 `resource_bindings.config_version`
3. 重启客户端并重复第 5 节验证

## 7. 风险与注意事项

1. 不要重跑 `18_seed_resource_center_runtime_baseline.sql` 覆盖生产真实配置
2. 不要把连接密码写入仓库代码文件
3. 若业务要求分离敏感字段，可改为：
   - `config_payload` 放 `host/port/database/username`
   - `resource_secrets` 放 `password`
