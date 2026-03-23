# 医保药品导入插件外部化设计

## 1. 目标

将 `MedicalDrugImport.Plugin` 从“仅验证外部 DLL 加载”的样例插件，逐步迁移为真正独立的业务插件。

宿主继续只负责：

- 插件发现
- 清单读取
- DLL 加载
- 菜单注入
- 页面承载
- 失败隔离

插件自身负责：

- 读取 `plugin.settings.json`
- 接收 `MEDICAL_DRUG_IMPORT__*` 环境变量覆盖
- Excel 固定模板预检
- PostgreSQL 导入
- SQL Server 同步

## 2. 目录约定

源码目录：

```text
plugins-src/MedicalDrugImport.Plugin/
```

运行时目录：

```text
plugins/MedicalDrugImport/
```

运行时目录必须至少包含：

- `plugin.json`
- `plugin.settings.json`
- `MedicalDrugImport.Plugin.dll`
- `MedicalDrugImport.Plugin.deps.json`
- 插件运行所需依赖文件

## 3. 配置约定

插件配置优先级：

1. `plugin.settings.json`
2. 环境变量覆盖
3. 代码默认值

建议环境变量：

- `MEDICAL_DRUG_IMPORT__POSTGRES__CONNECTIONSTRING`
- `MEDICAL_DRUG_IMPORT__SQLSERVER__CONNECTIONSTRING`
- `MEDICAL_DRUG_IMPORT__SQLSERVER__ENABLEWRITES`
- `MEDICAL_DRUG_IMPORT__IMPORT__BATCHSIZE`
- `MEDICAL_DRUG_IMPORT__IMPORT__MAXSYNCROWSPERRUN`
- `MEDICAL_DRUG_IMPORT__IMPORT__ALLOWUNSAFEFULLSYNC`

## 4. 实现阶段

### 4.1 已完成

- 插件宿主发现、加载、菜单接入和页面承载
- 插件内独立配置加载
- 插件真实工具页骨架
- 插件内 Excel 预检服务
- 插件内 PostgreSQL 导入已完成真实联调，`Excel -> etl.* -> biz.drug_catalog` 已跑通
- 插件内 SQL Server 同步实现已具备小批次、低死锁优先级、短锁等待和默认限流保护
- 插件页已支持“同步预检”，可在真实写入前只读评估同步规模和阈值命中情况
- 插件内真实 SQL Server 写入默认关闭，只有显式开启 `SqlServer.EnableWrites` 后才允许同步
- 插件页已接入 `预检 / 导入入库 / 同步 SQL Server / 重试同步`
- 运行时插件目录已纳入 `plugin.settings.json`
- 插件导入流水线已改成“结构预检 + 单遍导入”，不再在真实大文件上先做全量计数再导入
- 插件侧“同步预检”已改为数据库 `count(*)` 查询，不再枚举整批待同步记录

### 4.2 后续继续迁移

- SQL Server 真实同步实现
- 插件页更完整的状态展示与错误恢复
- 发布脚本自动同步 `plugins-src` 到 `plugins`

## 5. 当前约束

- 插件不直接引用宿主主项目
- 插件仅依赖 `DigitalIntelligenceBridge.Plugin.Abstractions`
- 宿主不再新增医保业务配置模型
- 业务配置以插件目录为准

## 6. 验收口径

当前阶段通过标准：

- 宿主可从 `plugins/MedicalDrugImport/` 发现插件
- 运行时目录存在 `plugin.settings.json`
- 插件即使缺少配置文件也可用默认值启动
- 插件工具页能创建本地 ViewModel
- 插件同步服务可从插件自身配置读取 SQL Server 连接串
- 默认情况下，待同步记录超过 `Import.MaxSyncRowsPerRun` 时直接拒绝执行真实同步；只有显式开启 `Import.AllowUnsafeFullSync` 后才允许继续
- 默认情况下，即使未超过阈值，只要 `SqlServer.EnableWrites` 未开启，插件也只允许做只读预检

## 7. 当前联调结果

- 真实 PostgreSQL 导入批次：`3a9f877e-45b8-4426-b352-4f5507554887`
- 导入结果：
  - `RAW=276595`
  - `CLEAN=276595`
  - `ERROR=0`
- 业务表核对：
  - `biz.drug_catalog=270433`
  - `drug_name_cn` 非空也是 `270433`
- 同步预检示例批次：`d3cbce98-1343-4d7f-b0c2-738ced4b21f1`
  - `SYNC_COUNT=270433`
  - 因超过 `MaxSyncRowsPerRun=50` 被只读阻断
