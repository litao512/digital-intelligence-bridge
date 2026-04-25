# 授权资源快照缓存实现计划

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**Goal:** 为 DIB 宿主建立第一阶段可运行的授权资源内存快照，并通过插件宿主上下文将授权资源按 `usageKey` 下发给试点插件消费。

**Architecture:** 采用“资源中心后台同步 -> 宿主内存快照 -> 插件上下文读取”的三层路径。后台仍是真相源，宿主维护站点级授权快照和插件级资源索引，插件通过 `IPluginHostContext` 按 `usageKey` 读取资源，优先使用宿主下发资源，再回退本地配置。

**Tech Stack:** .NET 10、Avalonia、现有 `IReleaseCenterService`、`DigitalIntelligenceBridge.Plugin.Abstractions`、`DigitalIntelligenceBridge.Plugin.Host`、xUnit、JSON 资源契约。

---

### Task 1: 固化授权资源缓存设计文档

**Files:**
- Create: `docs/plans/2026-04-14-authorized-resource-cache-design.md`
- Modify: `docs/plans/README.md`

**Step 1: 编写设计文档**

写入：
- 双层缓存结论
- 第一阶段“先内存、后磁盘”的实现顺序
- 宿主上下文暴露资源的接口方式
- 插件优先读宿主资源、再回退本地配置的边界

**Step 2: 更新计划导航**

将新增设计文档加入 `docs/plans/README.md` 的“当前有效文档”。

**Step 3: 运行文档语言检查**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: 文档语言检查通过。

### Task 2: 定义授权资源缓存模型

**Files:**
- Create: `digital-intelligence-bridge/Models/AuthorizedResourceCacheSnapshot.cs`
- Create: `digital-intelligence-bridge/Models/AuthorizedPluginResourceSet.cs`
- Create: `digital-intelligence-bridge/Models/AuthorizedRuntimeResource.cs`
- Test: `digital-intelligence-bridge.UnitTests/AuthorizedResourceCacheModelsTests.cs`

**Step 1: 写失败测试**

覆盖：
- 站点级快照包含站点标识、同步时间和资源集合
- 插件级资源集按 `PluginCode` 聚合
- 运行时资源保留 `UsageKey` 和 `Configuration`

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "AuthorizedResourceCacheModelsTests" --no-restore -m:1 -v minimal`
Expected: 因模型缺失或字段不匹配失败。

**Step 3: 写最小实现**

新增三个模型，字段以设计稿为准，避免提前引入磁盘持久化逻辑。

**Step 4: 回跑测试确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "AuthorizedResourceCacheModelsTests" --no-restore -m:1 -v minimal`
Expected: 测试通过。

### Task 3: 增加宿主授权资源缓存服务

**Files:**
- Create: `digital-intelligence-bridge/Services/AuthorizedResourceCacheService.cs`
- Modify: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`
- Modify: `digital-intelligence-bridge/App.axaml.cs`
- Test: `digital-intelligence-bridge.UnitTests/AuthorizedResourceCacheServiceTests.cs`

**Step 1: 写失败测试**

覆盖：
- 缓存服务能保存当前站点快照
- 能按 `PluginCode` 返回对应资源集
- 未命中插件时返回空集而不是异常

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "AuthorizedResourceCacheServiceTests" --no-restore -m:1 -v minimal`
Expected: 因服务缺失失败。

**Step 3: 写最小实现**

新增接口和内存实现：
- 保存快照
- 读取当前快照
- 按 `PluginCode` 读取资源集

**Step 4: 注册服务**

在宿主容器中注册 `IAuthorizedResourceCacheService`。

**Step 5: 回跑测试确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "AuthorizedResourceCacheServiceTests" --no-restore -m:1 -v minimal`
Expected: 测试通过。

### Task 4: 扩展插件宿主上下文资源读取接口

**Files:**
- Modify: `DigitalIntelligenceBridge.Plugin.Abstractions/IPluginHostContext.cs`
- Modify: `DigitalIntelligenceBridge.Plugin.Host/PluginHostContext.cs`
- Test: `digital-intelligence-bridge.UnitTests/PluginHostContextResourceTests.cs`

**Step 1: 写失败测试**

覆盖：
- `PluginHostContext` 能返回当前插件全部授权资源
- 能按 `usageKey` 返回单个资源
- 未命中 `usageKey` 时返回失败而不是异常

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "PluginHostContextResourceTests" --no-restore -m:1 -v minimal`
Expected: 因接口和实现缺失失败。

**Step 3: 写最小实现**

在 `IPluginHostContext` 中增加：
- `GetAuthorizedResources()`
- `TryGetResource(string usageKey, out AuthorizedRuntimeResource? resource)`

并在 `PluginHostContext` 中接入缓存服务提供的插件资源集。

**Step 4: 回跑测试确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "PluginHostContextResourceTests" --no-restore -m:1 -v minimal`
Expected: 测试通过。

### Task 5: 宿主同步授权资源后写入缓存

**Files:**
- Modify: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Modify: `digital-intelligence-bridge/App.axaml.cs`
- Test: `digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`

**Step 1: 写失败测试**

覆盖：
- 获取授权资源后，宿主能将结果写入缓存
- 写入内容按插件聚合并保留 `UsageKey`

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "ReleaseCenterServiceTests" --no-restore -m:1 -v minimal`
Expected: 因未写入缓存失败。

**Step 3: 写最小实现**

将后台授权结果转换为宿主内部快照格式，并通过缓存服务保存。

**Step 4: 回跑测试确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "ReleaseCenterServiceTests" --no-restore -m:1 -v minimal`
Expected: 目标测试通过。

### Task 6: 试点插件接入宿主授权资源

**Files:**
- Modify: `plugins-src/PatientRegistration.Plugin/PatientRegistrationPlugin.cs`
- Modify: `plugins-src/PatientRegistration.Plugin/Configuration/*`
- Modify: `plugins-src/PatientRegistration.Plugin/plugin.json` 或对应资源声明文件
- Test: `digital-intelligence-bridge.UnitTests/PatientRegistrationResourceResolutionTests.cs`

**Step 1: 写失败测试**

覆盖：
- 插件优先从宿主上下文读取 `registration-db`
- 若宿主无资源，再回退本地 `plugin.settings.json`

**Step 2: 运行测试确认失败**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "PatientRegistrationResourceResolutionTests" --no-restore -m:1 -v minimal`
Expected: 因插件尚未读取宿主资源失败。

**Step 3: 写最小实现**

在试点插件中：
- 初始化时保存宿主上下文
- 创建内容时优先读取 `usageKey = registration-db`
- 取不到时再使用本地配置加载器

**Step 4: 回跑测试确认通过**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "PatientRegistrationResourceResolutionTests" --no-restore -m:1 -v minimal`
Expected: 测试通过。

### Task 7: 串行回归验证

**Files:**
- Modify: `docs/plans/README.md`（如需补充实施记录）
- Test: `digital-intelligence-bridge.UnitTests/*`

**Step 1: 跑资源中心与插件宿主相关测试集**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "AuthorizedResourceCacheModelsTests|AuthorizedResourceCacheServiceTests|PluginHostContextResourceTests|ReleaseCenterServiceTests|PatientRegistrationResourceResolutionTests|ResourceCenterViewModelTests" --no-restore -m:1 -v minimal`
Expected: 全部通过。

**Step 2: 跑主项目构建**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
Expected: `0 个错误`。

**Step 3: 检查工作区格式**

Run: `git diff --check`
Expected: 无新增格式错误；仅可能保留仓库已有 LF/CRLF warning。

**Step 4: 记录本轮实施结果**

如实现过程中有新的长期规则，将其补到 `docs/standards/resource-center-development-guidelines.md`，不要只写在 `docs/plans/`。
