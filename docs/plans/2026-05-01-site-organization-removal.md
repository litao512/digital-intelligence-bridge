# 站点单位名称运行时依赖移除实施计划

> 执行提示：按任务顺序逐项实施，并在每个验证点记录结果。

**目标：** 移除客户端侧 `SiteOrganization` 对资源获取、站点心跳、申请理由和身份导入导出的运行时影响，让资源授权只由中心侧 `sites.organization_id` 和站点资源绑定决定。

**架构：** 客户端只保存和上报稳定 `SiteId`、`SiteName`、`SiteRemark`、插件资源需求和版本信息。单位归属完全由 DIB 中心 `sites.organization_id` 管理，`SiteOrganization` 不再作为配置、身份文件或运行时输入存在。发布中心资源 RPC 保持 `p_site_id -> sites.organization_id -> permissions/bindings` 的现有模型。

**技术栈：** .NET 10、Avalonia、xUnit、TypeScript/Vitest、Supabase/Postgres SQL。

---

### 任务 1：用测试锁定客户端不再使用单位名称

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/SiteIdentityServiceTests.cs`
- 修改：`digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`

**步骤 1：编写预期失败的测试**

- `SiteIdentityService.ExportAndImport_ShouldSucceed_WhenSnapshotIsValid` 改为调用不含单位名称的导出 API，并断言快照不含 `SiteOrganization`。
- `ReleaseCenterServiceTests.ApplyResourceAsync_ShouldUseSiteNameOnlyInReason` 替换原有单位拼接断言，确认申请理由只包含站点名称。
- 增加心跳测试断言 `p_site_name` 只等于 `SiteName`。

**步骤 2：运行测试确认失败**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "SiteIdentityServiceTests|ReleaseCenterServiceTests"
```

预期：失败，因为生产代码仍需要或使用 `SiteOrganization`。

### 任务 2：移除客户端配置与身份文件中的 SiteOrganization

**文件：**
- 修改：`digital-intelligence-bridge/Configuration/AppSettings.cs`
- 修改：`digital-intelligence-bridge/Configuration/UserAppSettings.cs`
- 修改：`digital-intelligence-bridge/Configuration/ConfigurationExtensions.cs`
- 修改：`digital-intelligence-bridge/Services/SiteIdentityService.cs`
- 修改：`digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- 修改：`digital-intelligence-bridge/Services/SiteProfileService.cs`

**步骤 1：实现最小代码变更**

- 删除 `ReleaseCenterConfig.SiteOrganization` 和 `UserReleaseCenterConfig.SiteOrganization`。
- 保存用户配置时不再写 `SiteOrganization`。
- `EnsureSiteHeartbeatPayload` 使用 `_config.SiteName` 作为站点名，不再调用带单位的 label。
- `BuildApplicationReason` 只使用站点名。
- `SiteIdentitySnapshot` 升为 v3，字段只包含 `SiteId`、`SiteName`、`SiteRemark`。
- `Import` 只接受 v3 文件，不再兼容读取带单位名称的旧身份文件。

**步骤 2：运行聚焦测试**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "SiteIdentityServiceTests|ReleaseCenterServiceTests"
```

预期：通过。

### 任务 3：移除设置页单位输入和 ViewModel 绑定

**文件：**
- 修改：`digital-intelligence-bridge/ViewModels/SettingsViewModel.cs`
- 修改：`digital-intelligence-bridge/ViewModels/SiteRegistrationDialogViewModel.cs`
- 修改：`digital-intelligence-bridge/Views/SettingsView.axaml`
- 修改：`digital-intelligence-bridge/Views/SiteRegistrationDialog.axaml`
- 按需修改 `digital-intelligence-bridge.UnitTests/SettingsViewModel*.cs` 下的测试。

**步骤 1：编写或更新预期失败的测试**

- 确认保存站点信息只保存 `SiteName` 和 `SiteRemark`。
- 确认导入身份文件不再写单位名称。

**步骤 2：清理 UI 和 ViewModel**

- 删除 `SiteOrganization` 属性、校验和表单字段。
- UI 文案改为提示“单位归属请在 DIB 中心站点管理中配置”。

**步骤 3：运行聚焦测试**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal --filter "SettingsViewModel"
```

预期：通过。

### 任务 4：清理发布中心和文档中的名称兼容表述

**文件：**
- 修改：`docs/05-operations/NEW_MACHINE_SETUP_GUIDE.md`
- 按需修改：`docs/plans/2026-04-30-dib-center-resource-center-design.md`
- 检查：`dib-release-center/src/**`

**步骤 1：确认中心侧已经使用 organization_id**

- 保留 `organizationName` 作为 UI 展示字段。
- 不新增任何 `organization_name` 匹配逻辑。
- 删除资源侧 `owner_organization_name` 文本兼容字段，资源所属单位统一使用 `owner_organization_id`。

**步骤 2：更新文档**

- 新电脑指南改为“保存站点名称后，在 DIB 中心分配单位”。

### 任务 5：最终验证

**文件：**
- 所有已修改文件。

**步骤 1：串行构建和测试**

运行：

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

预期：通过。

**步骤 2：报告迁移提示**

- 告知旧用户配置里的 `ReleaseCenter.SiteOrganization` 会被忽略。
- 站点单位必须在 DIB 中心维护。
