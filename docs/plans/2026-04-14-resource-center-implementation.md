# 资源中心实施计划

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**Goal:** 为 DIB 建立第一阶段可落地的资源中心骨架，完成资源主数据、申请审批、授权查询、托盘同步和插件资源声明的最小闭环。

**Architecture:** 采用“资源中心后台 + 托盘宿主 + 插件声明/消费”的三层模型。后台维护资源真相源与审批授权，托盘同步并向插件注入资源上下文，插件受控直连外部资源。

**Tech Stack:** .NET 10、Avalonia、现有 DIB 配置与插件宿主体系、Supabase/PostgreSQL 后台接口、JSON 配置与运行时依赖注入。

---

### Task 1: 固化资源中心设计文档

**Files:**
- Create: `docs/plans/2026-04-14-resource-center-design.md`
- Modify: `docs/plans/README.md`

**Step 1: 检查导航格式**

Run: `Get-Content docs\plans\README.md`
Expected: 能看到“当前有效文档”列表。

**Step 2: 编写正式设计文档**

写入资源中心的目标、对象模型、状态流转、职责边界、第三方产品选型结论。

**Step 3: 更新导航**

将资源中心设计文档加入 `docs/plans/README.md` 的“当前有效文档”。

**Step 4: 验证文档语言**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: 新增文档通过语言检查。

### Task 2: 定义后台资源中心数据契约

**Files:**
- Create: `docs/plans/2026-04-14-resource-center-contracts.md`
- Modify: `docs/PRD.md`

**Step 1: 编写实体契约**

定义：
- `Organization`
- `Site`
- `Plugin`
- `Resource`
- `ResourceSecret`
- `ResourceBinding`
- `ResourceApplication`
- `ApprovalLog`

**Step 2: 编写状态枚举**

明确：
- 资源状态
- 申请状态
- 绑定状态
- 可见性范围

**Step 3: 补充 PRD 入口**

在 `docs/PRD.md` 中增加资源中心的章节入口，避免后续实现和产品语义脱节。

**Step 4: 文档校验**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: 新文档通过语言检查。

### Task 3: 设计后台 API 草案

**Files:**
- Create: `docs/plans/2026-04-14-resource-center-api.md`

**Step 1: 定义注册类 API**

包含：
- 单位注册/查重
- 站点注册/更新
- 资源注册申请

**Step 2: 定义授权类 API**

包含：
- 资源发现列表
- 资源使用申请
- 已授权资源查询
- 审批动作接口

**Step 3: 定义响应模型**

明确：
- 托盘同步时的输入输出
- 资源列表返回结构
- 插件可消费的资源描述对象

**Step 4: 文档校验**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: API 草案通过语言检查。

### Task 4: 为插件增加资源声明协议

**Files:**
- Modify: `docs/plugin-development-conventions.md`
- Modify: `plugins-src/*/plugin.json`

**Step 1: 设计资源声明结构**

在插件约定中新增资源声明字段，例如：
- `resourceRequirements`
- `resourceType`
- `usageKey`
- `required`

**Step 2: 先改约定文档**

更新 `docs/plugin-development-conventions.md`，明确插件如何声明所需资源。

**Step 3: 选一个现有插件做试点**

优先选已有数据库依赖或服务依赖的插件，给出一个真实示例。

**Step 4: 手动验证**

确认试点插件的 `plugin.json` 能被宿主发现且结构清晰。

### Task 5: 为宿主补充资源同步模型

**Files:**
- Create: `digital-intelligence-bridge/Models/ResourceDescriptor.cs`
- Create: `digital-intelligence-bridge/Models/AuthorizedResourceSnapshot.cs`
- Modify: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Modify: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`

**Step 1: 先写模型测试**

在单测项目中为资源快照、资源描述对象和反序列化行为写测试。

**Step 2: 建立最小模型**

定义宿主内部用于承载授权资源的对象。

**Step 3: 扩展后台同步入口**

在现有站点注册/心跳基础上扩展资源发现与授权查询接口。

**Step 4: 串行运行相关测试**

Run: `dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`
Expected: 构建通过。

### Task 6: 为托盘增加资源中心入口

**Files:**
- Create: `digital-intelligence-bridge/ViewModels/ResourceCenterViewModel.cs`
- Create: `digital-intelligence-bridge/Views/ResourceCenterView.axaml`
- Modify: `digital-intelligence-bridge/Services/TrayService.cs`
- Modify: `digital-intelligence-bridge/ViewModels/MainWindowViewModel.cs`

**Step 1: 先定义最小界面范围**

界面先支持：
- 已授权资源列表
- 可申请资源列表
- 申请状态查看

**Step 2: 先写 ViewModel 测试**

覆盖：
- 授权列表加载
- 申请动作
- 空状态与异常状态

**Step 3: 补最小界面**

先不做复杂审批面板，只做托盘侧的发现和申请入口。

**Step 4: 手动回归**

启动客户端，确认托盘或主界面能进入资源中心入口。

### Task 7: 为插件宿主补充运行时资源注入

**Files:**
- Create: `digital-intelligence-bridge/Plugins/IPluginResourceContext.cs`
- Modify: `digital-intelligence-bridge/Services/*Plugin*`
- Modify: `digital-intelligence-bridge/Models/*Plugin*`

**Step 1: 先做接口定义**

定义插件可见的资源读取接口，不直接暴露后台管理能力。

**Step 2: 做最小注入实现**

宿主按插件过滤已授权资源，并在插件激活时注入资源上下文。

**Step 3: 试点改造一个插件**

优先挑选目前依赖数据库连接或 HTTP 服务的插件。

**Step 4: 手动验证**

确认插件能拿到授权资源，但看不到未授权资源。

### Task 8: 增加本地缓存与撤销处理

**Files:**
- Create: `digital-intelligence-bridge/Services/AuthorizedResourceCacheService.cs`
- Modify: `digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- Modify: `digital-intelligence-bridge/Configuration/AppSettings.cs`

**Step 1: 定义缓存策略**

明确：
- 缓存位置
- 缓存时效

---

## 本轮实施经验

### 1. RPC 契约必须显式映射
- 资源中心的 RPC 返回对象不能默认依赖本地命名推断。
- 本轮 `ApplyResourceAsync` 的失败根因，是后台返回 `success`，而本地使用 `IsSuccess`，未显式映射时会被反序列化为默认值。
- 后续同类对象统一按 [resource-center-development-guidelines.md](../standards/resource-center-development-guidelines.md) 处理。

### 2. 功能推进顺序要稳定
- 本轮验证下来，资源中心功能按“服务层契约测试 -> ViewModel 行为测试 -> 视图绑定 -> 手动回归”推进，定位问题最快。
- 先锁定 RPC 契约和 ViewModel 状态，再接界面按钮，能避免 UI 表面故障掩盖底层契约错误。

### 3. 扩接口先补测试桩
- `IReleaseCenterService` 一旦扩接口，必须第一时间补齐所有 fake 和 stub。
- 否则会先出现大面积编译失败，干扰对真实业务问题的判断。

### 4. Avalonia 验证继续串行
- 本轮再次确认：资源中心相关改动涉及 Avalonia 项目时，`dotnet build` 和 `dotnet test` 继续保持串行执行，优先使用 `-m:1`。
- 版本号
- 撤销后的清理规则

**Step 2: 先写测试**

覆盖：
- 授权资源写缓存
- 资源更新刷新缓存
- 绑定撤销后不再下发

**Step 3: 实现最小缓存**

仅做宿主层缓存，不把敏感资源散落到插件目录。

**Step 4: 串行验证**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
Expected: 构建通过。

### Task 9: 补文档与回归清单

**Files:**
- Modify: `digital-intelligence-bridge/README.md`
- Create: `docs/plans/2026-04-14-resource-center-regression-checklist.md`

**Step 1: 更新 README**

补充资源中心相关说明和使用入口。

**Step 2: 编写回归清单**

覆盖：
- 单位存在性校验
- 站点资源发现
- 资源申请
- 审批后同步
- 插件仅消费已授权资源

**Step 3: 文档语言校验**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: 文档检查通过。

### Task 10: 全量验证

**Files:**
- Modify: `docs/plans/2026-04-14-resource-center-implementation.md`

**Step 1: 串行构建主应用**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
Expected: PASS

**Step 2: 串行构建单测项目**

Run: `dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`
Expected: PASS

**Step 3: 串行执行相关测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal`
Expected: PASS

**Step 4: 手动回归资源中心流程**

确认：
- 托盘可发现资源
- 可发起申请
- 审批后可同步
- 插件只消费已授权资源
