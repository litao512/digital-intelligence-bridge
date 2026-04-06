# prod101 发布中心运维手册

## 1. 适用范围

本文档用于 `prod101` 上 DIB 发布中心的部署、验证、排障与日常操作。当前发布中心依赖以下基础设施：

- 服务器：`101.42.19.26`
- SSH 入口：`ssh prod-101`
- Supabase 根目录：`/data/supabase`
- 发布中心静态目录：`/data/dib-release-center`
- 发布中心访问入口：`http://101.42.19.26:8000/release-center/`
- 发布中心 schema：`dib_release`
- 发布资产 bucket：`dib-releases`
- 站点授权基础表：`site_groups / sites / group_plugin_policies / site_plugin_overrides / site_heartbeats`

## 2. 关键组件

发布中心依赖的 Supabase 容器如下：

- `supabase-db`
- `supabase-rest`
- `supabase-storage`
- `supabase-auth`
- `supabase-kong`

发布中心自身当前使用独立静态容器：

- `dib-release-center-web`

日常检查时，优先确认这 6 个容器是否正常运行。

## 3. 关键配置基线

### 3.1 PostgREST

`supabase-rest` 必须包含：

```text
PGRST_DB_SCHEMAS=public,storage,graphql_public,dib_release
```

如果缺少 `dib_release`，前端登录管理员账号后会报：

```text
Invalid schema: dib_release
```

### 3.2 Storage

`supabase-storage` 必须使用本地文件存储：

```text
STORAGE_BACKEND=file
FILE_STORAGE_BACKEND_PATH=/var/lib/storage
FILE_SIZE_LIMIT=268435456
```

当前 `FILE_SIZE_LIMIT` 已提高到 `256 MB`，用于统一承载：

- 插件包
- DIB 客户端便携包
- manifest 文件

如果错误落回旧的 `s3/minio` 配置，manifest 发布会失败，日志通常会出现：

```text
getaddrinfo ENOTFOUND minio
```

如果 `FILE_SIZE_LIMIT` 回退到 `52428800`（50 MB），客户端便携包上传会返回：

```text
413 Payload Too Large
```

### 3.3 公开资产

当前 manifest 通过公开地址供 DIB 客户端读取：

- `http://101.42.19.26:8000/storage/v1/object/public/dib-releases/manifests/stable/client-manifest.json`
- `http://101.42.19.26:8000/storage/v1/object/public/dib-releases/manifests/stable/plugin-manifest.json`

当前发布中心页面入口：

- `http://101.42.19.26:8000/release-center/`

正式环境建议切换为 `HTTPS`。

## 4. 首次落库步骤

### 4.1 执行 SQL

在 `dib-release-center/supabase/sql/` 中按顺序执行：

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
14. `07_create_release_center_views.sql`
15. `09_create_release_center_admins_and_policies.sql`
16. `08_validate_release_center_schema.sql`

### 4.2 初始化管理员

至少插入一条 `dib_release.release_center_admins` 记录，字段建议：

- `email`
- `display_name`
- `is_active = true`

首次登录后，再将 Supabase Auth 用户绑定到 `release_center_admins.user_id`。

### 4.3 首次验收

完成落库后，至少执行一次完整验收：

1. 管理员登录发布中心
2. 成功读取 `发布渠道`
3. 点击 `发布当前渠道 manifest`
4. 在 `发布资产` 页面看到 2 条 `manifest` 记录
5. 直接访问公开 manifest 地址返回 `200`
6. 打开“站点管理”和“站点统计”页确认能正常读取数据

## 5. 日常发布流程

### 5.1 发布插件版本

1. 上传插件包到 `dib-releases` bucket
2. 在 `发布资产` 页面登记对应 `release_assets`
3. 在 `插件版本` 页面录入版本记录
4. 发布对应渠道 manifest
5. 验证 `plugin-manifest.json` 是否包含新版本

### 5.2 发布客户端版本

1. 上传客户端包到 `dib-releases` bucket
2. 登记 `release_assets`
3. 在 `客户端版本` 页面录入版本记录
4. 发布对应渠道 manifest
5. 验证 `client-manifest.json` 是否带出 `latestVersion`

当前参考客户端资产：

- 版本：`1.0.1`
- 路径：`clients/stable/1.0.1/dib-win-x64-portable-1.0.1.zip`

### 5.3 发布中心页面部署

当前部署方式：

- 构建产物目录：`/data/dib-release-center/dist`
- nginx 配置：`/data/dib-release-center/nginx.conf`
- compose 文件：`/data/dib-release-center/docker-compose.yml`
- Kong 路由入口：`/release-center/`

如果页面静态资源返回 `403`，优先检查目录权限：

```bash
chmod -R a+rX /data/dib-release-center/dist
find /data/dib-release-center/dist -type d -exec chmod 755 {} +
```

## 6. 站点授权与统计运维

### 6.1 站点接入基线

当前 DIB 客户端会在本地配置中持久化：

- `ReleaseCenter.SiteId`
- `ReleaseCenter.SiteName`

并在检查更新时把 `siteId` 带到 `plugin-manifest` 请求中。

### 6.2 站点表检查

```bash
docker exec -i supabase-db psql -U postgres -d postgres -c "select site_name, site_id, client_version, last_seen_at from dib_release.site_overview order by updated_at desc limit 20;"
```

### 6.3 分组授权检查

```bash
docker exec -i supabase-db psql -U postgres -d postgres -c "select group_id, package_id, is_enabled, min_client_version, max_client_version from dib_release.group_plugin_policies order by created_at desc limit 20;"
```

### 6.4 站点覆盖检查

```bash
docker exec -i supabase-db psql -U postgres -d postgres -c "select site_id, package_id, action, is_active from dib_release.site_plugin_overrides order by created_at desc limit 20;"
```

### 6.5 站点统计检查

```bash
docker exec -i supabase-db psql -U postgres -d postgres -c "select group_code, group_name, site_count, active_site_count_24h from dib_release.site_group_statistics order by group_code;"
```

## 7. 常用检查项

### 7.1 检查容器

```bash
ssh prod-101
cd /data/supabase
docker ps --format '{{.Names}}|{{.Status}}' | grep supabase
docker ps --format '{{.Names}}|{{.Status}}' | grep dib-release-center-web
```

### 7.2 检查 REST schema

```bash
docker inspect supabase-rest --format '{{range .Config.Env}}{{println .}}{{end}}' | grep PGRST_DB_SCHEMAS
```

### 7.3 检查 Storage 后端与文件上限

```bash
docker inspect supabase-storage --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -E 'STORAGE_BACKEND|FILE_SIZE_LIMIT'
```

预期输出包含：

```text
STORAGE_BACKEND=file
FILE_SIZE_LIMIT=268435456
```

### 7.4 检查 bucket

```bash
docker exec -i supabase-db psql -U postgres -d postgres -c "select id, public from storage.buckets where id = 'dib-releases';"
```

### 7.5 检查管理员

```bash
docker exec -i supabase-db psql -U postgres -d postgres -c "select email, is_active, user_id from dib_release.release_center_admins order by created_at desc;"
```

### 7.6 检查发布中心入口

```bash
curl -I http://101.42.19.26:8000/release-center/
curl -I http://101.42.19.26:8000/release-center/assets/index-DQLm775K.js
```

## 8. 常见故障

### 8.1 登录后提示 `Invalid schema: dib_release`

根因：

- `supabase-rest` 未暴露 `dib_release`

处理：

1. 检查 `/data/supabase/.env`
2. 确认 `PGRST_DB_SCHEMAS` 包含 `dib_release`
3. 重启 `supabase-rest`

### 8.2 发布 manifest 返回 500

根因优先排查：

1. `supabase-storage` 是否误用 `s3/minio`
2. `dib-releases` bucket 是否存在
3. 当前管理员是否具备写入权限

重点命令：

```bash
docker logs supabase-storage --tail 100
docker inspect supabase-storage --format '{{range .Config.Env}}{{println .}}{{end}}'
```

### 8.3 客户端包上传返回 `413`

优先排查：

1. `FILE_SIZE_LIMIT` 是否回退到 `52428800`
2. `supabase-storage` 是否已重建并吃到新配置

处理：

```bash
cd /data/supabase
docker compose up -d --force-recreate storage
docker inspect supabase-storage --format '{{range .Config.Env}}{{println .}}{{end}}' | grep FILE_SIZE_LIMIT
```

### 8.4 发布中心首页可访问但静态资源返回 `403`

根因：

- `dist` 目录或 `assets` 目录权限过窄，nginx 无法读取

处理：

```bash
chmod -R a+rX /data/dib-release-center/dist
find /data/dib-release-center/dist -type d -exec chmod 755 {} +
docker restart dib-release-center-web
```

## 9. 建议的例行检查

每次变更 Supabase 配置或发布中心构建产物后，至少执行：

1. `scripts/prod101-health-check.ps1`
2. 手工登录发布中心
3. 点击一次 `发布当前渠道 manifest`
4. 直接访问两个公开 manifest 地址
5. 校验 `release-center` 页面入口与静态资源返回 `200`
6. 打开“站点管理”和“站点统计”页确认能正常读取数据

## 10. 风险边界

当前脚本只覆盖：

- 检查
- 常用重启
- 常用查询
- 静态页面部署

不包含：

- 全量重置 Supabase
- 清空 bucket
- 删除 `dib_release` schema
- 直接改写 `kong.yml` 其他现有路由

这些动作破坏性较强，必须人工确认后单独执行。
