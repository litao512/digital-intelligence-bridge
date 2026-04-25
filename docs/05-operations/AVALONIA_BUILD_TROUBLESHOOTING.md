# Avalonia 构建排障说明

## 1. 适用范围

本文档用于说明当前仓库在本地执行 `dotnet build`、`dotnet test` 时，和 Avalonia 相关的已知构建问题、根因以及推荐处理方式。

适用项目：

- `digital-intelligence-bridge/`
- `digital-intelligence-bridge.UnitTests/`
- `DigitalIntelligenceBridge.Plugin.Abstractions/`
- `DigitalIntelligenceBridge.Plugin.Host/`
- `plugins-src/*`

## 2. 当前已知问题

### 2.1 Avalonia telemetry 导致构建前失败

问题现象：

- `dotnet build` 失败
- 错误来自 `AvaloniaStatsTask`
- 常见报错包含：
  - `buildtasks.log`
  - `Access is denied`
  - `AvaloniaStatsTask`

根因：

- Avalonia 的构建期 telemetry 会尝试写入：
  - `C:\Users\<User>\AppData\Local\AvaloniaUI\BuildServices\buildtasks.log`
- 在受限环境中，该目录可能没有写权限
- 结果是项目在 `CoreCompile` 之前就失败，容易被误判为 SDK、NuGet 或业务代码问题

当前仓库的处理方式：

- 根目录 [Directory.Build.targets](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/Directory.Build.targets) 已覆盖 `AvaloniaStats` 目标
- 目的仅是禁用这段构建期 telemetry
- 不影响正常编译产物

说明：

- 如果后续移除该文件，或升级 Avalonia 后重新引入同类 telemetry 目标，构建问题可能再次出现

### 2.2 Avalonia 资源缓存文件锁

问题现象：

- `dotnet build` 或 `dotnet test` 失败
- 错误来自 `GenerateAvaloniaResourcesTask`
- 常见报错包含：
  - `Resources.Inputs.cache`
  - `Avalonia\\resources`
  - `because it is being used by another process`

根因：

- Avalonia 在 `obj\Debug\net10.0\Avalonia\` 下生成资源缓存
- 如果多个 Avalonia 项目或命令并行执行，会争抢这些缓存文件
- 表现为文件被占用，或者出现“无明确编译错误但退出码非零”的构建失败

结论：

- 当前仓库的 Avalonia 项目不适合并行执行 `build` 和 `test`
- 特别不要在同一工作区同时跑：
  - 一个 `dotnet build`
  - 一个 `dotnet test`
  - 多个 Avalonia 项目的并行验证命令

## 3. 推荐验证方式

### 3.1 推荐命令

先构建主应用：

```powershell
dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal
```

再构建单元测试：

```powershell
dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal
```

最后执行测试：

```powershell
dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-build -v minimal
```

### 3.2 执行原则

必须遵守：

1. 顺序执行，不并行
2. 使用 `-m:1`，避免多节点并发放大资源文件锁问题
3. 在已经构建完成后，再使用 `--no-build` 跑测试

不要这样做：

1. 同时开两个终端并发跑 `dotnet build` 和 `dotnet test`
2. 把多个 Avalonia 项目的构建命令放在并行任务里一起跑
3. 在构建失败后，先猜测是源码错误，而不先看 Avalonia 任务报错

## 4. 遇到“无错误失败”时的排障顺序

如果 `dotnet build` 显示：

- `生成失败`
- `0 个警告`
- `0 个错误`

不要先改代码。先按下面顺序排查：

1. 单独构建被引用项目

示例：

```powershell
dotnet build DigitalIntelligenceBridge.Plugin.Abstractions/DigitalIntelligenceBridge.Plugin.Abstractions.csproj --no-restore -v minimal
dotnet build DigitalIntelligenceBridge.Plugin.Host/DigitalIntelligenceBridge.Plugin.Host.csproj --no-restore -v minimal
```

2. 查看是否命中 Avalonia telemetry 权限问题

重点关键词：

- `AvaloniaStatsTask`
- `buildtasks.log`
- `Access is denied`

3. 查看是否命中 Avalonia 资源缓存文件锁

重点关键词：

- `GenerateAvaloniaResourcesTask`
- `Resources.Inputs.cache`
- `Avalonia\\resources`
- `being used by another process`

4. 如果日志仍不明确，再降到更小粒度

示例：

```powershell
dotnet msbuild digital-intelligence-bridge/digital-intelligence-bridge.csproj /t:GetTargetFrameworks /v:minimal
dotnet msbuild digital-intelligence-bridge/digital-intelligence-bridge.csproj /t:Compile /v:diag
```

5. 只有在确认不是构建环境问题后，才继续怀疑源码或依赖配置

## 5. 本仓库的固定约束

从当前版本开始，默认遵循以下约束：

1. Avalonia telemetry 通过 `Directory.Build.targets` 禁用
2. 本地 Avalonia 构建验证采用串行执行
3. 构建和测试命令优先带 `-m:1`
4. 测试优先采用“先 build，再 `--no-build` test”的方式

## 6. 相关文件

- [Directory.Build.targets](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/Directory.Build.targets)
- [README.md](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/digital-intelligence-bridge/README.md)
- [2026-04-14-build-troubleshooting-retrospective.md](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/docs/plans/2026-04-14-build-troubleshooting-retrospective.md)
