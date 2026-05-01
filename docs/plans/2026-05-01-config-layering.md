# DIB 客户端配置分层实施计划

> 执行提示：按任务顺序逐项实施，并在每个验证点记录结果。

**目标：** 收敛 DIB 客户端程序目录和用户目录 `appsettings.json` 的职责边界，并用测试防止 `ReleaseCenter.AnonKey`、站点资料和用户配置归一化互相污染。

**架构：** 保持当前“程序配置先加载、用户配置后覆盖”的架构。用户配置模型继续作为白名单，只保存本机可变项；程序配置保留默认连接配置。通过测试和轻量诊断补强边界，而不是引入新的配置系统。

**技术栈：** .NET 10、Microsoft.Extensions.Configuration、System.Text.Json、xUnit。

---

### 任务 1：覆盖最终合并配置中的 `ReleaseCenter.AnonKey`

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/ConfigurationExtensionsTests.cs`

**步骤 1：编写测试**

在 `ConfigurationExtensionsTests` 中新增测试：

```csharp
[Fact]
public void AddAppConfiguration_ShouldUseDefaultReleaseCenterAnonKey_WhenUserConfigOmitsAnonKey()
{
    using var sandbox = new TestConfigSandbox();
    var userConfigPath = ConfigurationExtensions.GetConfigFilePath();
    File.WriteAllText(
        userConfigPath,
        """
        {
          "Application": { "MinimizeToTray": true, "StartWithSystem": false },
          "Tray": { "ShowNotifications": true },
          "ReleaseCenter": {
            "Enabled": true,
            "BaseUrl": "http://101.42.19.26:8000",
            "Channel": "stable",
            "SiteId": "site-001",
            "SiteName": "LITAO"
          }
        }
        """);

    var services = new ServiceCollection();
    services.AddAppConfiguration();
    using var provider = services.BuildServiceProvider();

    var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;

    Assert.Equal("site-001", settings.ReleaseCenter.SiteId);
    Assert.Equal("LITAO", settings.ReleaseCenter.SiteName);
    Assert.False(string.IsNullOrWhiteSpace(settings.ReleaseCenter.AnonKey));
}
```

**步骤 2：运行测试验证行为**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal --filter AddAppConfiguration_ShouldUseDefaultReleaseCenterAnonKey_WhenUserConfigOmitsAnonKey
```

预期：如果当前合并行为已经正确，则测试通过；如果失败，再进入任务 2 的实现步骤。

### 任务 2：保留用户配置白名单行为

**文件：**
- 修改：`digital-intelligence-bridge.UnitTests/ConfigurationExtensionsTests.cs`
- 按需修改：`digital-intelligence-bridge/Configuration/ConfigurationExtensions.cs`

**步骤 1：编写或调整测试**

确认现有测试断言：

```csharp
Assert.False(releaseCenter.TryGetProperty("AnonKey", out var _));
Assert.False(document.RootElement.TryGetProperty("Plugin", out var _));
Assert.False(document.RootElement.TryGetProperty("Logging", out var _));
```

同时断言用户配置仍包含本机覆盖字段：

```csharp
Assert.True(releaseCenter.TryGetProperty("Enabled", out var _));
Assert.True(releaseCenter.TryGetProperty("BaseUrl", out var _));
Assert.True(releaseCenter.TryGetProperty("Channel", out var _));
Assert.True(releaseCenter.TryGetProperty("SiteId", out var _));
```

**步骤 2：运行聚焦测试**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal --filter ConfigurationExtensionsTests
```

预期：配置相关测试通过。

### 任务 3：为缺失发布中心 `AnonKey` 增加明确诊断

**文件：**
- 修改：`digital-intelligence-bridge/Services/ReleaseCenterService.cs`
- 测试：`digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`

**步骤 1：编写失败测试**

增加测试，证明 `GetAuthorizedResourcesAsync` 在 `ReleaseCenter.AnonKey` 缺失时返回空快照，并写入警告日志。

复用 `ReleaseCenterServiceTests.cs` 中已有的日志或测试辅助模式。断言应检查等价于以下内容的警告文本：

```text
ReleaseCenter.AnonKey 未配置，已跳过授权资源刷新。
```

**步骤 2：运行聚焦测试**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal --filter GetAuthorizedResourcesAsync
```

预期：如果尚未实现警告日志，则测试失败。

**步骤 3：实现最小日志提示**

在 `ReleaseCenterService.GetAuthorizedResourcesAsync` 中拆分保护逻辑：

```csharp
if (!IsConfigured)
{
    return new AuthorizedResourceSnapshot();
}

if (string.IsNullOrWhiteSpace(_config.AnonKey))
{
    _logger.LogWarning("ReleaseCenter.AnonKey 未配置，已跳过授权资源刷新。");
    return new AuthorizedResourceSnapshot();
}
```

**步骤 4：再次运行测试**

运行同一个聚焦测试。

预期：测试通过。

### 任务 4：记录运维说明

**文件：**
- 修改：`docs/05-operations/PLUGIN_RELEASE_PUBLISH_RUNBOOK.md`，或在现有运维文档中增加一个简短章节。

**步骤 1：增加简明说明**

记录：

- 程序目录 `appsettings.json` 存放发布中心连接配置和 `ReleaseCenter.AnonKey`。
- 用户目录 `appsettings.json` 存放 `SiteId`、`SiteName`、`SiteRemark` 和本机偏好。
- 如果 `%LocalAppData%\DibClient\resource-cache` 不存在，优先检查最终合并配置里 `ReleaseCenter.AnonKey` 是否生效。

**步骤 2：运行文档语言检查**

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-doc-lang.ps1
```

预期：检查通过。

### 任务 5：完整验证

**文件：**
- 除测试和文档外，预期不新增文件。

**步骤 1：构建主项目**

运行：

```powershell
dotnet build digital-intelligence-bridge\digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
```

预期：0 个错误。

**步骤 2：构建测试项目**

运行：

```powershell
dotnet build digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
```

预期：0 个错误。

**步骤 3：运行测试**

运行：

```powershell
dotnet test digital-intelligence-bridge.UnitTests\digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

预期：全部测试通过。

**步骤 4：检查差异**

运行：

```powershell
git diff -- digital-intelligence-bridge digital-intelligence-bridge.UnitTests docs
```

预期：变更限定在配置分层测试、可选警告日志和文档内。
