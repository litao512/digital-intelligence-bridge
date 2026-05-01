# 主程序配置收紧设计

## 1. 背景

当前仓库已经把插件正式资源来源收敛为“资源中心 -> 宿主缓存 -> 插件读取”，并明确这是一个全新系统，不保留历史兼容负担。

但主程序侧仍然保留了几类旧兼容逻辑和旧说明：

- `ConfigurationExtensions.cs` 中的 `MSSQL_DB_*` 环境变量映射
- `MedicalDrugImport.SqlServer` 等插件业务配置仍可通过主程序配置进入系统
- `README`、`PRD`、运维文档中仍存在“插件业务资源可由主程序配置或环境变量提供”的表述

这会造成配置边界不清：插件正式资源已经中心化，但主程序侧仍然暗示“业务连接也可以从宿主兼容入口进来”。

## 2. 目标

将主程序配置模型正式收敛为：

- 主程序只负责宿主部署控制和本机状态
- 插件业务资源不再通过主程序配置或环境变量进入系统
- 删除与该目标冲突的兼容逻辑和文档表述

## 3. 最终边界

### 3.1 保留项

以下内容继续保留在主程序配置体系中：

1. 宿主部署控制
- `DIB_CONFIG_ROOT`
- 日志目录
- 发布中心/资源中心地址
- 宿主运行开关

2. 主程序本机状态
- `%LOCALAPPDATA%\\DibClient\\appsettings.json`
- `SiteId`
- `SiteName`
- `SiteRemark`
- 托盘与宿主行为偏好

单位归属不属于客户端本机状态，必须由 DIB 中心 `sites.organization_id` 维护。

3. 主程序通用默认配置模型
- 安装目录 `appsettings.json` 作为默认模板
- 用户目录 `appsettings.json` 作为运行时状态文件

### 3.2 删除项

以下内容应视为旧兼容设计并删除：

1. `MSSQL_DB_*` 兼容环境变量映射
- `MSSQL_DB_SERVER`
- `MSSQL_DB_PORT`
- `MSSQL_DB_NAME`
- `MSSQL_DB_USER`
- `MSSQL_DB_PASSWORD`
- `MSSQL_DB_ENCRYPT`
- `MSSQL_DB_TRUST_SERVER_CERTIFICATE`

2. 主程序中承载插件业务连接的配置入口
- `MedicalDrugImport.SqlServer` 这类插件业务资源配置
- 所有“通过主程序配置为插件提供业务连接”的文档说明

3. 所有仍然暗示“插件业务资源可通过主程序环境变量覆盖”的文档口径

## 4. 设计原则

### 4.1 单一真相源

插件正式业务资源的唯一真相源是资源中心。

主程序不再承担插件业务资源的配置真相源职责。

### 4.2 宿主配置与插件资源分离

主程序配置只回答两个问题：

1. 宿主怎么运行
2. 这台机器是谁

插件资源配置只回答一个问题：

1. 某插件被授权使用什么资源

### 4.3 不保留无必要兼容层

既然项目是全新系统，就不再为不存在的历史迁移场景保留代码兼容入口。

## 5. 影响范围

### 5.1 代码

重点影响：

- `digital-intelligence-bridge/Configuration/ConfigurationExtensions.cs`
- `digital-intelligence-bridge/Configuration/AppSettings.cs`
- 依赖 `MedicalDrugImport.SqlServer` 的主程序服务与测试
- 任何仍读取 `MSSQL_DB_*` 的测试或说明

### 5.2 文档

重点影响：

- `digital-intelligence-bridge/README.md`
- `docs/PRD.md`
- `docs/05-operations/CONFIGURATION_ARCHITECTURE_SPEC.md`
- `docs/standards/configuration-layering-principles.md`
- 其他仍提到插件业务资源环境变量或主程序插件业务配置入口的文档

## 6. 风险与控制

### 6.1 风险

1. 删除主程序插件业务配置入口后，仍依赖这些入口的测试会失败
2. 文档如果改得不完整，后续仍会按旧口径排障
3. 若某些主程序服务仍直接引用插件业务配置模型，需要一并重构

### 6.2 控制措施

1. 先清理文档口径，再删除实现
2. 删除兼容逻辑时同步删测试，不保留“名义废弃、实际可用”的路径
3. 用定向测试和构建验证确认主程序仍可正常启动、配置仍可绑定

## 7. 实施顺序

建议按以下顺序落地：

1. 清理文档与说明
2. 删除 `ConfigurationExtensions.cs` 中的 `MSSQL_DB_*` 映射
3. 删除主程序中的插件业务配置入口
4. 同步调整受影响测试
5. 跑串行构建与测试回归

## 8. 验收标准

满足以下条件即视为完成：

1. 主程序侧不再支持 `MSSQL_DB_*` 兼容环境变量
2. 主程序配置模型中不再承载插件业务连接
3. README、PRD、运维文档不再描述插件业务资源由主程序配置提供
4. 插件正式业务资源来源口径与代码实现一致
5. 主项目构建通过，相关单测通过
