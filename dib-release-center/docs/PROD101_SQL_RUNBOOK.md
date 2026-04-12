# prod101 SQL 执行清单

## 1. 目的

本文档用于说明 `dib-release-center/supabase/sql/` 在 `prod101` 上的执行顺序、用途与增量变更边界。

## 2. 执行顺序

首次落库必须按以下顺序执行：

1. `01_create_release_channels.sql`
2. `02_create_release_assets.sql`
3. `03_create_plugin_packages.sql`
4. `04_create_plugin_versions.sql`
5. `05_create_client_versions.sql`
6. `06_seed_release_channels.sql`
7. `07_create_release_center_views.sql`
8. `08_validate_release_center_schema.sql`
9. `09_create_release_center_admins_and_policies.sql`

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

创建前端读取用视图与 manifest 聚合视图。

### `08_validate_release_center_schema.sql`

执行事务内校验，确保：

- 关键表存在
- 关键视图可查询
- 基线约束成立

### `09_create_release_center_admins_and_policies.sql`

创建：

- 管理员表
- RLS 策略
- Storage 写权限策略
- bucket 初始化逻辑

## 4. 增量变更原则

后续新增 SQL 时遵循：

1. 只追加，不改号
2. 新文件按 `10_...sql`、`11_...sql` 顺延
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

### 6.2 查看渠道

```sql
select code, name, sort_order, is_default, is_active
from dib_release.release_channels
order by sort_order;
```

### 6.3 查看资产

```sql
select asset_kind, file_name, storage_path, size_bytes
from dib_release.release_assets
order by created_at desc;
```

## 7. 注意事项

- 正式库对象统一落在 `dib_release`，不要混到 `public`
- 所有 manifest 读取与资产上传都依赖 `dib-releases` bucket
- 如果 `PostgREST` 没暴露 `dib_release`，前端登录与数据读取会失败
