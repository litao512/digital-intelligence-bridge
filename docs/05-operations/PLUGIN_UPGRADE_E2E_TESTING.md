# 插件本机协同升级验收

本文说明如何在本机隔离环境中，使用真实客户端 UI 和真实发布中心验证插件升级流程。

## 定位

插件升级建议分三层验证：

1. 单元测试：验证插件包下载、预安装、激活、回滚等底层逻辑。
2. 本机协同验收：在隔离 `DIB_CONFIG_ROOT` 中运行客户端，由人工点击真实 UI，验证插件中心或设置页触发的真实升级链路。
3. 远端机器验收：只在关键版本发布前做最终确认。

本机协同验收的目标是提前发现插件 manifest、插件包、缓存、激活、重启加载等问题。

## 目录约定

默认测试目录：

```text
.tmp/plugin-upgrade-e2e/
```

主要子目录：

```text
.tmp/plugin-upgrade-e2e/current/
.tmp/plugin-upgrade-e2e/config-root/
.tmp/plugin-upgrade-e2e/evidence/
```

脚本启动客户端时会设置：

```text
DIB_CONFIG_ROOT=.tmp/plugin-upgrade-e2e/config-root
```

因此插件缓存、预安装目录、运行时插件目录、备份目录和日志都会留在沙盒配置根目录中。

## 标准流程

### 1. 确认客户端包存在

本机需要有一个可运行客户端包：

```text
artifacts/releases/<ClientVersion>/dib-win-x64-portable-<ClientVersion>.zip
```

### 2. 准备旧插件版本

如果要验证“旧版本升级到新版本”，本机需要有旧插件发布目录：

```text
artifacts/plugin-releases/<plugin-code>/<FromPluginVersion>/publish/
```

该目录中必须包含 `plugin.json` 和插件主 DLL。

如果不传 `-FromPluginVersion`，脚本不会植入旧插件，适合验证“首次安装插件”的流程。

### 3. 发布新插件版本

按 `docs/05-operations/PLUGIN_RELEASE_PUBLISH_RUNBOOK.md` 发布 `<ToPluginVersion>`，并确认 `plugin-manifest.json` 已指向新插件包。

### 4. 准备并启动沙盒客户端

```powershell
pwsh -File .\scripts\test-plugin-upgrade-e2e.ps1 `
  -ClientVersion <ClientVersion> `
  -PluginCode patient-registration `
  -FromPluginVersion <FromPluginVersion> `
  -ToPluginVersion <ToPluginVersion> `
  -Prepare
```

脚本会：

- 清理并创建测试目录。
- 解压客户端包到 `current/`。
- 创建独立 `config-root/`。
- 可选植入旧插件版本到 `config-root/plugins/<plugin-code>/`。
- 使用 `DIB_CONFIG_ROOT` 启动客户端。

### 5. 人工操作 UI

在启动的客户端中执行插件升级流程。当前可用入口包括：

- 插件中心的检查或更新操作。
- 设置页中的插件包下载、预安装、激活操作。
- 必要时重启客户端，让运行时重新加载插件。

完成后确认 UI 中插件版本显示为 `<ToPluginVersion>`。

### 6. 收集验收证据

```powershell
pwsh -File .\scripts\test-plugin-upgrade-e2e.ps1 `
  -ClientVersion <ClientVersion> `
  -PluginCode patient-registration `
  -FromPluginVersion <FromPluginVersion> `
  -ToPluginVersion <ToPluginVersion> `
  -Collect
```

脚本会检查：

- `config-root/plugins/<plugin-code>/plugin.json` 的 `version` 是否等于 `<ToPluginVersion>`。
- 运行时插件目录是否存在。
- 插件主 DLL 是否存在。
- 插件缓存目录是否有 zip。
- 预安装目录是否曾出现 `plugin.json`。
- 备份目录是否存在旧插件备份。
- 沙盒日志中是否出现失败、异常或插件加载错误。

证据文件：

```text
.tmp/plugin-upgrade-e2e/evidence/summary.json
.tmp/plugin-upgrade-e2e/evidence/logs-tail.txt
```

## 失败排查顺序

| 现象 | 重点排查 |
|---|---|
| 没发现新插件版本 | `plugin-manifest.json`、插件版本排序、渠道、插件编码 |
| 下载失败 | `packageUrl`、公开 zip、SHA256、网络 |
| 预安装失败 | zip 结构、`plugin.json`、依赖 DLL 是否随包 |
| 激活失败 | 运行时插件目录权限、旧插件是否被占用、备份目录 |
| UI 版本未变化 | 是否已重启、插件中心是否读取运行时目录、`plugin.json.version` |
| 插件加载失败 | `.deps.json`、依赖 DLL、入口类型、入口程序集 |

## 脚本自检

修改脚本后运行：

```powershell
pwsh -File .\scripts\test-plugin-upgrade-e2e-script.ps1
```

预期输出：

```text
plugin upgrade e2e script tests passed.
```

## 注意事项

- 每轮测试建议使用递增插件版本。
- 若旧客户端或旧插件本身存在已知缺陷，先手工切到修复后的基线版本，再测试它升级到下一版。
- 本流程验证的是插件升级，不替代客户端整包升级验收。
- 本机协同验收通过后，再安排远端机器做最终确认。
