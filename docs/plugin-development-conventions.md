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
  - 所有插件运行时目录根目录
  - 宿主启动时从这里发现插件

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

### 3.2 运行时目录

每个插件运行时目录都放在：

```text
plugins/<PluginName>/
```

例如：

- `plugins/MedicalDrugImport/`
- `plugins/PatientManagement/`
- `plugins/ScheduleAssistant/`

### 3.3 规则

- 一个插件只对应一个源码项目
- 一个插件只对应一个运行时目录
- 不允许多个插件共用同一个运行时目录
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
  - 插件业务配置

## 6. 运行时目录结构建议

运行时目录建议采用下面结构：

```text
plugins/
  MedicalDrugImport/
    plugin.json
    plugin.settings.json
    MedicalDrugImport.Plugin.dll
    MedicalDrugImport.Plugin.deps.json
    其他依赖.dll
```

说明：

- `plugin.json`
  - 运行时必须存在
- `plugin.settings.json`
  - 建议存在
  - 插件业务配置从这里读取
- `*.dll`
  - 插件入口程序集及其依赖

## 7. plugin.json 约定

建议所有插件统一采用以下字段：

```json
{
  "id": "medical-drug-import",
  "name": "医保药品导入",
  "version": "0.1.0",
  "entryAssembly": "MedicalDrugImport.Plugin.dll",
  "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
  "minHostVersion": "1.0.0"
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

## 8. 插件配置约定

插件业务配置不应放进宿主 `appsettings.json`。

建议采用：

1. `plugin.settings.json`
2. 环境变量覆盖
3. 插件默认值兜底

建议使用插件前缀环境变量，避免多个插件互相污染，例如：

- `MEDICAL_DRUG_IMPORT__POSTGRES__CONNECTIONSTRING`
- `MEDICAL_DRUG_IMPORT__SQLSERVER__CONNECTIONSTRING`
- `MEDICAL_DRUG_IMPORT__IMPORT__BATCHSIZE`

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
- 运行时：`plugins/<SecondPluginName>/`

如果后续新增第三个插件，应按以下位置放置：

- 源码：`plugins-src/<ThirdPluginName>.Plugin/`
- 运行时：`plugins/<ThirdPluginName>/`

示例：

- `plugins-src/PatientManagement.Plugin/`
- `plugins/PatientManagement/`
- `plugins-src/ScheduleAssistant.Plugin/`
- `plugins/ScheduleAssistant/`

## 11. 推荐实践

- 新增插件时，先建源码项目，再补运行时发布目录
- 插件业务逻辑尽量放插件内部，不继续扩散进宿主
- 宿主只做通用插件能力，不做单个插件的业务定制
- 每个插件都应有独立测试
- 每个插件都应有自己的 `plugin.settings.json`

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
- 插件运行时根：`plugins/`

后续第二、第三个插件都应沿用这一规则，不再混放进宿主目录。
