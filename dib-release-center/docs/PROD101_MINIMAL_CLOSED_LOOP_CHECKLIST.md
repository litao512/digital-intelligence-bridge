# prod101 最小闭环执行清单（托盘重注册 + 使用单位联动申请）

## 1. 目标

在 `prod101` 完成 `patient-registration / registration-db` 的资源更新与客户端回归，验证：

1. 托盘可重新注册站点（含“使用单位”）。
2. 资源申请请求会携带站点信息（`使用单位 / 站点名称`）进入 `p_reason`。
3. 客户端可同步并应用最新 `registration-db` 配置。

## 2. 前置检查

1. 客户端版本已包含本次改动（`ReleaseCenterService.ApplyResourceAsync` 已拼接站点信息）。
2. 客户端配置已启用发布中心：
   - `ReleaseCenter.Enabled = true`
   - `ReleaseCenter.BaseUrl` 正确
   - `ReleaseCenter.Channel` 正确
   - `ReleaseCenter.AnonKey` 正确
3. 目标站点 `SiteId` 与生产一致：`3eb54a19-2897-423b-8098-539a373dcce2`。

## 3. 生产更新（沿用 Runbook）

执行以下 SQL（与 `PROD101_REGISTRATION_DB_UPDATE_RUNBOOK.md` 一致）：

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

## 4. 数据库校验

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

1. `connection_string` 为本次更新值。
2. `config_version` 已递增。

## 5. 客户端回归

1. 通过托盘执行“重新注册站点”，确认填写并保存“使用单位 + 站点名称”。
2. 在资源中心对目标资源发起申请（若需）。
3. 检查日志文件：
   - `C:\Users\Administrator\AppData\Local\DibClient\logs\app-YYYYMMDD.log`
4. 检查授权缓存：
   - `C:\Users\Administrator\AppData\Local\DibClient\resource-cache\authorized-resources.json`
5. 打开 `PatientRegistration` 页面验证可连库。

预期：

1. 申请请求的 `p_reason` 包含前缀：`站点信息：<使用单位 / 站点名称>`。
2. 缓存中存在 `patient-registration / registration-db` 最新配置。
3. 日志出现“已应用宿主下发的 registration-db 资源配置”。

## 6. 失败回滚

1. 将 `connectionString` 回滚到上一个可用值。
2. 再次递增 `resource_bindings.config_version`。
3. 重启客户端，重复第 5 节验证。
