# 站点单位名称运行时依赖移除 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 移除客户端侧 `SiteOrganization` 对资源获取、站点心跳、申请理由和身份导入导出的运行时影响，让资源授权只由中心侧 `sites.organization_id` 和站点资源绑定决定。

**Architecture:** 客户端只保存和上报稳定 `SiteId`、`SiteName`、`SiteRemark`、插件资源需求和版本信息。单位归属完全由 DIB 中心 `sites.organization_id` 管理，`SiteOrganization` 不再作为配置、身份文件或运行时输入存在。发布中心资源 RPC 保持 `p_site_id -> sites.organization_id -> permissions/bindings` 的现有模型。

**Tech Stack:** .NET 10、Avalonia、xUnit、TypeScript/Vitest、Supabase/Postgres SQL。

---

### Task 1: 用测试锁定客户端不再使用单位名称

**Files:**
- Modify: `digital-intelligence-bridge.UnitTests/SiteIdentityServiceTests.cs`
- Modify: `digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`

**Step 1: Write the failing tests**

- `SiteIdentityService.ExportAndImport_ShouldSucceed_WhenSnapshotIsValid` 改为调用不含单位名称的导出 API，并断言快照不含 `SiteOrganization`。
- `ReleaseCenterServiceTests.ApplyResourceAsync_ShouldUseSiteNameOnlyInReason` 替换原有单位拼接断言，确认申请理由只包含站点名称。
- 增加心跳测试断言 `p_site_name` 只等于 `SiteName`。

**Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "SiteIdentityServiceTests|ReleaseCenterServiceTests"
```

Expected: FAIL，因为生产代码仍需要/使用 `SiteOrganization`。

### Task 2: 移除客户端配置与身份文件中的 SiteOrganization

**Files:**
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`
- Modify: `digital-intelligence-bridge/Configuration/UserAppSettings.cs`
- Modify: `digital-intelligence-bridge/Configuration/ConfigurationExtensions.cs`
- Modify: `digital-intelligence-bridge/Services/SiteIdentityService.cs`
- Modify: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Modify: `digital-intelligence-bridge/Services/SiteProfileService.cs`

**Step 1: Implement minimal code**

- 删除 `ReleaseCenterConfig.SiteOrganization` 和 `UserReleaseCenterConfig.SiteOrganization`。
- 保存用户配置时不再写 `SiteOrganization`。
- `EnsureSiteHeartbeatPayload` 使用 `_config.SiteName` 作为站点名，不再调用带单位的 label。
- `BuildApplicationReason` 只使用站点名。
- `SiteIdentitySnapshot` 升为 v3，字段只包含 `SiteId`、`SiteName`、`SiteRemark`。
- `Import` 只接受 v3 文件，不再兼容读取带单位名称的旧身份文件。

**Step 2: Run focused tests**

Run:

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "SiteIdentityServiceTests|ReleaseCenterServiceTests"
```

Expected: PASS.

### Task 3: 移除设置页单位输入和 ViewModel 绑定

**Files:**
- Modify: `digital-intelligence-bridge/ViewModels/SettingsViewModel.cs`
- Modify: `digital-intelligence-bridge/ViewModels/SiteRegistrationDialogViewModel.cs`
- Modify: `digital-intelligence-bridge/Views/SettingsView.axaml`
- Modify: `digital-intelligence-bridge/Views/SiteRegistrationDialog.axaml`
- Modify tests under `digital-intelligence-bridge.UnitTests/SettingsViewModel*.cs` as needed.

**Step 1: Write or update failing tests**

- 确认保存站点信息只保存 `SiteName` 和 `SiteRemark`。
- 确认导入身份文件不再写单位名称。

**Step 2: Implement UI/ViewModel cleanup**

- 删除 `SiteOrganization` 属性、校验和表单字段。
- UI 文案改为提示“单位归属请在 DIB 中心站点管理中配置”。

**Step 3: Run focused tests**

Run:

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "SettingsViewModel"
```

Expected: PASS.

### Task 4: 清理发布中心和文档中的名称兼容表述

**Files:**
- Modify: `docs/05-operations/NEW_MACHINE_SETUP_GUIDE.md`
- Modify: `docs/plans/2026-04-30-dib-center-resource-center-design.md` if needed
- Inspect: `dib-release-center/src/**`

**Step 1: Verify center already uses organization_id**

- 保留 `organizationName` 作为 UI 展示字段。
- 不新增任何 `organization_name` 匹配逻辑。
- 删除资源侧 `owner_organization_name` 文本兼容字段，资源所属单位统一使用 `owner_organization_id`。

**Step 2: Update docs**

- 新电脑指南改为“保存站点名称后，在 DIB 中心分配单位”。

### Task 5: Final verification

**Files:**
- All touched files.

**Step 1: Build and test serially**

Run:

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

Expected: PASS.

**Step 2: Report migration note**

- 告知旧用户配置里的 `ReleaseCenter.SiteOrganization` 会被忽略。
- 站点单位必须在 DIB 中心维护。
