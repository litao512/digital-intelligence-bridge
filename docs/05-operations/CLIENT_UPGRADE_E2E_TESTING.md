# 客户端本机协同升级验收

本文说明如何在本机搭建隔离目录，使用真实客户端 UI 和真实发布中心验证客户端自动升级流程。

## 定位

客户端升级建议分三层验证：

1. 单元测试：验证参数转义、升级器覆盖、日志保留等底层逻辑。
2. 本机协同验收：在隔离目录运行旧版客户端，由人工点击真实 UI，验证真实发布中心下载和升级。
3. 远端机器验收：只在关键版本发布前做最终确认。

本机协同验收的目标是提前发现大部分升级链路问题，减少跨机器反复测试。

## 目录约定

默认测试目录：

```text
.tmp/client-upgrade-e2e/
```

主要子目录：

```text
.tmp/client-upgrade-e2e/current/
.tmp/client-upgrade-e2e/downloads/
.tmp/client-upgrade-e2e/evidence/
```

脚本只会清理默认 `.tmp` 测试目录，或名称包含 `upgrade`、`e2e`、`sandbox`、`test` 的显式测试目录，避免误删正式客户端目录。

## 标准流程

### 1. 确认旧版包存在

旧版包必须已经在本地产物目录中：

```text
artifacts/releases/<FromVersion>/dib-win-x64-portable-<FromVersion>.zip
```

如果本地没有旧版包，可以重新运行：

```powershell
pwsh -File .\scripts\publish-release.ps1 -Version <FromVersion>
```

### 2. 发布新版

使用客户端发布流程发布 `<ToVersion>`，并确认 stable manifest 已指向新版。

### 3. 准备并启动旧版客户端

```powershell
pwsh -File .\scripts\test-client-upgrade-e2e.ps1 `
  -FromVersion <FromVersion> `
  -ToVersion <ToVersion> `
  -Prepare
```

脚本会：

- 清理并创建测试目录。
- 解压旧版客户端到 `current/`。
- 校验旧版 `appsettings.json` 版本号。
- 启动旧版客户端。

### 4. 人工操作 UI

在启动的客户端中执行：

1. 打开设置页。
2. 点击 `检查更新`。
3. 确认发现 `<ToVersion>`。
4. 点击 `下载客户端更新包`。
5. 下载完成后点击 `立即升级`。
6. 等待客户端退出、升级器覆盖文件并重启。

### 5. 收集验收证据

升级完成或失败后运行：

```powershell
pwsh -File .\scripts\test-client-upgrade-e2e.ps1 `
  -FromVersion <FromVersion> `
  -ToVersion <ToVersion> `
  -Collect
```

脚本会检查：

- `current/appsettings.json` 的 `Application.Version` 是否等于 `<ToVersion>`。
- `digital-intelligence-bridge.exe` 是否存在。
- `DibClient.Updater.exe` 是否存在。
- `plugins/` 是否存在。
- 本次测试开始后的升级器日志中是否出现 `升级失败`。

证据文件：

```text
.tmp/client-upgrade-e2e/evidence/summary.json
.tmp/client-upgrade-e2e/evidence/client-updater-tail.log
```

## 失败排查顺序

优先查看：

```powershell
Get-Content "$env:LOCALAPPDATA\DibClient\logs\client-updater.log" -Tail 80
```

常见失败点：

| 现象 | 重点排查 |
|---|---|
| 没发现新版本 | 发布中心 manifest、客户端当前版本、发布渠道 |
| 下载失败 | `ReleaseCenter.BaseUrl`、网络、zip 公开地址、SHA256 |
| `立即升级` 灰色 | 升级服务注册、下载包路径、文件是否存在 |
| 日志显示客户端目录不存在 | 升级器参数解析、路径转义、当前运行目录 |
| 日志显示升级包不存在 | 下载包路径是否被清理或传参错误 |
| 日志显示文件被占用 | 客户端是否退出、托盘进程、杀毒软件、权限 |
| 文件覆盖成功但未重启 | 主程序路径、`--restart` 参数、Windows 执行权限 |

## 脚本自检

修改脚本后运行：

```powershell
pwsh -File .\scripts\test-client-upgrade-e2e-script.ps1
```

预期输出：

```text
client upgrade e2e script tests passed.
```

## 注意事项

- 不要把测试目录放在正式安装目录、`Program Files` 或源码根目录。
- 每轮测试建议使用递增版本，例如 `1.0.9 -> 1.0.10`。
- 如果旧版本本身的升级启动逻辑有缺陷，需要先手工运行修复后的版本，再验证它升级到下一个版本。
- 本机协同验收通过后，再安排远端机器做最终确认。
