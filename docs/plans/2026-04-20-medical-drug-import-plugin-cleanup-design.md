# 药品导入业务插件化清理设计

## 1. 目标

将药品导入业务代码彻底从主程序中剥离，主程序只保留插件宿主职责。`MedicalDrugImport` 相关业务实现、业务接口、业务视图模型和业务页面都应以外部插件形态存在，不再在主程序域模型中留残影。

## 2. 当前状态

已经完成：

1. 主程序配置中的 `MedicalDrugImport` 已删除。
2. 主程序 DI 不再注册内置药品导入链路。
3. 主窗口不再暴露内置药品导入入口。
4. `MedicalDrugImport.Plugin` 已具备独立的 Excel 预检、PostgreSQL 导入和 SQL Server 同步实现。
5. 主程序中的 `DrugImportViewModel`、`DrugImportView` 与对应代码文件已删除。
6. 主程序中的药品导入模型、接口、服务副本已删除。
7. 主程序测试中对内置药品导入 UI、业务契约和服务副本的覆盖已删除或改写为“宿主不再承载该能力”。

当前剩余工作：

1. 文档与说明页继续收口，避免把药品导入描述成主程序能力。
2. 手动回归运行时插件发现、菜单接入与插件页面承载。

## 3. 设计原则

### 3.1 宿主边界

主程序只负责：

- 插件发现与加载
- 资源中心与授权缓存
- 站点身份与宿主部署控制
- 通用插件页面承载

主程序不再负责：

- 药品导入业务页面
- 药品导入业务流程
- 药品导入业务接口定义
- 药品导入业务仓储与同步实现

### 3.2 插件边界

`MedicalDrugImport.Plugin` 负责承载完整业务实现：

- Excel 模板预检
- 导入批次编排
- PostgreSQL 导入仓储
- SQL Server 同步
- 插件自己的业务模型与业务接口

### 3.3 迁移方式

采用“删除主程序残留 + 插件保留唯一实现”的方式，而不是抽取新的共享业务库。

原因：

1. 这是全新系统，没有历史兼容负担。
2. 药品导入已明确是插件能力，不属于宿主职责。
3. 现在再保留一层主程序共享业务库，会继续污染宿主边界。

## 4. 清理范围

### 4.1 主程序中应删除的内容

#### UI 与 ViewModel

- `digital-intelligence-bridge/Views/DrugImportView.axaml`
- `digital-intelligence-bridge/Views/DrugImportView.axaml.cs`
- `digital-intelligence-bridge/ViewModels/DrugImportViewModel.cs`

#### 业务接口与实现

- `digital-intelligence-bridge/Services/IDrugExcelImportService.cs`
- `digital-intelligence-bridge/Services/IDrugImportPipelineService.cs`
- `digital-intelligence-bridge/Services/IDrugImportRepository.cs`
- `digital-intelligence-bridge/Services/IDrugCatalogSyncRepository.cs`
- `digital-intelligence-bridge/Services/ISqlServerDrugSyncService.cs`
- `digital-intelligence-bridge/Services/DrugExcelImportService.cs`
- `digital-intelligence-bridge/Services/DrugImportPipelineService.cs`
- `digital-intelligence-bridge/Services/DrugImportRepository.cs`
- `digital-intelligence-bridge/Services/SqlServerDrugSyncService.cs`
- `digital-intelligence-bridge/Services/DrugExcelTemplate.cs`

#### 主程序测试中应删除或改写的内容

- 内置药品导入 ViewModel 测试
- 内置药品导入 ViewLocator 测试
- 主窗口内置药品导入视图测试
- 主程序内置药品导入业务契约测试
- 任何以主程序命名空间直接引用这些业务接口的测试

### 4.2 插件中保留的内容

保留并作为唯一实现来源：

- `plugins-src/MedicalDrugImport.Plugin/Models/**`
- `plugins-src/MedicalDrugImport.Plugin/Services/**`
- `plugins-src/MedicalDrugImport.Plugin/ViewModels/**`
- `plugins-src/MedicalDrugImport.Plugin/Views/**`
- `plugins-src/MedicalDrugImport.Plugin/Configuration/**`

## 5. 测试策略

### 5.1 主程序测试

主程序只保留这些验证：

- 插件发现
- 插件加载失败隔离
- 插件导航与宿主承载
- 资源中心与授权缓存

### 5.2 插件测试

药品导入业务测试统一留在插件侧：

- `MedicalDrugImportPluginConfigurationTests`
- `MedicalDrugImportPluginViewTests`
- `MedicalDrugImportPluginExcelTests`
- `MedicalDrugImportPluginPipelineTests`
- `MedicalDrugImportPluginPostgresTests`
- `MedicalDrugImportPluginSqlServerTests`

## 6. 风险与控制

### 风险 1：主程序测试编译失败

原因：主程序测试仍直接引用已删除类型。

控制：

- 先写失败测试或删除旧测试
- 再删实现
- 每一批删除后跑定向测试

### 风险 2：插件测试漏掉主程序删除带来的依赖

原因：测试工程同时引用主程序项目和插件项目。

控制：

- 删除前先全局搜索主程序接口引用
- 将相关测试改到插件命名空间和插件实现

### 风险 3：README 与打包文档继续暗示主程序内置能力

控制：

- README 改成“药品导入能力完全由外部插件提供”
- 插件开发与打包文档成为唯一业务说明来源

## 7. 验收标准

满足以下条件视为完成：

1. 主程序项目中不再存在 `DrugImportViewModel`、`DrugImportView` 及药品导入业务服务/接口。
2. 主程序构建通过。
3. 主程序相关旧测试已删除或改写，不再引用药品导入业务类型。
4. 插件相关业务测试通过。
5. 插件加载、导航与宿主承载测试通过。
6. 文档不再把药品导入描述为主程序能力。

## 8. 当前验收进度

截至 2026-04-20，以下条目已满足：

1. 主程序项目中已不存在 `DrugImportViewModel`、`DrugImportView` 及药品导入业务服务/接口。
2. 主程序构建通过。
3. 主程序相关旧测试已删除或改写，不再引用药品导入业务类型。
4. 插件相关业务测试通过。
5. 插件加载、导航与宿主承载测试通过。

当前未完全收口的仅剩第 6 条：继续把有效文档中的表述统一到“药品导入是外部插件能力”。
