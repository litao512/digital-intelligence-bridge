# 资源中心数据契约

**目标**

- 为资源中心后台、托盘宿主和插件运行时建立统一的数据契约。
- 明确单位、站点、插件、资源、申请、绑定和审批日志之间的关系。
- 为后续 API、数据库表设计和客户端同步逻辑提供稳定语义。

## 一、设计原则

- “资源”是统一抽象，数据库连接、Supabase、OCR、人脸识别和其他 HTTP 服务都纳入同一模型。
- “申请”和“授权结果”分离；申请单不代表可用权限，绑定关系才代表可用权限。
- “单位”是业务主体，“站点”是部署实例，“插件”是能力载体，三者不能混用。
- 敏感配置与资源元数据分离，避免将密码、Token 与资源主数据混放。

## 二、核心实体

### 1. 使用单位 `Organization`

表示医疗机构或业务单位。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `OrganizationId` | `uuid` | 主键 |
| `OrganizationCode` | `text` | 平台内唯一编码 |
| `OrganizationName` | `text` | 单位名称 |
| `Status` | `text` | `Pending` / `Active` / `Disabled` |
| `Source` | `text` | 来源，如 `Manual` / `ClientRegistration` |
| `CreatedBy` | `text` | 创建人或系统标识 |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |
| `UpdatedAt` | `timestamp with time zone` | 更新时间 |

### 2. 站点 `Site`

表示单位下的一个部署实例，对应当前客户端的站点身份。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `SiteId` | `uuid` | 主键，兼容客户端现有 `SiteId` |
| `OrganizationId` | `uuid` | 所属单位 |
| `SiteCode` | `text` | 站点编码，可选 |
| `SiteName` | `text` | 站点名称 |
| `SiteRemark` | `text` | 备注 |
| `MachineName` | `text` | 最近一次上报机器名 |
| `Channel` | `text` | 客户端渠道 |
| `ClientVersion` | `text` | 客户端版本 |
| `Status` | `text` | `Pending` / `Active` / `Disabled` |
| `LastSeenAt` | `timestamp with time zone` | 最近心跳时间 |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |
| `UpdatedAt` | `timestamp with time zone` | 更新时间 |

### 3. 插件 `Plugin`

表示系统中可识别的插件能力。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `PluginId` | `uuid` | 主键 |
| `PluginCode` | `text` | 插件唯一编码，对应 `plugin.json` 中的逻辑标识 |
| `PluginName` | `text` | 插件名称 |
| `VersionPolicy` | `jsonb` | 可选的版本策略 |
| `Status` | `text` | `Active` / `Disabled` |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |
| `UpdatedAt` | `timestamp with time zone` | 更新时间 |

### 4. 资源 `Resource`

统一抽象可被插件消费的外部依赖。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `ResourceId` | `uuid` | 主键 |
| `ResourceCode` | `text` | 资源唯一编码 |
| `ResourceName` | `text` | 资源名称 |
| `ResourceType` | `text` | 资源类型 |
| `OwnerOrganizationId` | `uuid` | 资源所有者单位 |
| `VisibilityScope` | `text` | `Private` / `Shared` / `Platform` |
| `ConfigSchemaVersion` | `integer` | 资源配置结构版本 |
| `ConfigPayload` | `jsonb` | 非敏感连接配置，如主机、端口、数据库名、BaseUrl |
| `Status` | `text` | `Draft` / `PendingApproval` / `Active` / `Disabled` / `Archived` |
| `Description` | `text` | 资源描述 |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |
| `UpdatedAt` | `timestamp with time zone` | 更新时间 |

### 5. 资源密钥 `ResourceSecret`

承载资源敏感信息，如密码、Token、API Key。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `ResourceSecretId` | `uuid` | 主键 |
| `ResourceId` | `uuid` | 关联资源 |
| `SecretVersion` | `integer` | 密钥版本 |
| `EncryptionMode` | `text` | `AppEncrypted` / `VaultRef` 等 |
| `SecretPayload` | `text` | 加密后密文或外部密钥引用 |
| `RotatedAt` | `timestamp with time zone` | 最近轮换时间 |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |

### 6. 资源绑定 `ResourceBinding`

表示最终授权结果，是判断资源是否可用的唯一依据。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `ResourceBindingId` | `uuid` | 主键 |
| `OrganizationId` | `uuid` | 绑定所属单位 |
| `SiteId` | `uuid` | 站点级绑定时填写 |
| `PluginId` | `uuid` | 插件级绑定时填写 |
| `ResourceId` | `uuid` | 关联资源 |
| `BindingScope` | `text` | `Organization` / `Site` / `PluginAtSite` |
| `Status` | `text` | `PendingActivation` / `Active` / `Suspended` / `Revoked` |
| `ApprovedRequestId` | `uuid` | 来源申请单 |
| `EffectiveFrom` | `timestamp with time zone` | 生效时间 |
| `EffectiveTo` | `timestamp with time zone` | 失效时间，可空 |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |
| `UpdatedAt` | `timestamp with time zone` | 更新时间 |

### 7. 资源申请 `ResourceApplication`

统一承载注册资源和申请使用资源两类流程。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `ApplicationId` | `uuid` | 主键 |
| `ApplicationType` | `text` | `RegisterResource` / `UseResource` |
| `ApplicantOrganizationId` | `uuid` | 申请单位 |
| `ApplicantSiteId` | `uuid` | 申请站点，可空 |
| `ApplicantPluginId` | `uuid` | 申请插件，可空 |
| `TargetResourceId` | `uuid` | 目标资源；注册申请时可空，审批通过后回填 |
| `Payload` | `jsonb` | 申请内容；注册资源时承载拟注册信息，使用申请时承载用途与理由 |
| `Reason` | `text` | 申请原因 |
| `Status` | `text` | `Draft` / `Submitted` / `UnderReview` / `Approved` / `Rejected` / `Returned` / `Cancelled` |
| `SubmittedAt` | `timestamp with time zone` | 提交时间 |
| `ApprovedAt` | `timestamp with time zone` | 审批完成时间 |
| `ApprovedBy` | `text` | 审批人 |
| `CreatedAt` | `timestamp with time zone` | 创建时间 |
| `UpdatedAt` | `timestamp with time zone` | 更新时间 |

### 8. 审批日志 `ApprovalLog`

承载完整审批留痕。

建议字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `ApprovalLogId` | `uuid` | 主键 |
| `ApplicationId` | `uuid` | 关联申请单 |
| `Action` | `text` | `Submit` / `Approve` / `Reject` / `Return` / `Cancel` |
| `Comment` | `text` | 审批意见 |
| `Operator` | `text` | 操作人 |
| `CreatedAt` | `timestamp with time zone` | 操作时间 |

## 三、关键枚举

### 1. 资源类型 `ResourceType`

第一阶段建议支持：

- `PostgreSQL`
- `SqlServer`
- `Supabase`
- `HttpService`

后续可扩展：

- `Redis`
- `MessageQueue`
- `FileStorage`
- `FaceRecognition`

### 2. 资源状态 `ResourceStatus`

| 状态 | 含义 |
|---|---|
| `Draft` | 草稿，尚未提交注册审批 |
| `PendingApproval` | 已提交注册审批 |
| `Active` | 资源可被发现和授权 |
| `Disabled` | 资源被停用，不再对外授权 |
| `Archived` | 历史归档，不再参与运行时发现 |

### 3. 申请状态 `ApplicationStatus`

| 状态 | 含义 |
|---|---|
| `Draft` | 草稿 |
| `Submitted` | 已提交 |
| `UnderReview` | 审批中 |
| `Approved` | 审批通过 |
| `Rejected` | 审批拒绝 |
| `Returned` | 退回修改 |
| `Cancelled` | 申请方撤销 |

### 4. 绑定状态 `BindingStatus`

| 状态 | 含义 |
|---|---|
| `PendingActivation` | 已获批待生效 |
| `Active` | 正常可用 |
| `Suspended` | 临时停用 |
| `Revoked` | 永久撤销 |

### 5. 可见范围 `VisibilityScope`

| 范围 | 含义 |
|---|---|
| `Private` | 仅资源所有单位可发现 |
| `Shared` | 可被其他单位申请使用 |
| `Platform` | 平台级公共资源 |

### 6. 绑定粒度 `BindingScope`

| 粒度 | 含义 |
|---|---|
| `Organization` | 整个单位可用 |
| `Site` | 某个站点可用 |
| `PluginAtSite` | 某个站点下某个插件可用 |

## 四、关系约束

- 一个 `Organization` 下可以有多个 `Site`。
- 一个 `Organization` 可以拥有多个 `Resource`。
- 一个 `Resource` 可以被多个 `ResourceBinding` 引用。
- 一个 `ResourceApplication` 审批通过后可产生一个或多个 `ResourceBinding`。
- 一个 `ResourceApplication` 可以有多条 `ApprovalLog`，构成完整审批历史。

## 五、运行时下发契约建议

资源中心后台返回给托盘的运行时资源对象，建议至少包含以下字段：

| 字段 | 说明 |
|---|---|
| `ResourceId` | 资源标识 |
| `ResourceCode` | 资源编码 |
| `ResourceType` | 资源类型 |
| `BindingScope` | 当前授权粒度 |
| `ConfigVersion` | 配置版本 |
| `ConfigPayload` | 非敏感配置 |
| `SecretPayload` 或 `SecretRef` | 解密后的运行时密钥或密钥引用 |
| `Capabilities` | 允许的操作，如 `read` / `write` |

该对象属于运行时下发契约，不等同于后台数据库原始表结构。

## 六、设计结论

- 资源中心必须同时解决主数据、审批流、授权结果和运行时下发四类问题。
- 资源申请和资源绑定必须分离，防止把“申请中”误当成“可用”。
- 第一阶段优先建立统一语义，再逐步细化资源类型专属配置表。
