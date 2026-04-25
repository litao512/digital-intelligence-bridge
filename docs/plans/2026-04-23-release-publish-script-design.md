# Release 发布脚本设计

## 背景

当前仓库发布主程序时，需要自动把 `plugins-src/` 下的插件项目重新构建并同步到仓库根 `plugins/` 本地中转目录。该目录用于产包阶段组装 `publish/plugins/`，已被 `.gitignore` 忽略，不作为源码提交内容。

## 目标

提供一个统一的 Release 发布脚本，默认完成以下动作：

1. 还原主程序与插件依赖
2. 构建并同步 `plugins-src/` 到仓库根 `plugins/` 本地中转目录
3. 发布主程序 `Release`
4. 生成完整发布目录
5. 生成 zip 分发包

## 方案

采用单个 PowerShell 脚本 `scripts/publish-release.ps1`：

- 默认发布 `digital-intelligence-bridge/digital-intelligence-bridge.csproj`
- 默认运行时标识为 `win-x64`
- 默认输出到 `artifacts/releases/<version>/`
- 默认将仓库根 `plugins/` 本地中转目录打入发布目录
- 在打包前先遍历 `plugins-src/*.Plugin` 项目并构建、同步到 `plugins/<plugin-id>`

## 关键规则

- 插件同步来源是 `plugins-src/<PluginName>.Plugin/bin/Release/net10.0/`
- 插件目标目录是仓库根 `plugins/<plugin-id>` 本地中转目录
- 同步时保留目标目录已有的 `plugin.settings.json`
- 正式发布目录包含主程序安装目录 `appsettings.json`
- 不打包 `%LocalAppData%` 运行时目录、日志、缓存、备份目录
- 不提交仓库根 `plugins/` 与 `artifacts/releases/` 生成目录

## 风险与取舍

- 当前仓库没有现成的脚本自动化测试框架，因此本次以脚本执行验证为主
- 插件项目命名依赖 `<PluginId>.Plugin` 约定；若后续出现不符合该约定的新插件，需要补参数或映射逻辑
