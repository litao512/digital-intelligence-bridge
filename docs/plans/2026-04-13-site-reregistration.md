# 站点重新注册实现计划

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**目标:** 为托盘增加“重新注册站点”对话框，并让站点资料包含“使用单位”。

**架构:** 通过新增轻量对话框和站点资料保存辅助服务，复用现有本地配置持久化逻辑；托盘菜单直接打开该对话框。站点身份导入导出与首页摘要同步补充“使用单位”。

**技术栈:** .NET 10、Avalonia、Prism、xUnit

---

### 任务 1: 先写失败测试

**文件:**
- 修改: `digital-intelligence-bridge.UnitTests/SettingsViewModelInitializationTests.cs`
- 修改: `digital-intelligence-bridge.UnitTests/SiteIdentityServiceTests.cs`
- 修改: `digital-intelligence-bridge.UnitTests/HomeDashboardViewModelTests.cs`

**步骤 1: 编写失败测试**

为“使用单位持久化”“身份导出导入包含使用单位”“首页缺少使用单位时提示补全”补测试。

**步骤 2: 运行测试确认失败**

运行: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "FullyQualifiedName~SettingsViewModelInitializationTests|FullyQualifiedName~SiteIdentityServiceTests|FullyQualifiedName~HomeDashboardViewModelTests" -v minimal`

**步骤 3: 编写最小实现**

补配置字段、摘要格式、身份文件结构与校验逻辑。

**步骤 4: 回跑测试确认通过**

重复上面的 `dotnet test` 命令。

### 任务 2: 增加托盘重新注册对话框

**文件:**
- 修改: `digital-intelligence-bridge/Services/ITrayService.cs`
- 修改: `digital-intelligence-bridge/Services/TrayService.cs`
- 新增: `digital-intelligence-bridge/Services/SiteRegistrationDialogService.cs`
- 新增: `digital-intelligence-bridge/ViewModels/SiteRegistrationDialogViewModel.cs`
- 新增: `digital-intelligence-bridge/Views/SiteRegistrationDialog.axaml`
- 新增: `digital-intelligence-bridge/Views/SiteRegistrationDialog.axaml.cs`
- 修改: `digital-intelligence-bridge/App.axaml.cs`
- 修改: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`

**步骤 1: 编写失败测试**

优先依赖前面站点资料保存测试，不额外引入 UI 自动化测试。

**步骤 2: 运行测试确认失败**

沿用 Task 1 的测试集。

**步骤 3: 编写最小实现**

托盘新增菜单项，点击后弹轻量窗口，保存即写配置。

**步骤 4: 回跑测试确认通过**

运行: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Debug`

### 任务 3: 同步设置页与首页摘要

**文件:**
- 修改: `digital-intelligence-bridge/ViewModels/SettingsViewModel.cs`
- 修改: `digital-intelligence-bridge/Views/SettingsView.axaml`
- 修改: `digital-intelligence-bridge/ViewModels/HomeDashboardViewModel.cs`

**步骤 1: 编写失败测试**

前面首页和设置页测试已覆盖核心行为。

**步骤 2: 运行测试确认失败**

沿用 Task 1 的测试集。

**步骤 3: 编写最小实现**

设置页新增“使用单位”输入框，首页站点摘要改为 `使用单位 / 站点名称`。

**步骤 4: 回跑测试确认通过**

运行: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "FullyQualifiedName~SettingsViewModelInitializationTests|FullyQualifiedName~SiteIdentityServiceTests|FullyQualifiedName~HomeDashboardViewModelTests" -v minimal`
