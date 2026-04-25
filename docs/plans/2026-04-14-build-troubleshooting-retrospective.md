# 构建排障经验归档

## 1. 问题现象

在当前仓库执行 `dotnet build`、`dotnet test` 时，曾出现以下两类误导性表现：

1. 直接显示“生成失败”，但日志里没有常规 C# 编译错误
2. 在并行执行 `build` 和 `test` 时，偶发出现 Avalonia 资源文件被占用

这些现象容易让人误判为：

1. SDK 版本不兼容
2. NuGet 还原失败
3. 新提交的源码存在语法错误

## 2. 根因拆分

本次排障确认了两层独立问题。

### 2.1 第一层：Avalonia telemetry 写权限失败

根因：

- `AvaloniaStatsTask` 会写 `LocalAppData\AvaloniaUI\BuildServices\buildtasks.log`
- 当前受限环境无法写该路径
- 导致构建在编译前失败

### 2.2 第二层：Avalonia 资源缓存文件锁

根因：

- `GenerateAvaloniaResourcesTask` 会在 `obj\...\Avalonia\` 下读写缓存
- 并行执行多个 Avalonia 构建/测试命令时，缓存文件容易被占用

## 3. 已落地修复

本次已在仓库中固化：

1. 新增 [Directory.Build.targets](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/Directory.Build.targets)，覆盖 `AvaloniaStats` 目标，禁用构建期 telemetry
2. 新增正式文档 [AVALONIA_BUILD_TROUBLESHOOTING.md](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/docs/05-operations/AVALONIA_BUILD_TROUBLESHOOTING.md)
3. 在 [README.md](C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/digital-intelligence-bridge/README.md) 中补充本地验证建议

## 4. 后续执行约束

从这次开始，本仓库本地验证遵循：

1. 不并行执行 Avalonia 项目的 `build` 和 `test`
2. 优先使用 `-m:1`
3. 优先顺序执行“先 build，再 `--no-build` test”
4. 遇到“无错误失败”时，先检查 Avalonia telemetry 和 Avalonia 资源缓存文件锁

## 5. 经验结论

这类问题的关键不是“先改代码”，而是先判断：

1. 是不是构建工具链前置任务失败
2. 是不是 Avalonia 的附加构建任务失败
3. 是不是并发导致的文件锁问题

只有排除这三类问题后，才值得继续怀疑业务代码。
