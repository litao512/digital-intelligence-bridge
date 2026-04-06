# prod101 SQL 执行清单

## 1. 目的

本文档用于说明 `dib-release-center/supabase/sql/` 在 `prod101` 上的执行顺序、用途与增量变更边界。

## 2. 执行顺序

首次落库建议按以下顺序执行：

1. `01_create_release_channels.sql`
2. `02_create_release_assets.sql`
3. `03_create_plugin_packages.sql`
4. `04_create_plugin_versions.sql`
5. `05_create_client_versions.sql`
6. `06_seed_release_channels.sql`
7. `07_create_release_center_views.sql`
8. `09_create_release_center_admins_and_policies.sql`
9. `10_create_site_groups.sql`
10. `11_create_sites.sql`
11. `12_create_group_plugin_policies.sql`
12. `13_create_site_plugin_overrides.sql`
13. `14_create_site_heartbeats.sql`
14. `15_create_site_runtime_rpcs.sql`
15. `07_create_release_center_views.sql`
16. `09_create_release_center_admins_and_policies.sql`
17. `08_validate_release_center_schema.sql`

## 3. 文件作用

### `01_create_release_channels.sql`

创建发布渠道表，包含：

- `stable`
- `beta`
- `internal`

### `02_create_release_assets.sql`

创建统一资产表，管理：

- 插件包
- 客户端包
- manifest 文件

### `03_create_plugin_packages.sql`

创建插件定义表，不存版本，仅存插件基础标识与描述。

### `04_create_plugin_versions.sql`

创建插件版本表，记录：

- 版本号
- 渠道
- 关联资产
- 兼容的 DIB 版本区间

### `05_create_client_versions.sql`

创建客户端版本表，记录：

- 版本号
- 渠道
- 关联资产
- 是否强制升级

### `06_seed_release_channels.sql`

初始化默认渠道数据。

### `07_create_release_center_views.sql`

创建前端读取用视图与 manifest 聚合视图。二次执行时会补入站点相关视图：

- `site_overview`
- `site_group_statistics`
- `site_effective_plugin_policies_view`

### `08_validate_release_center_schema.sql`

执行事务内校验，确保：

- 关键表存在
- 关键视图可查询
- 基线约束成立
- 站点授权表、索引和统计视图可用

### `09_create_release_center_admins_and_policies.sql`

创建：

- 管理员表
- RLS 策略
- Storage 写权限策略
- bucket 初始化逻辑
- 站点相关表的安全策略与 `security_invoker` 视图

### `10_create_site_groups.sql`

创建站点分组表，并初始化默认分组 `unassigned`。

### `11_create_sites.sql`

创建 DIB 安装实例站点表，持久化：

- `site_id`
- `site_name`
- 当前客户端版本
- 最近活动时间

### `12_create_group_plugin_policies.sql`

创建组级插件授权表。

### `13_create_site_plugin_overrides.sql`

创建站点级插件覆盖表。

### `14_create_site_heartbeats.sql`

创建站点心跳记录表。

### `15_create_site_runtime_rpcs.sql`

创建运行时 RPC：

- `dib_release.register_site_heartbeat(...)`
- `dib_release.get_site_plugin_manifest(...)`
- `dib_release.semver_cmp(...)`

其中：

- `register_site_heartbeat` 用于 DIB 客户端检查更新、下载插件、下载客户端包时写回站点活动
- `get_site_plugin_manifest` 用于按 `site_id + channel + client_version` 返回授权后的插件清单
- `site_id` 输入值必须为 GUID
- 通过 PostgREST 调用时需要使用 `dib_release` profile 头

## 4. 增量变更原则

后续新增 SQL 时遵循：

1. 只追加，不改号
2. 新文件按 `15_...sql`、`16_...sql` 顺延
3. 已在生产执行过的 SQL 不做原地重写
4. 结构变更通过新增 migration 表达

## 5. 首次管理员初始化

管理员初始化建议分两步：

1. 插入 `dib_release.release_center_admins`
2. 创建 Supabase Auth 用户并绑定 `user_id`

示例查询：

```sql
select id, email, display_name, is_active, user_id
from dib_release.release_center_admins
order by created_at desc;
```

## 6. 验收 SQL

### 6.1 查看 schema 对象

```sql
select table_name
from information_schema.tables
where table_schema = 'dib_release'
order by table_name;
```

建议至少确认包含：

- `site_groups`
- `sites`
- `group_plugin_policies`
- `site_plugin_overrides`
- `site_heartbeats`
- `register_site_heartbeat`
- `get_site_plugin_manifest`

### 6.2 查看渠道

```sql
select channel_code, channel_name, sort_order, is_default, is_active
from dib_release.release_channels
order by sort_order;
```

### 6.3 查看资产

```sql
select asset_kind, file_name, storage_path, size_bytes
from dib_release.release_assets
order by created_at desc;
```

### 6.4 查看站点概览

```sql
select site_name, site_id, group_name, client_version, last_seen_at
from dib_release.site_overview
order by updated_at desc;
```

### 6.5 查看站点统计

```sql
select group_code, group_name, site_count, active_site_count_24h
from dib_release.site_group_statistics
order by group_code;
```

### 6.6 查看运行时 RPC

```sql
select to_regprocedure('dib_release.register_site_heartbeat(text,text,text,text,text,jsonb,text)');
select to_regprocedure('dib_release.get_site_plugin_manifest(text,text,text)');
```

## 7. 注意事项

- 正式库对象统一落在 `dib_release`，不要混到 `public`
- 所有 manifest 读取与资产上传都依赖 `dib-releases` bucket
- 如果 `PostgREST` 没暴露 `dib_release`，前端登录与数据读取会失败
- 站点相关页面依赖 `site_*` 表和 3 个站点视图，迁移不完整时会直接影响站点管理与统计页面
- 新增 RPC migration 后，如果 HTTP 访问仍提示函数不存在，需要重启 `supabase-rest` 刷新 schema cache
