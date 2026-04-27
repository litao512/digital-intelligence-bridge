# 客户端升级助手实施计划

> **执行要求：**按任务逐项实施，并在每个关键步骤后运行对应验证。

**目标：**为 DIB portable 客户端增加“立即升级”闭环。

**架构：**主客户端只负责下载、校验、启动外置升级助手并退出。升级助手在独立进程中等待主程序退出，解压 zip 到临时目录，覆盖客户端目录，再启动新版主程序。

**技术栈：**.NET 10、Avalonia、Prism DelegateCommand、xUnit。

---

### Task 1: 升级助手参数与执行器

**Files:**
- Create: `DibClient.Updater/DibClient.Updater.csproj`
- Create: `DibClient.Updater/Program.cs`
- Create: `DibClient.Updater/UpgradeOptions.cs`
- Create: `DibClient.Updater/UpgradeExecutor.cs`
- Modify: `digital-intelligence-bridge.slnx`

**Steps:**
1. 写失败测试覆盖参数解析和目录覆盖行为。
2. 新增升级助手项目。
3. 实现参数解析、等待进程、解压、覆盖、重启。
4. 串行运行升级助手相关测试。

### Task 2: 主客户端升级服务

**Files:**
- Create: `digital-intelligence-bridge/Services/ClientUpgradeService.cs`
- Modify: `digital-intelligence-bridge/Services/ServiceCollectionExtensions.cs`
- Modify: `digital-intelligence-bridge/digital-intelligence-bridge.csproj`
- Test: `digital-intelligence-bridge.UnitTests/ClientUpgradeServiceTests.cs`

**Steps:**
1. 写失败测试覆盖升级助手参数、临时复制和启动结果。
2. 实现 `IClientUpgradeService` 与 `ClientUpgradeService`。
3. 将升级助手输出复制进主客户端 publish 目录。
4. 串行运行相关测试。

### Task 3: 设置页入口

**Files:**
- Modify: `digital-intelligence-bridge/ViewModels/SettingsViewModel.cs`
- Modify: `digital-intelligence-bridge/Views/SettingsView.axaml`
- Test: `digital-intelligence-bridge.UnitTests/SettingsViewModelClientDownloadTests.cs`

**Steps:**
1. 写失败测试覆盖下载成功后“立即升级”可用。
2. 增加按钮、命令、状态文案。
3. 点击后调用 `IClientUpgradeService`，成功启动升级助手后退出客户端。
4. 串行运行设置页相关测试。

### Task 4: 构建和冒烟验证

**Files:**
- Modify as needed.

**Steps:**
1. 串行构建主项目和测试项目。
2. 运行相关单元测试。
3. 运行一次升级助手临时目录覆盖测试。
4. 打包并确认 publish 目录包含 `DibClient.Updater.exe`。
