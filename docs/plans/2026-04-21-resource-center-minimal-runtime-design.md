# 资源中心最小运行时闭环设计

**目标**

- 在 `dib-release-center` 中补齐资源中心一期的最小运行时后端能力。
- 消除 DIB 客户端当前访问资源中心运行时 RPC 时的 `404`。
- 让宿主能够正式从资源中心拉取 `patient-registration / registration-db` 授权资源，并向插件下发。

**设计范围**

- 本轮只做“最小可运行闭环”，不追求一次性完成完整资源治理平台。
- 本轮只覆盖：
  - 资源主数据最小表
  - 资源密钥最小表
  - 资源绑定最小表
  - 运行时发现与授权查询 RPC
  - 最小申请入口 RPC
- 本轮不包含：
  - 完整审批页面
  - 完整申请流状态机
  - 复杂单位级、站点级、组织级多层继承授权
  - Vault、Nacos、Apollo 等第三方底座集成

## 一、设计结论

- 当前客户端、宿主缓存、插件读取链路已经具备资源消费能力，阻塞点在 `dib-release-center` 后端缺少资源中心运行时 RPC。
- 本轮应先实现“运行时资源下发闭环”，而不是先做完整后台管理 UI。
- 最小授权粒度先固定为 `PluginAtSite`，即“某站点的某插件可使用某资源”。
- 资源类型先只要求支持 `PostgreSQL`，但表结构保持可扩展，以便后续加入 `SqlServer`、`Supabase`、`HttpService`。
- 运行时返回对象沿用当前客户端约定：返回 `pluginCode`、`resourceType`、`configPayload`、`configVersion`、`bindingScope` 等字段，由宿主映射到授权资源缓存。

## 二、当前现状与直接问题

### 1. 客户端现状

- `ReleaseCenterService.GetAuthorizedResourcesAsync()` 会调用：
  - `get_site_authorized_resources`
- `ReleaseCenterService.DiscoverResourcesAsync()` 会调用：
  - `discover_site_resources`
- `ReleaseCenterService.ApplyResourceAsync()` 会调用：
  - `apply_site_resource`

客户端配置、请求头和缓存逻辑都已经存在。

### 2. 当前实际问题

- `dib-release-center` 当前只实现了：
  - `dib_release.register_site_heartbeat(...)`
  - `dib_release.get_site_plugin_manifest(...)`
- 资源中心一期的 3 个运行时 RPC 尚未实现。
- 因此客户端访问资源中心时返回 `404`，无法生成 `authorized-resources.json`，插件也无法取得正式运行资源。

### 3. 为什么不先做本地伪造缓存

- 本项目已经明确为全新系统，不保留历史兼容路径。
- 当前需要推进的是正式链路，而不是再引入临时兜底。
- 插件侧资源读取链路已经基本成立，本轮不应继续把精力放在客户端本地绕行上。

## 三、最小对象模型

### 1. `resource_plugins`

用途：

- 为资源绑定提供稳定插件主数据。
- 避免直接依赖客户端上传的自由文本作为唯一键。

建议字段：

- `id`
- `plugin_code`
- `plugin_name`
- `status`
- `created_at`
- `updated_at`

说明：

- 第一阶段可通过 seed 初始化 `patient-registration`。
- 后续再补后台管理入口。

### 2. `resources`

用途：

- 统一承载可被插件消费的正式外部资源。

建议字段：

- `id`
- `resource_code`
- `resource_name`
- `resource_type`
- `owner_organization_name`
- `visibility_scope`
- `config_schema_version`
- `config_payload`
- `status`
- `description`
- `created_at`
- `updated_at`

说明：

- 第一阶段允许 `owner_organization_name` 直接存文本，后续再升级为正式 `organization_id`。
- `config_payload` 只存非敏感字段。

### 3. `resource_secrets`

用途：

- 独立保存敏感字段。

建议字段：

- `id`
- `resource_id`
- `secret_payload`
- `secret_version`
- `encryption_mode`
- `created_at`
- `updated_at`

说明：

- 第一阶段可先沿用 Supabase/Postgres 内部加密或受控明文方案，但结构上必须独立成表。
- 运行时 RPC 返回时合并成最终 `configPayload`。

### 4. `resource_bindings`

用途：

- 表示最终授权结果。

建议字段：

- `id`
- `site_id`
- `plugin_code`
- `resource_id`
- `binding_scope`
- `status`
- `usage_key`
- `config_version`
- `created_at`
- `updated_at`

说明：

- 第一阶段只支持：
  - `binding_scope = PluginAtSite`
  - `status = Active`
- `usage_key` 直接存入绑定表，避免当前阶段还要在后端重建复杂的需求匹配器。

### 5. `resource_applications`

用途：

- 提供最小“申请使用资源”留痕。

建议字段：

- `id`
- `site_id`
- `plugin_code`
- `resource_id`
- `reason`
- `status`
- `created_at`
- `updated_at`

说明：

- 第一阶段只需要支持最小记录与返回。
- 审批流、审批日志和复杂状态流转后续再补。

## 四、最小运行时 RPC

### 1. `dib_release.get_site_authorized_resources(...)`

用途：

- 客户端启动或同步时拉取本站点已授权资源。

输入建议：

- `p_channel_code`
- `p_site_id`
- `p_client_version`
- `p_plugins_json`

输出建议：

- 授权资源数组
- 每项至少包含：
  - `resourceId`
  - `resourceCode`
  - `resourceName`
  - `resourceType`
  - `bindingScope`
  - `pluginCode`
  - `configVersion`
  - `configPayload`
  - `capabilities`

第一阶段行为：

- 只返回 `status = Active` 的 `PluginAtSite` 绑定。
- `configPayload` 由 `resources.config_payload` 与 `resource_secrets.secret_payload` 合并。

### 2. `dib_release.discover_site_resources(...)`

用途：

- 返回：
  - 可申请资源
  - 已授权资源
  - 待处理申请

第一阶段最小行为：

- `authorized`：返回和 `get_site_authorized_resources` 同源的授权结果
- `availableToApply`：返回当前未绑定但允许该站点查看的资源
- `pendingApplications`：返回当前站点提交且未关闭的最小申请记录

### 3. `dib_release.apply_site_resource(...)`

用途：

- 接收客户端资源申请动作。

第一阶段最小行为：

- 插入 `resource_applications`
- 返回：
  - `success`
  - `message`
  - `applicationId`
  - `status`

第一阶段不做：

- 自动审批
- 完整审批状态机
- 审批日志

## 五、数据流

### 1. 授权下发链路

1. 客户端带 `siteId + channel + installed plugins` 调用 `get_site_authorized_resources`
2. `dib-release-center` 查询 `resource_bindings`
3. 按 `plugin_code + usage_key` 组装授权结果
4. 客户端写入 `authorized-resources.json`
5. 插件通过宿主 `TryGetResource("registration-db")` 读取资源

### 2. 资源发现链路

1. 客户端调用 `discover_site_resources`
2. 后端返回：
   - 已授权资源
   - 可申请资源
   - 申请中记录
3. 客户端资源中心页展示结果

### 3. 资源申请链路

1. 客户端调用 `apply_site_resource`
2. 后端插入最小申请记录
3. 返回“已提交”
4. 后续审批和激活由后端下一阶段补齐

## 六、最小交付目标

本轮交付完成后，应满足：

1. 客户端不再因为资源中心 RPC 缺失而返回 `404`
2. 本地生成 `authorized-resources.json`
3. `patient-registration` 能从正式资源中心链路拿到 `registration-db`
4. 资源中心页能看到最小发现/授权/申请结果

## 七、明确不做的内容

本轮不做以下内容：

1. 完整后台审批 UI
2. 完整组织/单位主数据治理
3. 复杂组织级/站点级多层授权继承
4. Vault 等密钥平台集成
5. 资源注册管理后台页面
6. 多资源类型的深度校验器

## 八、设计结论

- 这一步应聚焦在 `dib-release-center` 补齐最小运行时后端，而不是继续修改客户端插件逻辑。
- 当前最短闭环是：`PostgreSQL + PluginAtSite + Active Binding + 3 个运行时 RPC`。
- 一旦这条链路跑通，`PatientRegistration` 的正式资源消费就能进入可验证状态，后续再逐步扩充资源中心治理能力。
