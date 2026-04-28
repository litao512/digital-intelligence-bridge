# 插件本机协同升级验收实施计划

> **执行要求：** 使用 `superpowers:executing-plans` 按任务逐步实施本计划。

**目标：** 建立 DIB 插件本机协同升级验收流程，让插件下载、预安装、激活、重启加载问题先在本机沙盒中暴露。

**架构：** 新增 PowerShell 脚本负责创建隔离客户端目录和独立 `DIB_CONFIG_ROOT`，可选植入旧版插件运行时目录，启动客户端后由用户通过真实 UI 触发插件更新。升级后脚本收集缓存、预安装、运行时插件、备份和日志证据。

**技术栈：** PowerShell 7、DIB portable 客户端包、插件发布产物、现有 `scripts/publish-plugin-release.ps1` 和 `scripts/test-client-upgrade-e2e.ps1` 的脚本风格。

---

### 任务 1：新增插件协同验收脚本

**文件：**
- 新建：`scripts/test-plugin-upgrade-e2e.ps1`

**步骤 1：编写脚本参数**

支持：
- `-ClientVersion`
- `-PluginCode`
- `-FromPluginVersion`
- `-ToPluginVersion`
- `-Prepare`
- `-Collect`
- `-SandboxRoot`
- `-NoLaunch`

**步骤 2：实现 Prepare 模式**

脚本解压 `artifacts/releases/<ClientVersion>/dib-win-x64-portable-<ClientVersion>.zip` 到 `.tmp/plugin-upgrade-e2e/current/`，创建 `.tmp/plugin-upgrade-e2e/config-root/`，并使用 `DIB_CONFIG_ROOT` 启动客户端。

**步骤 3：支持植入旧插件版本**

当提供 `-FromPluginVersion` 时，从 `artifacts/plugin-releases/<PluginCode>/<FromPluginVersion>/publish/` 复制旧插件到 `config-root/plugins/<PluginCode>/`。

**步骤 4：实现 Collect 模式**

检查 `config-root/plugins/<PluginCode>/plugin.json` 的版本是否等于 `ToPluginVersion`，同时收集 `release-cache`、`release-staging`、`release-backups`、`logs` 的证据摘要。

### 任务 2：新增脚本自检

**文件：**
- 新建：`scripts/test-plugin-upgrade-e2e-script.ps1`

**步骤 1：构造 fake 客户端 zip**

创建假的客户端发布包，包含 `digital-intelligence-bridge.exe`、`appsettings.json` 和最小目录结构。

**步骤 2：构造 fake 插件旧版本**

创建 `artifacts/plugin-releases/patient-registration/0.0.1-e2e-plugin-test/publish/plugin.json`。

**步骤 3：运行 Prepare 和 Collect**

运行 Prepare 后模拟插件升级结果，把运行时 `plugin.json` 改为目标版本，再运行 Collect，验证脚本返回通过。

### 任务 3：新增运维文档

**文件：**
- 新建：`docs/05-operations/PLUGIN_UPGRADE_E2E_TESTING.md`

**步骤 1：说明测试定位**

说明插件本机协同验收位于插件单元测试和远端机器验收之间。

**步骤 2：写清标准流程**

记录旧插件准备、新插件发布、Prepare 启动客户端、用户触发插件更新、Collect 收集证据。

**步骤 3：写清失败排查**

列出插件 manifest、缓存、预安装、激活、重启加载和插件中心显示的排查点。

### 任务 4：验证并提交

**文件：**
- 修改：`scripts/test-plugin-upgrade-e2e.ps1`
- 修改：`scripts/test-plugin-upgrade-e2e-script.ps1`
- 修改：`docs/05-operations/PLUGIN_UPGRADE_E2E_TESTING.md`

**步骤 1：运行脚本自检**

运行：

```powershell
pwsh -File .\scripts\test-plugin-upgrade-e2e-script.ps1
```

预期：输出 `plugin upgrade e2e script tests passed.`

**步骤 2：运行相关单元测试**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~ReleaseCenterDownloadTests|FullyQualifiedName~ReleaseCenterPrepareInstallTests|FullyQualifiedName~ReleaseCenterActivatePreparedTests|FullyQualifiedName~PluginCenterViewModelTests" -v minimal
```

预期：测试通过。

**步骤 3：提交**

提交：

```powershell
git add scripts\test-plugin-upgrade-e2e.ps1 scripts\test-plugin-upgrade-e2e-script.ps1 docs\05-operations\PLUGIN_UPGRADE_E2E_TESTING.md docs\plans\2026-04-28-plugin-upgrade-e2e-testing.md
git commit -m "test: 增加插件本机升级验收脚本"
```
