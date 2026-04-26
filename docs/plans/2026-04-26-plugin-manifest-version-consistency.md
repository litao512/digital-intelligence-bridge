# 插件清单版本一致性实施计划

> **执行要求：** 实施时使用 `superpowers:executing-plans`，按任务逐项执行并在每项后验证。

**目标：** 让病人登记插件模块自报版本与发布包 `plugin.json.version` 保持一致。

**架构：** 发布包内 `plugin.json` 是插件发布版本的事实来源。`PatientRegistrationPlugin.GetManifest()` 优先读取程序集同目录的 `plugin.json`，失败时回退到内置清单；发布脚本测试负责验证发布包清单和模块自报清单一致。

**技术栈：** PowerShell 7、.NET 10、C#、插件发布脚本、插件抽象接口。

---

### 任务 1：扩展发布脚本测试

**文件：**
- 修改：`scripts/test-publish-plugin-release.ps1`

**步骤 1：写失败测试**

在测试发布完成后读取 `publish/plugin.json`，断言 `version` 等于测试版本。再生成临时 .NET 控制台程序，引用发布目录中的 `DigitalIntelligenceBridge.Plugin.Abstractions.dll`，从 `PatientRegistration.Plugin.dll` 创建 `PatientRegistration.Plugin.PatientRegistrationPlugin` 实例，调用 `GetManifest()`，断言返回版本等于测试版本。

**步骤 2：运行测试确认失败**

运行：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\test-publish-plugin-release.ps1
```

预期：测试失败，错误指出 `GetManifest()` 返回 `0.1.0`，期望为 `0.1.0-test`。

### 任务 2：修改病人登记插件清单读取

**文件：**
- 修改：`plugins-src/PatientRegistration.Plugin/PatientRegistrationPlugin.cs`

**步骤 1：实现最小修复**

新增私有方法从 `typeof(PatientRegistrationPlugin).Assembly.Location` 所在目录读取 `plugin.json`，使用大小写不敏感的 JSON 反序列化得到 `PluginManifest`。清单为空或关键字段缺失时返回 `null`。

**步骤 2：保留回退清单**

将当前硬编码对象移动为 `CreateFallbackManifest()`，`GetManifest()` 返回 `TryReadPackagedManifest() ?? CreateFallbackManifest()`。

### 任务 3：验证

**文件：**
- 验证：`scripts/test-publish-plugin-release.ps1`
- 验证：`plugins-src/PatientRegistration.Plugin/PatientRegistrationPlugin.cs`

**步骤 1：运行发布脚本测试**

运行：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\test-publish-plugin-release.ps1
```

预期：通过，并输出发布脚本测试通过。

**步骤 2：运行文档语言检查**

运行：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -File .\scripts\check-doc-lang.ps1
```

预期：通过。

### 任务 4：测试环境同版本修复和升级烟测

**文件：**
- 使用：`scripts/publish-plugin-release.ps1`
- 使用：`dib-release-center/`

**步骤 1：重建 `1.0.3-dev.1` 插件包**

运行插件发布脚本，生成新的 `artifacts/plugin-releases/patient-registration/1.0.3-dev.1/` 产物。

**步骤 2：覆盖测试环境资产**

将新压缩包上传到发布中心同一路径，更新 `release_assets.sha256`、`size_bytes` 和相关 `plugin_versions` 记录，重新发布 `plugin-manifest.json`。

**步骤 3：运行升级烟测**

用隔离目录执行插件下载、预安装、激活和宿主加载，断言运行时清单和 `GetManifest()` 版本均为 `1.0.3-dev.1`。
