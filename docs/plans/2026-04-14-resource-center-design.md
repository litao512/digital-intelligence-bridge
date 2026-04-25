# 资源中心设计

**目标**

- 为 DIB 建立统一的资源中心，集中管理数据库连接、Supabase 后端连接、OCR 服务、人脸识别服务等插件运行时依赖。
- 支持医疗机构注册资源、管理员审批、托盘发现资源、站点发起申请、审批后按授权向插件下发资源。
- 为后续多单位、多站点、多插件的统一治理提供稳定的数据模型和流程边界。

**设计范围**

- 本轮聚焦资源中心的业务模型、审批流、托盘与插件职责边界。
- 资源类型先覆盖 `PostgreSQL`、`SqlServer`、`Supabase`、`HttpService`。
- 本轮不直接引入 `Nacos`、`Apollo`、`Vault` 作为硬依赖，只为后续接入预留扩展位。

## 一、设计结论

- 资源中心应被建模为“资源注册中心 + 授权中心”，而不是简单的“数据库连接配置表”。
- 数据库连接只是资源的一类；所有插件运行时依赖都应统一纳入“资源”抽象。
- 采用“单位级租户 + 站点级部署实例 + 插件级最小授权”的模型。
- 宿主负责资源发现、申请、同步、下发；插件负责声明需求并消费已授权资源。
- 第一阶段采用“自建资源中心业务层”，敏感信息先由后台统一加密存储，不建议一开始就把重心放在 `Nacos`。

## 二、核心对象模型

### 1. 使用单位 `Organization`

- 表示医疗机构或业务单位。
- 负责统一管理“使用单位”主数据，避免客户端各自录入导致脏数据。
- 建议字段：
  - `OrganizationId`
  - `OrganizationCode`
  - `OrganizationName`
  - `Status`
  - `CreatedBy`
  - `CreatedAt`

### 2. 站点 `Site`

- 表示单位下的一个部署实例。
- 站点继续承接当前 DIB 客户端已有的 `SiteId` 身份。
- 建议字段：
  - `SiteId`
  - `OrganizationId`
  - `SiteCode`
  - `SiteName`
  - `SiteRemark`
  - `Status`

### 3. 插件 `Plugin`

- 表示系统中可识别的业务能力。
- 插件身份不等于安装记录，安装记录由宿主运行时汇总。
- 建议字段：
  - `PluginId`
  - `PluginCode`
  - `PluginName`
  - `VersionPolicy`
  - `Status`

### 4. 资源 `Resource`

- 统一抽象所有可被插件使用的外部依赖。
- 资源类型建议先支持：
  - `PostgreSQL`
  - `SqlServer`
  - `Supabase`
  - `HttpService`
- 建议字段：
  - `ResourceId`
  - `ResourceCode`
  - `ResourceName`
  - `ResourceType`
  - `OwnerOrganizationId`
  - `VisibilityScope`
  - `Status`
  - `Description`

### 5. 资源密钥 `ResourceSecret`

- 用于承载密码、Token、API Key 等敏感信息。
- 与 `Resource` 主表分离，避免元数据和敏感信息混存。
- 建议字段：
  - `ResourceSecretId`
  - `ResourceId`
  - `SecretPayload`
  - `SecretVersion`
  - `EncryptionMode`
  - `RotatedAt`

### 6. 资源绑定 `ResourceBinding`

- 表示最终授权结果。
- 只有绑定存在且有效，资源才算真正可用。
- 建议字段：
  - `ResourceBindingId`
  - `OrganizationId`
  - `SiteId`
  - `PluginId`
  - `ResourceId`
  - `BindingScope`
  - `Status`
  - `ApprovedRequestId`

### 7. 资源申请 `ResourceApplication`

- 承载“注册资源”和“申请使用资源”两类流程。
- 建议字段：
  - `ApplicationId`
  - `ApplicationType`
  - `ApplicantOrganizationId`
  - `ApplicantSiteId`
  - `ApplicantPluginId`
  - `TargetResourceId`
  - `Reason`
  - `Status`
  - `SubmittedAt`
  - `ApprovedAt`
  - `ApprovedBy`

### 8. 审批日志 `ApprovalLog`

- 审计审批动作，不把审批痕迹只留在状态字段里。
- 建议字段：
  - `ApprovalLogId`
  - `ApplicationId`
  - `Action`
  - `Comment`
  - `Operator`
  - `CreatedAt`

## 三、状态流转

### 1. 资源注册流程

- 医疗机构或管理员提交资源注册申请。
- 申请单进入 `Submitted`。
- 管理员可执行：
  - `Approve`
  - `Reject`
  - `ReturnForUpdate`
- 审批通过后，系统创建：
  - `Resource`
  - `ResourceSecret`
- 建议状态：
  - `Draft`
  - `Submitted`
  - `UnderReview`
  - `Approved`
  - `Rejected`
  - `Returned`

### 2. 资源使用授权流程

- 托盘发现可申请资源。
- 站点为某个插件发起使用申请。
- 管理员审批通过后，系统创建 `ResourceBinding`。
- 授权判断以绑定表为准，而不是以申请单状态为准。

### 3. 资源绑定生命周期

- 建议状态：
  - `PendingActivation`
  - `Active`
  - `Suspended`
  - `Revoked`

### 4. 资源发现规则

托盘只应发现满足以下条件的资源：

- `Resource.Status = Active`
- `VisibilityScope` 允许当前单位可见
- 当前单位未被显式禁用

托盘视图建议区分为：

- `AvailableToApply`
- `Authorized`

## 四、职责边界

### 1. 资源中心后台

后台负责：

- 单位管理
- 站点管理
- 插件目录管理
- 资源注册
- 资源审批
- 资源授权
- 敏感信息存储
- 授权查询
- 审计日志

后台不负责执行业务调用，不替插件执行 SQL 或调用 OCR。

### 2. 托盘宿主

托盘负责：

- 维护本机站点身份
- 站点注册与心跳
- 拉取可发现资源
- 发起资源申请
- 拉取已授权资源
- 向插件下发运行时资源上下文
- 管理本地缓存
- 处理资源撤销或更新后的同步

### 3. 插件

插件负责：

- 声明所需资源
- 接收宿主下发的已授权资源
- 自行建立连接并执行业务逻辑
- 上报资源使用错误

插件不负责资源真相管理，不直接维护长期固定连接来源。

## 五、资源下发方式

推荐采用“宿主统一编排，插件受控直连”的模式。

- 宿主根据 `单位 + 站点 + 插件` 拉取已授权资源。
- 宿主向插件注入运行时资源描述对象。
- 插件拿到配置后自己建立数据库连接或调用 HTTP 服务。

不建议采用以下两种极端方案：

- 宿主全量代理所有资源调用：宿主会快速膨胀成业务平台。
- 插件完全自由读取本地连接配置：会重新回到配置分散、难审计、难治理的状态。

## 六、插件资源声明

建议插件增加显式资源声明，供宿主做发现、授权匹配和运行时校验。

声明内容至少应包含：

- `ResourceType`
- `UsageKey`
- `Required`
- `Description`

示例场景：

- 药品导入插件需要 `PostgreSQL`
- OCR 识别插件需要 `HttpService`
- 同步插件需要 `Supabase`

## 七、第三方产品选型结论

### 1. `Nacos` 是否适合作为第一阶段主骨架

不建议。

原因：

- `Nacos` 更适合做配置分发和服务发现。
- 当前核心问题是“单位 / 站点 / 插件 / 资源”的业务模型和审批授权流程。
- 即使引入 `Nacos`，仍然需要自建一层业务型资源中心。

### 2. 推荐策略

- 第一阶段：自建资源中心业务层。
- 第二阶段：如有需要，引入成熟产品承接底层能力。

可参考产品：

- 配置中心：
  - `Nacos`
  - `Apollo`
  - `Consul`
- 密钥管理：
  - `HashiCorp Vault`
  - 云厂商 KMS / Secret Manager
- 身份与授权：
  - `Keycloak`

### 3. 第一阶段推荐方案

- 业务模型、审批流、授权流完全自建。
- 敏感信息先统一加密存于后台。
- 为未来接入 `Vault`、`Apollo` 或 `Nacos` 预留接口，但不作为当前强依赖。

## 八、实施建议

### 一期

1. 建立后台资源中心最小数据模型
2. 建立单位、站点、插件、资源、申请、绑定、审批日志表
3. 建立资源注册、资源申请、资源授权查询 API
4. 在托盘增加资源发现、申请、授权同步能力
5. 为插件补充资源声明与运行时注入能力

### 二期

1. 增加资源配置版本号与刷新机制
2. 增加资源撤销与停用后的宿主同步处理
3. 增加资源审计视图与运维界面

### 三期

1. 评估接入 `Vault` 承载密钥
2. 评估接入 `Nacos` / `Apollo` 承载动态配置分发
3. 评估更细粒度的审批流、灰度和告警

## 九、设计结论

- 资源中心应统一管理插件运行时依赖，而不局限于数据库连接。
- 系统的主线应是“资源注册、资源发现、资源申请、审批授权、运行时下发”。
- 推荐采用“宿主统一编排，插件受控直连”的模式。
- 第一阶段以自建资源中心业务层为主，不把第三方配置中心作为核心前提。
