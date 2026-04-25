# 插件开发约定

## 1. 目的

本文档用于统一当前仓库中的外部插件开发方式，避免后续新增第二、第三个插件时出现目录混乱、命名不一致、运行时发布结构不统一的问题。

当前仓库已经具备第一版外部插件宿主能力，因此后续新增插件时应遵循本文档，而不是继续把插件业务直接塞入宿主主项目。

## 2. 仓库整体分层

当前仓库建议按以下职责理解：

- `digital-intelligence-bridge/`
  - 宿主主程序
  - 负责窗口、导航、插件发现、插件加载、菜单接入、页面承载、失败隔离
- `DigitalIntelligenceBridge.Plugin.Abstractions/`
  - 插件公共契约
  - 放宿主和插件共享的最小接口与清单模型
- `DigitalIntelligenceBridge.Plugin.Host/`
  - 插件宿主基础设施
  - 放插件扫描、清单读取、DLL 加载、`AssemblyLoadContext`
- `plugins-src/`
  - 所有插件源码根目录
  - 每个插件一个独立项目
- `plugins/`
  - 本地产包中转目录
  - 由发布脚本从 `plugins-src/` 构建输出同步生成
  - 已被 `.gitignore` 忽略，不作为源码提交内容

## 3. 插件目录约定

### 3.1 源码目录

每个插件源码项目都放在：

```text
plugins-src/<PluginName>.Plugin/
```

例如：

- `plugins-src/MedicalDrugImport.Plugin/`
- `plugins-src/PatientManagement.Plugin/`
- `plugins-src/ScheduleAssistant.Plugin/`

### 3.2 本地产包中转目录

每个插件的本地产包中转目录都放在：

```text
plugins/<PluginName>/
```

例如：

- `plugins/MedicalDrugImport/`
- `plugins/PatientManagement/`
- `plugins/ScheduleAssistant/`

补充说明：

- 仓库根 `plugins/` 仅用于本地产包和发布脚本中转，不提交到 Git
- 正式运行目录位于 `%LOCALAPPDATA%\\DibClient\\plugins\\<PluginName>`
- 如果设置了 `DIB_CONFIG_ROOT`，正式运行目录位于 `<DIB_CONFIG_ROOT>\\plugins\\<PluginName>`

### 3.3 规则

- 一个插件只对应一个源码项目
- 一个插件只对应一个本地中转目录和一个正式运行目录
- 不允许多个插件共用同一个中转目录或正式运行目录
- 不建议把插件源码直接放进宿主主项目目录

## 4. 命名约定

建议统一使用三套名字：

### 4.1 项目名

格式：

```text
<BusinessName>.Plugin
```

示例：

- `MedicalDrugImport.Plugin`
- `PatientManagement.Plugin`
- `ScheduleAssistant.Plugin`

### 4.2 运行时目录名

格式：

```text
<BusinessName>
```

示例：

- `MedicalDrugImport`
- `PatientManagement`
- `ScheduleAssistant`

### 4.3 插件 ID

格式：

```text
小写短横线
```

示例：

- `medical-drug-import`
- `patient-management`
- `schedule-assistant`

### 4.4 建议映射

- 项目名：`MedicalDrugImport.Plugin`
- 运行时目录：`MedicalDrugImport`
- 插件 ID：`medical-drug-import`

三者要保持稳定映射，避免后续发布和排错困难。

## 5. 插件源码结构建议

每个插件源码项目建议采用下面的目录结构：

```text
plugins-src/
  MedicalDrugImport.Plugin/
    Configuration/
    Models/
    Services/
    ViewModels/
    Views/
    MedicalDrugImportPlugin.cs
    plugin.json
    plugin.settings.json
    plugin.development.json
    MedicalDrugImport.Plugin.csproj
```

说明：

- `Configuration/`
  - 插件自己的配置模型和配置加载器
- `Models/`
  - 插件内部使用的数据模型
- `Services/`
  - Excel、数据库、同步等业务服务
- `ViewModels/`
  - 插件页面的 ViewModel
- `Views/`
  - 插件页面和控件
- `MedicalDrugImportPlugin.cs`
  - 插件入口类，实现 `IPluginModule`
- `plugin.json`
  - 插件清单
- `plugin.settings.json`
  - 插件非敏感本地配置
- `plugin.development.json`
  - 仅开发模式使用的本地敏感资源配置

## 6. 本地中转目录结构建议

本地中转目录和正式运行目录建议采用下面结构：

```text
plugins/
  MedicalDrugImport/
    plugin.json
    plugin.settings.json
    plugin.development.json
    MedicalDrugImport.Plugin.dll
    MedicalDrugImport.Plugin.deps.json
    其他依赖.dll
```

说明：

- `plugin.json`
  - 运行时必须存在
- `plugin.settings.json`
  - 可选
  - 仅用于非敏感本地配置
- `plugin.development.json`
  - 可选
  - 仅在显式开发模式下用于本地敏感资源配置
- `*.dll`
  - 插件入口程序集及其依赖

正式生产资源不从 `plugin.settings.json` 或 `plugin.development.json` 读取，而是由宿主按 `resourceRequirements` 从资源中心下发。

仓库根 `plugins/` 被视为生成目录，不应手工加入 Git 提交。

## 7. plugin.json 约定

建议所有插件统一采用以下字段：

```json
{
  "id": "medical-drug-import",
  "name": "医保药品导入",
  "version": "0.1.0",
  "entryAssembly": "MedicalDrugImport.Plugin.dll",
  "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
  "minHostVersion": "1.0.0",
  "resourceRequirements": [
    {
      "resourceType": "PostgreSQL",
      "usageKey": "business-db",
      "required": true,
      "description": "读取和写入业务数据库"
    }
  ]
}
```

字段说明：

- `id`
  - 插件唯一标识
- `name`
  - 插件显示名
- `version`
  - 插件版本
- `entryAssembly`
  - 入口 DLL 文件名
- `entryType`
  - 入口类全名
- `minHostVersion`
  - 最低宿主版本
- `resourceRequirements`
  - 插件声明的运行时资源需求列表

### 7.1 `resourceRequirements` 约定

建议 `resourceRequirements` 采用以下结构：

```json
[
  {
    "resourceType": "PostgreSQL",
    "usageKey": "business-db",
    "required": true,
    "description": "读取和写入业务数据库"
  },
  {
    "resourceType": "SqlServer",
    "usageKey": "sync-target",
    "required": false,
    "description": "将同步结果写入目标 SQL Server"
  }
]
```

字段说明：

- `resourceType`
  - 资源类型
  - 第一阶段建议使用：`PostgreSQL`、`SqlServer`、`Supabase`、`HttpService`
- `usageKey`
  - 插件内部使用该资源的逻辑别名
  - 要求在同一插件内稳定且唯一
- `required`
  - 是否为必需资源
  - `true` 表示缺失时插件应阻止核心流程
  - `false` 表示缺失时插件可降级运行
- `description`
  - 资源用途说明，便于托盘和后台审批识别

### 7.2 设计原则

- 插件只声明“需要什么资源”，不在 `plugin.json` 中内联连接串、密码、Token 等敏感信息。
- `resourceRequirements` 是宿主发现、申请、授权匹配的输入，不替代本地开发配置文件。
- `resourceRequirements` 是插件正式资源来源建模的唯一入口；正式资源由宿主按授权下发。
- `usageKey` 一旦发布，应尽量保持稳定，避免宿主绑定规则和后台授权映射失效。

## 8. 插件配置约定

插件业务配置不应放进宿主 `appsettings.json`。

建议采用：

1. 宿主授权资源
2. 显式开发模式下的 `plugin.development.json`
3. 插件非敏感默认值

补充约定：

- `plugin.settings.json` 仅用于非敏感本地配置。
- `plugin.development.json` 仅用于显式开发模式下的本地敏感资源配置。
- 正式生产资源不得通过 `plugin.settings.json` 或 `plugin.development.json` 持有或回退。
- 一旦宿主提供资源运行时注入，插件必须优先读取宿主下发资源。
- 不允许把后台审批后的敏感资源配置长期回写到 `plugin.json`。
- 插件不再通过环境变量持有业务资源配置。

### 8.1 `plugin.development.json` 约定

该文件仅用于开发联调，不作为正式发布内容。

建议：

- 文件名固定为 `plugin.development.json`
- 仅在 `DevelopmentMode.Enabled = true` 时读取
- 仅保存本地开发需要的敏感资源
- 不提交真实凭据

示例：

```json
{
  "BusinessDbConnectionString": "<填写本地开发 PostgreSQL 连接串>",
  "SyncTargetConnectionString": "<填写本地开发 SQL Server 连接串>"
}
```

### 8.2 如何启用开发模式

建议按下面顺序操作：

1. 在插件目录的 `plugin.settings.json` 中显式开启：

```json
{
  "DevelopmentMode": {
    "Enabled": true
  }
}
```

2. 在同一目录创建 `plugin.development.json`

位置示例：

- `plugins-src/MedicalDrugImport.Plugin/plugin.development.json`
- `plugins-src/PatientRegistration.Plugin/plugin.development.json`
- 运行时调试时也可以放在实际插件目录下，例如 `%LOCALAPPDATA%\\DibClient\\plugins\\MedicalDrugImport\\plugin.development.json`

3. 按插件填写对应键

`MedicalDrugImport`：

- `BusinessDbConnectionString`
  - 药品导入业务库的 PostgreSQL 连接串
- `SyncTargetConnectionString`
  - 目标 SQL Server 的连接串

`PatientRegistration`：

- `RegistrationDbConnectionString`
  - 就诊登记业务库的 PostgreSQL 连接串

4. 正式发布前关闭开发模式，并移除 `plugin.development.json`

建议：

- 将 `DevelopmentMode.Enabled` 改回 `false`
- 不把 `plugin.development.json` 打进正式包
- 不把真实开发凭据提交到仓库

## 9. 依赖边界约定

### 9.1 插件允许依赖

插件可以依赖：

- `DigitalIntelligenceBridge.Plugin.Abstractions`
- Avalonia
- 插件自身所需的数据库、配置、解析库

### 9.2 插件禁止依赖

插件不应直接依赖：

- `digital-intelligence-bridge`
- 宿主内部 ViewModel
- 宿主内部 Service
- Prism / DryIoc 的宿主容器实例

## 10. 新增第二、第三个插件时的放置规则

如果后续新增第二个插件，应按以下位置放置：

- 源码：`plugins-src/<SecondPluginName>.Plugin/`
- 本地中转：`plugins/<SecondPluginName>/`
- 正式运行：`%LOCALAPPDATA%\\DibClient\\plugins\\<SecondPluginName>`

如果后续新增第三个插件，应按以下位置放置：

- 源码：`plugins-src/<ThirdPluginName>.Plugin/`
- 本地中转：`plugins/<ThirdPluginName>/`
- 正式运行：`%LOCALAPPDATA%\\DibClient\\plugins\\<ThirdPluginName>`

示例：

- `plugins-src/PatientManagement.Plugin/`
- `plugins/PatientManagement/`
- `plugins-src/ScheduleAssistant.Plugin/`
- `plugins/ScheduleAssistant/`

## 11. 推荐实践

- 新增插件时，先建源码项目，再通过发布脚本生成本地中转目录
- 插件业务逻辑尽量放插件内部，不继续扩散进宿主
- 宿主只做通用插件能力，不做单个插件的业务定制
- 每个插件都应有独立测试
- 每个插件都应有自己的 `plugin.settings.json`
- 需要本地联调敏感资源时，再单独创建 `plugin.development.json`

## 12. 不推荐实践

- 把第二、第三个插件继续放进 `digital-intelligence-bridge/`
- 多个插件共用一个源码项目
- 多个插件共用一个运行时目录
- 插件直接引用宿主主项目
- 在宿主 `appsettings.json` 中长期维护某个插件的大量业务配置

## 13. 结论

从当前仓库开始，插件开发应统一遵循：

- 宿主主程序：`digital-intelligence-bridge/`
- 插件源码根：`plugins-src/`
- 本地产包中转根：`plugins/`
- 正式运行根：`%LOCALAPPDATA%\\DibClient\\plugins`

后续第二、第三个插件都应沿用这一规则，不再混放进宿主目录。
