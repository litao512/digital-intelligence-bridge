# DIB 中心资源中心一期 Implementation Plan

> **给执行者：** 必须使用 `superpowers:executing-plans`，并按任务逐项实施。

**Goal:** 将资源中心从站点级最小运行时升级为以单位为治理主体的 DIB 中心资源中心一期能力。

**Architecture:** 后端继续基于 Supabase 的 `dib_release` schema 和 RPC，先补单位、单位插件授权、单位资源授权，再让现有站点资源绑定受单位授权约束。管理端继续使用 Vue + TypeScript + Repository/Service 分层，客户端 RPC 契约尽量保持兼容。

**Tech Stack:** Supabase SQL、PostgreSQL RPC、Vue 3、TypeScript、Vitest、Avalonia 客户端、C# 服务层。

---

## 前置阅读

实施前先阅读：

- `docs/plans/2026-04-30-dib-center-resource-center-design.md`
- `docs/plans/2026-04-14-resource-center-design.md`
- `docs/plans/2026-04-21-resource-center-minimal-runtime-design.md`
- `dib-release-center/supabase/sql/17_create_resource_center_runtime_tables.sql`
- `dib-release-center/supabase/sql/19_create_get_site_authorized_resources_rpc.sql`
- `dib-release-center/supabase/sql/20_create_discover_site_resources_rpc.sql`
- `dib-release-center/supabase/sql/21_create_apply_site_resource_rpc.sql`
- `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- `digital-intelligence-bridge/ViewModels/ResourceCenterViewModel.cs`

实施中保持一个核心判断：

```text
单位授权决定可选范围
站点绑定决定实际使用
```

## Task 1: 数据库新增单位和单位授权模型

**Files:**
- Create: `dib-release-center/supabase/sql/22_create_organization_resource_governance_tables.sql`
- Modify: `dib-release-center/supabase/sql/08_validate_release_center_schema.sql`

**Step 1: 编写迁移 SQL**

创建 `organizations`、`organization_plugin_permissions`、`organization_resource_permissions`，并增强 `sites`、`resources`。

迁移内容应包含：

```sql
create table if not exists dib_release.organizations (
  id uuid primary key default gen_random_uuid(),
  code text not null,
  name text not null,
  organization_type text not null default 'Unknown',
  business_tags jsonb not null default '[]'::jsonb,
  status text not null default 'Active',
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ux_organizations_code unique (code),
  constraint ck_organizations_status check (status in ('Active', 'Inactive')),
  constraint ck_organizations_business_tags_array check (jsonb_typeof(business_tags) = 'array')
);
```

继续增加：

```sql
alter table dib_release.sites
  add column if not exists organization_id uuid references dib_release.organizations(id),
  add column if not exists business_tags jsonb not null default '[]'::jsonb;

alter table dib_release.resources
  add column if not exists owner_organization_id uuid references dib_release.organizations(id),
  add column if not exists business_tags jsonb not null default '[]'::jsonb;
```

再新增两张授权表：

```sql
create table if not exists dib_release.organization_plugin_permissions (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references dib_release.organizations(id) on delete cascade,
  plugin_code text not null,
  status text not null default 'Active',
  granted_by text not null default '',
  granted_at timestamptz not null default now(),
  expires_at timestamptz null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ux_org_plugin_permissions unique (organization_id, plugin_code),
  constraint ck_org_plugin_permissions_status check (status in ('Active', 'Inactive'))
);

create table if not exists dib_release.organization_resource_permissions (
  id uuid primary key default gen_random_uuid(),
  organization_id uuid not null references dib_release.organizations(id) on delete cascade,
  resource_id uuid not null references dib_release.resources(id) on delete cascade,
  status text not null default 'Active',
  granted_by text not null default '',
  granted_at timestamptz not null default now(),
  expires_at timestamptz null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ux_org_resource_permissions unique (organization_id, resource_id),
  constraint ck_org_resource_permissions_status check (status in ('Active', 'Inactive'))
);
```

为查询路径增加索引：

```sql
create index if not exists ix_sites_organization_id on dib_release.sites(organization_id);
create index if not exists ix_resources_owner_organization_id on dib_release.resources(owner_organization_id);
create index if not exists ix_org_plugin_permissions_org_status on dib_release.organization_plugin_permissions(organization_id, status);
create index if not exists ix_org_resource_permissions_org_status on dib_release.organization_resource_permissions(organization_id, status);
```

**Step 2: 扩展 schema 校验**

在 `08_validate_release_center_schema.sql` 中增加对新表和关键字段的存在性校验。

**Step 3: 本地 SQL 静态检查**

Run:

```powershell
Get-Content dib-release-center\supabase\sql\22_create_organization_resource_governance_tables.sql
```

Expected: 文件可以正常读取，SQL 语句编号和命名清晰。

**Step 4: Commit**

```powershell
git add dib-release-center\supabase\sql\22_create_organization_resource_governance_tables.sql dib-release-center\supabase\sql\08_validate_release_center_schema.sql
git commit -m "feat: 增加资源中心单位治理表"
```

## Task 2: 建立单位和授权的 TypeScript 契约

**Files:**
- Create: `dib-release-center/src/contracts/organization-types.ts`
- Modify: `dib-release-center/src/contracts/site-types.ts`

**Step 1: 编写类型契约**

新增单位、单位插件授权、单位资源授权类型：

```ts
export type OrganizationStatus = 'Active' | 'Inactive'

export interface OrganizationSummary {
  id: string
  code: string
  name: string
  organizationType: string
  businessTags: string[]
  status: OrganizationStatus
  createdAt: string
  updatedAt: string
}

export interface OrganizationPluginPermission {
  id: string
  organizationId: string
  pluginCode: string
  status: OrganizationStatus
  grantedBy: string
  grantedAt: string
  expiresAt: string | null
}

export interface OrganizationResourcePermission {
  id: string
  organizationId: string
  resourceId: string
  status: OrganizationStatus
  grantedBy: string
  grantedAt: string
  expiresAt: string | null
}
```

扩展 `SiteSummary`：

```ts
organizationId: string | null
organizationCode: string | null
organizationName: string | null
businessTags: string[]
```

**Step 2: 类型检查**

Run:

```powershell
Set-Location dib-release-center
npm run build
```

Expected: 允许先失败，失败点应集中在未更新的 Repository 映射。

**Step 3: Commit**

```powershell
git add dib-release-center\src\contracts\organization-types.ts dib-release-center\src\contracts\site-types.ts
git commit -m "feat: 增加单位治理类型契约"
```

## Task 3: 扩展站点视图和站点 Repository

**Files:**
- Modify: `dib-release-center/supabase/sql/07_create_release_center_views.sql`
- Modify: `dib-release-center/src/repositories/sitesRepository.ts`
- Modify: `dib-release-center/src/repositories/sitesRepository.test.ts`
- Modify: `dib-release-center/src/services/siteManagementService.ts`
- Modify: `dib-release-center/src/services/siteManagementService.test.ts`

**Step 1: 写失败测试**

在 `sitesRepository.test.ts` 增加映射测试，输入行包含：

```ts
organization_id: 'org-1',
organization_code: 'hospital-a',
organization_name: 'A 医院',
business_tags: ['门诊', '随访'],
```

Expected: `SiteSummary` 正确输出 `organizationId`、`organizationCode`、`organizationName`、`businessTags`。

**Step 2: 更新视图**

在 `site_overview` 中左连接 `organizations`，暴露：

```sql
organization_id
organization_code
organization_name
business_tags
```

**Step 3: 更新 Repository 映射**

修改 `SiteOverviewRow`、`toSiteSummary` 和查询字段。

**Step 4: 更新站点筛选服务**

让 `filterSites` 的搜索范围包含单位名称、单位编码和业务标签。

**Step 5: Run tests**

```powershell
Set-Location dib-release-center
npm test -- sitesRepository siteManagementService
```

Expected: 新增测试通过。

**Step 6: Commit**

```powershell
git add dib-release-center\supabase\sql\07_create_release_center_views.sql dib-release-center\src\repositories\sitesRepository.ts dib-release-center\src\repositories\sitesRepository.test.ts dib-release-center\src\services\siteManagementService.ts dib-release-center\src\services\siteManagementService.test.ts
git commit -m "feat: 在站点列表展示所属单位"
```

## Task 4: 实现单位 Repository 和服务

**Files:**
- Create: `dib-release-center/src/repositories/organizationsRepository.ts`
- Create: `dib-release-center/src/repositories/organizationsRepository.test.ts`
- Create: `dib-release-center/src/services/organizationManagementService.ts`
- Create: `dib-release-center/src/services/organizationManagementService.test.ts`

**Step 1: 写 Repository 测试**

覆盖：

```text
listOrganizations 映射字段
buildOrganizationInsertPayload 修剪 code/name/type
buildOrganizationUpdatePayload 保持 businessTags 为数组
throwIfError 输出中文错误
```

**Step 2: 实现 Repository**

导出：

```ts
listOrganizations(): Promise<OrganizationSummary[]>
createOrganization(input): Promise<void>
updateOrganization(id, input): Promise<void>
```

**Step 3: 写 Service 测试**

覆盖：

```text
按关键字过滤单位
按单位类型过滤单位
排除 Inactive 单位
标签参与搜索
```

**Step 4: 实现 Service**

导出：

```ts
createDefaultOrganizationFilterInput()
filterOrganizations(organizations, input)
normalizeBusinessTags(value)
```

**Step 5: Run tests**

```powershell
Set-Location dib-release-center
npm test -- organizationsRepository organizationManagementService
```

Expected: 新增测试通过。

**Step 6: Commit**

```powershell
git add dib-release-center\src\repositories\organizationsRepository.ts dib-release-center\src\repositories\organizationsRepository.test.ts dib-release-center\src\services\organizationManagementService.ts dib-release-center\src\services\organizationManagementService.test.ts
git commit -m "feat: 增加单位管理服务"
```

## Task 5: 实现单位授权 Repository 和服务

**Files:**
- Create: `dib-release-center/src/repositories/organizationPermissionsRepository.ts`
- Create: `dib-release-center/src/repositories/organizationPermissionsRepository.test.ts`
- Create: `dib-release-center/src/services/organizationPermissionService.ts`
- Create: `dib-release-center/src/services/organizationPermissionService.test.ts`

**Step 1: 写 Repository 测试**

覆盖：

```text
listOrganizationPluginPermissions
upsertOrganizationPluginPermission
deactivateOrganizationPluginPermission
listOrganizationResourcePermissions
upsertOrganizationResourcePermission
deactivateOrganizationResourcePermission
```

**Step 2: 实现 Repository**

使用 Supabase `.upsert(..., { onConflict })` 维护唯一授权关系。

**Step 3: 写 Service 测试**

覆盖：

```text
只返回 Active 授权
资源授权按 resourceId 去重
插件授权按 pluginCode 去重
过期授权不作为有效授权
```

**Step 4: 实现 Service**

导出：

```ts
isPermissionActive(permission, now)
getActivePluginCodes(permissions, now)
getActiveResourceIds(permissions, now)
```

**Step 5: Run tests**

```powershell
Set-Location dib-release-center
npm test -- organizationPermissionsRepository organizationPermissionService
```

Expected: 新增测试通过。

**Step 6: Commit**

```powershell
git add dib-release-center\src\repositories\organizationPermissionsRepository.ts dib-release-center\src\repositories\organizationPermissionsRepository.test.ts dib-release-center\src\services\organizationPermissionService.ts dib-release-center\src\services\organizationPermissionService.test.ts
git commit -m "feat: 增加单位插件和资源授权服务"
```

## Task 6: 升级资源中心 RPC 的单位授权校验

**Files:**
- Modify: `dib-release-center/supabase/sql/19_create_get_site_authorized_resources_rpc.sql`
- Modify: `dib-release-center/supabase/sql/20_create_discover_site_resources_rpc.sql`
- Modify: `dib-release-center/supabase/sql/21_create_apply_site_resource_rpc.sql`
- Create: `dib-release-center/supabase/sql/23_validate_resource_center_organization_governance.sql`

**Step 1: 更新 get_site_authorized_resources**

RPC 必须：

```text
根据 site_id 找站点
读取 sites.organization_id
拒绝未绑定单位的站点
只返回存在 Active resource_bindings 的资源
校验 organization_plugin_permissions 为 Active
校验 organization_resource_permissions 为 Active
保持返回 JSON 字段兼容客户端
```

**Step 2: 更新 discover_site_resources**

RPC 必须：

```text
根据站点所属单位限定候选资源
候选资源只来自单位 Active 资源授权
插件范围只来自单位 Active 插件授权
继续返回 availableToApply、authorized、pendingApplications
```

**Step 3: 更新 apply_site_resource**

RPC 必须：

```text
申请记录关联站点
通过站点找到单位
如果站点无单位，返回明确错误
如果资源不在单位授权范围内，申请仍可记录但状态保持 Pending
审批阶段后续由管理端补单位授权和站点绑定
```

**Step 4: 增加 SQL 验证脚本**

`23_validate_resource_center_organization_governance.sql` 至少验证：

```text
站点无 organization_id 时不会返回资源
单位无插件授权时不会返回资源
单位无资源授权时不会返回资源
单位授权 + 站点绑定齐全时返回资源
```

**Step 5: Commit**

```powershell
git add dib-release-center\supabase\sql\19_create_get_site_authorized_resources_rpc.sql dib-release-center\supabase\sql\20_create_discover_site_resources_rpc.sql dib-release-center\supabase\sql\21_create_apply_site_resource_rpc.sql dib-release-center\supabase\sql\23_validate_resource_center_organization_governance.sql
git commit -m "feat: 为资源中心RPC增加单位授权校验"
```

## Task 7: 增加 DIB 中心单位管理页面

**Files:**
- Create: `dib-release-center/src/web/pages/OrganizationsPage.vue`
- Modify: `dib-release-center/src/App.vue`

**Step 1: 页面结构**

页面包含：

```text
单位列表
关键字筛选
单位类型筛选
新增单位表单
编辑单位状态和标签
```

**Step 2: 接入 Repository**

页面加载时调用 `listOrganizations()`。

**Step 3: 修改导航**

在 `App.vue` 中增加“单位管理”页签。位置建议放在“站点管理”之前。

**Step 4: Build**

```powershell
Set-Location dib-release-center
npm run build
```

Expected: 构建通过。

**Step 5: Commit**

```powershell
git add dib-release-center\src\web\pages\OrganizationsPage.vue dib-release-center\src\App.vue
git commit -m "feat: 增加DIB中心单位管理页"
```

## Task 8: 增加资源管理和单位授权页面

**Files:**
- Create: `dib-release-center/src/repositories/resourcesRepository.ts`
- Create: `dib-release-center/src/repositories/resourcesRepository.test.ts`
- Create: `dib-release-center/src/web/pages/ResourcesPage.vue`
- Create: `dib-release-center/src/web/pages/OrganizationPermissionsPage.vue`
- Modify: `dib-release-center/src/App.vue`

**Step 1: 资源 Repository 测试**

覆盖：

```text
资源列表映射 ownerOrganizationId、businessTags、status
资源筛选包含资源名称、资源类型、所属单位、标签
```

**Step 2: 实现资源 Repository**

导出：

```ts
listResources()
createResource(input)
updateResource(id, input)
```

密钥维护如果当前页面暂不实现，应保留清晰占位，不在前端显示密钥明文。

**Step 3: 实现 ResourcesPage**

页面包含：

```text
资源列表
资源所属单位
资源类型
资源状态
业务标签
新增和编辑资源基础信息
```

**Step 4: 实现 OrganizationPermissionsPage**

页面包含：

```text
选择单位
单位插件授权列表
单位资源授权列表
授权和停用操作
```

**Step 5: Build 和测试**

```powershell
Set-Location dib-release-center
npm test -- resourcesRepository organizationPermissionService
npm run build
```

Expected: 测试和构建通过。

**Step 6: Commit**

```powershell
git add dib-release-center\src\repositories\resourcesRepository.ts dib-release-center\src\repositories\resourcesRepository.test.ts dib-release-center\src\web\pages\ResourcesPage.vue dib-release-center\src\web\pages\OrganizationPermissionsPage.vue dib-release-center\src\App.vue
git commit -m "feat: 增加资源管理和单位授权页面"
```

## Task 9: 增加站点资源绑定管理页面

**Files:**
- Create: `dib-release-center/src/repositories/resourceBindingsRepository.ts`
- Create: `dib-release-center/src/repositories/resourceBindingsRepository.test.ts`
- Create: `dib-release-center/src/services/resourceBindingService.ts`
- Create: `dib-release-center/src/services/resourceBindingService.test.ts`
- Create: `dib-release-center/src/web/pages/SiteResourceBindingsPage.vue`
- Modify: `dib-release-center/src/App.vue`

**Step 1: 写 Service 测试**

覆盖：

```text
站点无单位时不可绑定
插件未被单位授权时不可绑定
资源未被单位授权时不可绑定
同一 site + plugin + usage_key 只能有一个 Active 绑定
候选资源只来自单位授权资源
```

**Step 2: 实现绑定服务**

导出：

```ts
buildResourceBindingCandidateList(input)
validateResourceBindingRequest(input)
```

**Step 3: 实现 Repository**

导出：

```ts
listResourceBindingsBySite(siteRowId)
upsertResourceBinding(input)
deactivateResourceBinding(bindingId)
```

**Step 4: 实现页面**

页面包含：

```text
选择站点
展示站点所属单位
选择插件
输入或选择 usage_key
展示候选资源
创建、替换、停用绑定
```

**Step 5: Test and Build**

```powershell
Set-Location dib-release-center
npm test -- resourceBindingsRepository resourceBindingService
npm run build
```

Expected: 测试和构建通过。

**Step 6: Commit**

```powershell
git add dib-release-center\src\repositories\resourceBindingsRepository.ts dib-release-center\src\repositories\resourceBindingsRepository.test.ts dib-release-center\src\services\resourceBindingService.ts dib-release-center\src\services\resourceBindingService.test.ts dib-release-center\src\web\pages\SiteResourceBindingsPage.vue dib-release-center\src\App.vue
git commit -m "feat: 增加站点资源绑定管理"
```

## Task 10: 客户端兼容验证

**Files:**
- Modify only if needed: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Modify only if needed: `digital-intelligence-bridge/Services/AuthorizedResourceCacheService.cs`
- Modify only if needed: `digital-intelligence-bridge/ViewModels/ResourceCenterViewModel.cs`

**Step 1: 检查客户端契约**

确认 RPC 返回字段仍可被 `ReleaseCenterService` 解析。

重点检查：

```text
DiscoverResourcesAsync
GetAuthorizedResourcesAsync
ApplyResourceAsync
TrySaveAuthorizedResourcesToCache
```

**Step 2: 如需修改，先补测试**

如果已有测试工程，补充服务层解析测试。如果没有测试工程，本任务不新增大测试工程，只记录手动验证步骤。

**Step 3: 串行构建客户端**

Run:

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
```

Expected: 构建通过。

**Step 4: Commit**

如果客户端无代码变更，不提交。如果有变更：

```powershell
git add digital-intelligence-bridge\Services\ReleaseCenterService.cs digital-intelligence-bridge\Services\AuthorizedResourceCacheService.cs digital-intelligence-bridge\ViewModels\ResourceCenterViewModel.cs
git commit -m "fix: 兼容资源中心单位授权返回"
```

## Task 11: 总体验证和文档更新

**Files:**
- Modify: `docs/plans/2026-04-30-dib-center-resource-center.md`
- Modify if needed: `docs/standards/resource-center-development-guidelines.md`

**Step 1: 运行发布中心测试**

```powershell
Set-Location dib-release-center
npm test
```

Expected: 全部 Vitest 测试通过。

**Step 2: 运行发布中心构建**

```powershell
Set-Location dib-release-center
npm run build
```

Expected: `vue-tsc --noEmit` 和 `vite build` 通过。

**Step 3: 运行客户端构建**

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
```

Expected: 构建通过。

**Step 4: 运行文档语言检查**

```powershell
.\scripts\check-doc-lang.ps1
```

Expected: `Document language check passed.`

**Step 5: 更新计划状态**

在本计划中追加实施记录，包含：

```text
完成的迁移文件
完成的管理页面
通过的测试命令
未完成或延期事项
```

**Step 6: Final Commit**

```powershell
git add docs\plans\2026-04-30-dib-center-resource-center.md docs\standards\resource-center-development-guidelines.md
git commit -m "docs: 更新资源中心一期实施记录"
```

## 风险和回滚

主要风险：

1. 历史站点没有 `organization_id` 后无法发现资源。
2. 历史资源没有 `owner_organization_id` 后管理端展示不完整。
3. RPC 加入单位授权校验后，旧绑定可能因为缺少单位授权而不再返回。
4. 管理端页面如果一次性塞进 `App.vue`，可能继续放大单文件复杂度。

缓解策略：

1. 迁移时准备默认单位或待分配单位。
2. 历史资源不保留 `owner_organization_name` 兼容展示，所属单位统一通过 `owner_organization_id` 关联展示。
3. 上线前为现有站点和资源补齐单位插件授权、单位资源授权。
4. 新页面尽量放入 `src/web/pages`，`App.vue` 只负责导航和页面切换。

## 执行顺序

严格按任务顺序执行：

```text
数据库模型
类型契约
站点单位信息
单位服务
单位授权服务
RPC 校验
单位管理页面
资源和授权页面
站点绑定页面
客户端兼容验证
总体验证
```

不要先做页面再补授权模型。页面只是入口，单位授权和站点绑定才是资源中心一期的核心。

## 实施记录

实施分支：

```text
codex/dib-center-resource-center
```

已完成的迁移和验证脚本：

```text
dib-release-center/supabase/sql/22_create_organization_resource_governance_tables.sql
dib-release-center/supabase/sql/23_validate_resource_center_organization_governance.sql
```

已增强的 SQL：

```text
dib-release-center/supabase/sql/07_create_release_center_views.sql
dib-release-center/supabase/sql/08_validate_release_center_schema.sql
dib-release-center/supabase/sql/19_create_get_site_authorized_resources_rpc.sql
dib-release-center/supabase/sql/20_create_discover_site_resources_rpc.sql
dib-release-center/supabase/sql/21_create_apply_site_resource_rpc.sql
```

已完成的管理端能力：

```text
单位管理
站点绑定单位
资源管理
单位插件授权
单位资源授权
站点资源绑定
```

已完成的管理端页面：

```text
dib-release-center/src/web/pages/OrganizationsPage.vue
dib-release-center/src/web/pages/ResourcesPage.vue
dib-release-center/src/web/pages/OrganizationPermissionsPage.vue
dib-release-center/src/web/pages/SiteResourceBindingsPage.vue
```

已完成的服务和仓储：

```text
dib-release-center/src/repositories/organizationsRepository.ts
dib-release-center/src/repositories/organizationPermissionsRepository.ts
dib-release-center/src/repositories/resourcesRepository.ts
dib-release-center/src/repositories/resourceBindingsRepository.ts
dib-release-center/src/services/organizationManagementService.ts
dib-release-center/src/services/organizationPermissionService.ts
dib-release-center/src/services/resourceBindingService.ts
```

通过的验证命令：

```text
npm test
19 个测试文件通过，79 个测试通过

npm run build
vue-tsc --noEmit 和 vite build 通过

dotnet build digital-intelligence-bridge\digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
客户端 Debug 构建通过，0 警告，0 错误

.\scripts\check-doc-lang.ps1
文档语言检查通过
```

客户端兼容结论：

```text
客户端 RPC 契约未改变。
get_site_authorized_resources 仍返回 resources。
discover_site_resources 仍返回 availableToApply、authorized、pendingApplications。
apply_site_resource 仍返回 success、message、applicationId、status。
客户端代码无需修改。
```

延期事项：

```text
资源密钥维护页面暂未实现，仍沿用 resource_secrets 表和后端维护流程。
资源申请审批页面暂未完整实现，当前先具备单位授权和站点绑定的人工管理闭环。
标签仍为轻量 JSON 元数据，未升级为正式标签字典。
未引入组织层级、跨单位授权协议和策略引擎。
```
