# 站点授权实施计划

> **执行要求：** 使用 `superpowers:executing-plans` 技能按任务逐步执行。

**目标：** 为每个 DIB 安装实例建立站点身份，支持按站点分组批量授权插件，并在发布中心展示站点统计分析。

**架构：** 在 `dib_release` schema 中新增站点、分组、授权与心跳表；DIB 客户端在启动或检查更新时上报站点信息；发布中心按 `site_id` 和 `channel` 生成裁剪后的插件 manifest，并提供站点管理与统计页面。

**技术栈：** Supabase Postgres、Supabase Auth、Vue 3、TypeScript、Vite、.NET 10、Avalonia、xUnit

---

### 任务 1: 扩展数据库 schema 支撑站点与授权

**文件：**
- Create: `dib-release-center/supabase/sql/10_create_site_groups.sql`
- Create: `dib-release-center/supabase/sql/11_create_sites.sql`
- Create: `dib-release-center/supabase/sql/12_create_group_plugin_policies.sql`
- Create: `dib-release-center/supabase/sql/13_create_site_plugin_overrides.sql`
- Create: `dib-release-center/supabase/sql/14_create_site_heartbeats.sql`
- Modify: `dib-release-center/supabase/sql/07_create_release_center_views.sql`
- Modify: `dib-release-center/supabase/sql/08_validate_release_center_schema.sql`
- Modify: `dib-release-center/supabase/sql/09_create_release_center_admins_and_policies.sql`

**步骤 1：先写会失败的 SQL 校验扩展**
- 在 `08_validate_release_center_schema.sql` 先增加对新表、新索引、新视图的断言。
- 预期当前执行会失败，因为对象尚不存在。

**步骤 2：创建最小可用 schema 对象**
- 新建站点、站点组、组级授权、站点覆盖、心跳表。
- 建立必要索引：
  - `sites(site_id)` 唯一
  - `sites(group_id)`
  - `site_heartbeats(site_id, created_at desc)`
  - `group_plugin_policies(group_id, plugin_package_id)` 唯一
  - `site_plugin_overrides(site_id, plugin_package_id)` 唯一（可配合 `is_active` 控制）

**步骤 3：更新视图**
- 在 `07_create_release_center_views.sql` 增加：
  - 站点概览视图
  - 站点统计视图
  - 站点授权解析视图或辅助视图

**步骤 4：更新 RLS/策略**
- 允许管理员读写站点与授权表。
- 保持客户端侧不直接写原始表，后续优先走 RPC 或受限接口。

**步骤 5：执行校验**
执行： 对 `08_validate_release_center_schema.sql` 进行验证执行
预期： 全部断言通过

**步骤 6：提交**
```bash
git add dib-release-center/supabase/sql
git commit -m "feat(release-center): add site authorization schema"
```

### 任务 2: 发布中心增加站点与授权数据访问层

**文件：**
- Create: `dib-release-center/src/contracts/site-types.ts`
- Create: `dib-release-center/src/repositories/siteGroupsRepository.ts`
- Create: `dib-release-center/src/repositories/sitesRepository.ts`
- Create: `dib-release-center/src/repositories/groupPluginPoliciesRepository.ts`
- Create: `dib-release-center/src/repositories/sitePluginOverridesRepository.ts`
- Create: `dib-release-center/src/services/siteAuthorizationService.ts`
- Test: `dib-release-center/src/services/siteAuthorizationService.test.ts`

**步骤 1：先写会失败的测试**
- 为分组授权合并与站点覆盖解析写纯函数测试：
  - 组授权允许某插件
  - 站点覆盖 deny 会移除该插件
  - 版本范围不兼容时剔除插件

**步骤 2：执行测试并确认失败**
执行： `npm test -- siteAuthorizationService`
预期： FAIL，因为文件和实现不存在

**步骤 3：实现最小仓储与服务**
- 建立类型定义与仓储。
- 在 `siteAuthorizationService.ts` 中实现：
  - 站点有效授权解析
  - 分组与覆盖合并
  - 输出可供 manifest 使用的插件结果

**步骤 4：执行测试并确认通过**
执行： `npm test`
预期： 新测试通过

**Step 5: Commit**
```bash
git add dib-release-center/src
git commit -m "feat(release-center): add site authorization repositories"
```

### 任务 3: DIB 客户端生成站点身份并上报站点状态

**文件：**
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`
- Modify: `digital-intelligence-bridge/appsettings.json`
- Modify: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Modify: `digital-intelligence-bridge/ViewModels/SettingsViewModel.cs`
- Test: `digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`
- Test: `digital-intelligence-bridge.UnitTests/SettingsViewModelReleaseCenterTests.cs`

**Step 1: Write failing tests**
- 新增测试断言：
  - 首次运行时生成稳定 `SiteId`
  - 调用更新检查前会构建站点注册/心跳 payload
  - 第二次运行不会重新生成 `SiteId`

**步骤 2：执行测试并确认失败**
执行： `dotnet test ... --filter "SiteId|SiteHeartbeat"`
预期： FAIL

**步骤 3：实现最小配置与服务改动**
- 在配置中加入：
  - `ReleaseCenter.SiteId`
  - `ReleaseCenter.SiteName`
- 启动或检查更新前确保 `SiteId` 存在。
- 在 `ReleaseCenterService` 中增加站点注册/心跳调用。

**步骤 4：执行测试并确认通过**
执行： `dotnet test ... --filter "SiteId|SiteHeartbeat|ReleaseCenterServiceTests|SettingsViewModelReleaseCenterTests"`
预期： PASS

**Step 5: Commit**
```bash
git add digital-intelligence-bridge digital-intelligence-bridge.UnitTests
git commit -m "feat(dib): add site identity and heartbeat"
```

### 任务 4: 插件 manifest 按站点裁剪

**文件：**
- Modify: `dib-release-center/src/services/manifestService.ts`
- Modify: `dib-release-center/src/services/manifestService.test.ts`
- Modify: `dib-release-center/src/contracts/plugin-manifest.ts`
- Modify: `dib-release-center/src/services/supabase.ts`
- Modify: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Test: `digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`

**Step 1: Write failing tests**
- 测试 `manifestService`：
  - 不同 `site_id` 返回不同插件集合
  - 未分组站点只拿到默认空集或 `unassigned` 组授权
- 测试客户端：
  - 请求插件 manifest 时附带 `site_id`

**步骤 2：执行测试并确认失败**
执行： `npm test` and `dotnet test ... --filter "ReleaseCenterServiceTests|manifestService"`
预期： FAIL

**步骤 3：实现最小 manifest 改动**
- `manifestService` 支持带 `site_id` 裁剪插件列表。
- DIB 客户端请求插件 manifest 时带上 `site_id`。
- 保持客户端 manifest 仍按 channel。

**步骤 4：执行测试并确认通过**
执行： `npm test` and `dotnet test ... --filter "ReleaseCenterServiceTests"`
预期： PASS

**Step 5: Commit**
```bash
git add dib-release-center/src digital-intelligence-bridge/Services/ReleaseCenterService.cs digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs
git commit -m "feat(release-center): scope plugin manifest by site"
```

### 任务 5: 发布中心增加站点管理页

**文件：**
- Create: `dib-release-center/src/web/pages/SitesPage.vue`
- Modify: `dib-release-center/src/App.vue`
- Modify: `dib-release-center/src/repositories/sitesRepository.ts`
- Modify: `dib-release-center/src/repositories/siteGroupsRepository.ts`
- Modify: `dib-release-center/src/services/releaseAuthService.ts`（如需权限辅助）

**步骤 1：先写会失败的 UI/服务测试**
- 为站点管理页的数据加载与分组更新写最小测试或纯函数测试。

**步骤 2：执行测试并确认失败**
执行： `npm test`
预期： 新增测试失败

**步骤 3：实现页面**
- 新增站点列表页，展示：
  - `site_name`
  - `site_id`
  - `group`
  - `client_version`
  - `last_seen_at`
- 支持修改站点分组。

**步骤 4：执行测试/构建**
执行： `npm test` and `npm run build`
预期： PASS

**Step 5: Commit**
```bash
git add dib-release-center/src
git commit -m "feat(release-center): add site management page"
```

### 任务 6: 发布中心增加站点统计页

**文件：**
- Create: `dib-release-center/src/web/pages/SiteAnalyticsPage.vue`
- Modify: `dib-release-center/src/App.vue`
- Modify: `dib-release-center/src/repositories/sitesRepository.ts`
- Modify: `dib-release-center/src/contracts/site-types.ts`
- Test: `dib-release-center/src/services/siteAuthorizationService.test.ts`

**步骤 1：先写会失败的测试**
- 为统计聚合函数写测试：
  - 站点总数
  - 活跃站点数
  - 未分组站点数
  - 版本分布
  - 授权/安装差异数

**步骤 2：执行测试并确认失败**
执行： `npm test`
预期： FAIL

**步骤 3：实现页面与聚合逻辑**
- 在仓储或服务层增加站点统计查询。
- 页面展示：
  - 总览卡片
  - 分组分布
  - 版本分布
  - 授权/安装差异
  - 最近活动

**步骤 4：执行测试/构建**
执行： `npm test` and `npm run build`
预期： PASS

**Step 5: Commit**
```bash
git add dib-release-center/src
git commit -m "feat(release-center): add site analytics page"
```

### 任务 7: 文档与运维补充

**文件：**
- Modify: `dib-release-center/README.md`
- Modify: `dib-release-center/docs/PROD101_RELEASE_CENTER_OPERATIONS_GUIDE.md`
- Modify: `dib-release-center/docs/PROD101_SQL_RUNBOOK.md`
- Create: `docs/plans/2026-04-06-site-authorization-design.md`
- Create: `docs/plans/2026-04-06-site-authorization-implementation.md`

**步骤 1：更新文档**
- 补充站点注册、分组授权、按站点 manifest、统计页说明。
- 补充 SQL 执行顺序与 prod101 验证要点。

**步骤 2：执行最终验证**
执行：
- `npm test`
- `npm run build`
- `dotnet test ... --filter "ReleaseCenter|SettingsViewModel"`
- `dotnet build ... /p:UseSharedCompilation=false`
预期： 全部通过

**步骤 3：提交**
```bash
git add docs dib-release-center
git commit -m "docs(release-center): document site authorization workflow"
```

