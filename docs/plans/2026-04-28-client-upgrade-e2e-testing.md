# 客户端本机协同升级验收实施计划

> **执行要求：** 使用 `superpowers:executing-plans` 按任务逐步实施本计划。

**目标：** 建立 DIB 客户端本机协同升级验收流程，让升级链路问题先在本机沙盒中暴露。

**架构：** 新增一个 PowerShell 脚本负责创建隔离测试目录、解压旧版客户端、启动测试客户端、收集升级日志和验证目标版本。用户仍通过真实 UI 点击检查更新、下载和立即升级，脚本只负责环境和证据。

**技术栈：** PowerShell 7、.NET 发布产物、DIB portable zip、现有 `scripts/publish-release.ps1`。

---

### 任务 1：新增本机协同验收脚本

**文件：**
- 新建：`scripts/test-client-upgrade-e2e.ps1`

**步骤 1：编写脚本参数**

支持：
- `-FromVersion`
- `-ToVersion`
- `-Prepare`
- `-Collect`
- `-SandboxRoot`

**步骤 2：实现 Prepare 模式**

脚本检查 `artifacts/releases/<FromVersion>/dib-win-x64-portable-<FromVersion>.zip` 存在，清理并创建 `.tmp/client-upgrade-e2e/current/`，把旧版 zip 解压到该目录，然后启动 `digital-intelligence-bridge.exe`。

**步骤 3：实现 Collect 模式**

脚本读取 `%LOCALAPPDATA%\DibClient\logs\client-updater.log` 最近内容，检查 `.tmp/client-upgrade-e2e/current/appsettings.json` 的 `Application.Version` 是否等于 `ToVersion`，输出通过或失败摘要。

**步骤 4：增加防误删保护**

只允许删除位于仓库 `.tmp` 下或用户显式传入的 `SandboxRoot` 下的脚本测试目录，避免误删正式目录。

### 任务 2：新增脚本自检

**文件：**
- 新建：`scripts/test-client-upgrade-e2e-script.ps1`

**步骤 1：构造临时 fake release zip**

创建假的 `artifacts/releases/0.0.1/dib-win-x64-portable-0.0.1.zip`，里面包含 `digital-intelligence-bridge.exe` 和 `appsettings.json`。

**步骤 2：运行 Prepare**

执行 `scripts/test-client-upgrade-e2e.ps1 -FromVersion 0.0.1 -ToVersion 0.0.2 -Prepare -NoLaunch`，验证 `current/appsettings.json` 已解压。

**步骤 3：模拟升级结果并运行 Collect**

把 `current/appsettings.json` 改成 `0.0.2`，运行 `-Collect`，验证脚本返回成功。

### 任务 3：新增运维文档

**文件：**
- 新建：`docs/05-operations/CLIENT_UPGRADE_E2E_TESTING.md`

**步骤 1：说明测试定位**

说明它是“本机协同验收”，位于单元测试和远端机器验收之间。

**步骤 2：写清标准流程**

记录：
1. 准备旧版包。
2. 发布新版包。
3. Prepare 启动旧版。
4. 用户在 UI 点击升级。
5. Collect 收集日志和判断版本。

**步骤 3：写清失败排查**

列出升级日志、版本号、进程退出、路径和权限检查点。

### 任务 4：验证并提交

**文件：**
- 修改：`scripts/test-client-upgrade-e2e.ps1`
- 修改：`scripts/test-client-upgrade-e2e-script.ps1`
- 修改：`docs/05-operations/CLIENT_UPGRADE_E2E_TESTING.md`

**步骤 1：运行脚本自检**

运行：
```powershell
pwsh -File .\scripts\test-client-upgrade-e2e-script.ps1
```

预期：输出 `client upgrade e2e script tests passed.`

**步骤 2：运行相关单元测试**

运行：
```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~ClientUpgradeServiceTests|FullyQualifiedName~ClientUpdaterExecutorTests" -v minimal
```

预期：测试通过。

**步骤 3：提交**

提交：
```powershell
git add scripts\test-client-upgrade-e2e.ps1 scripts\test-client-upgrade-e2e-script.ps1 docs\05-operations\CLIENT_UPGRADE_E2E_TESTING.md docs\plans\2026-04-28-client-upgrade-e2e-testing.md
git commit -m "test: 增加客户端本机升级验收脚本"
```
