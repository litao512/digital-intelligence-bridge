# 资源中心最小运行时闭环实施计划

> 执行说明：按任务顺序逐项实现，并在每个关键步骤后执行可验证检查。

**Goal:** 在 `dib-release-center` 中补齐资源中心一期最小运行时后端，让 DIB 客户端能够正式同步 `patient-registration / registration-db` 授权资源。

**Architecture:** 基于现有 `dib_release` schema 继续扩展最小资源主数据、密钥、绑定和申请表，并新增 3 个运行时 RPC。客户端保持现有调用方式不变，只通过新 RPC 获取授权资源与发现结果。

**Tech Stack:** Supabase/PostgreSQL、SQL migration 脚本、PostgREST RPC、现有 DIB 客户端 `ReleaseCenterService`、xUnit / Vitest（如适用）、串行 .NET 验证。

---

### Task 1: 固化资源中心最小运行时设计文档

**Files:**
- Create: `docs/plans/2026-04-21-resource-center-minimal-runtime-design.md`
- Modify: `docs/plans/README.md`

**Step 1: 写设计文档**

写入：
- 最小对象模型
- 3 个运行时 RPC
- 当前客户端与后端的职责边界
- 明确本轮不做的范围

**Step 2: 更新计划导航**

把设计文档加入 `docs/plans/README.md` 的“当前有效文档”。

**Step 3: 运行文档语言检查**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: 新文档通过语言检查。

### Task 2: 为 `dib-release-center` 增加资源中心最小表结构

**Files:**
- Create: `dib-release-center/supabase/sql/17_create_resource_center_runtime_tables.sql`

**Step 1: 编写最小建表 SQL**

至少创建：
- `dib_release.resource_plugins`
- `dib_release.resources`
- `dib_release.resource_secrets`
- `dib_release.resource_bindings`
- `dib_release.resource_applications`

**Step 2: 编写必要索引**

至少覆盖：
- `resource_plugins.plugin_code`
- `resource_bindings.site_id + plugin_code + status`
- `resource_applications.site_id + plugin_code + status`

**Step 3: 编写最小权限**

确保运行时 RPC 所需表具备最小可访问权限。

**Step 4: 自查字段语义**

确认：
- 绑定粒度先只支持 `PluginAtSite`
- `usage_key` 直接存入绑定表
- `config_payload` 与 `secret_payload` 分离

### Task 3: 增加资源中心最小种子数据

**Files:**
- Create: `dib-release-center/supabase/sql/18_seed_resource_center_runtime_baseline.sql`

**Step 1: 初始化插件主数据**

至少插入：
- `patient-registration`

**Step 2: 准备最小资源和绑定样例**

插入一条可供本地联调的 `PostgreSQL` 资源和可选样例绑定，便于验证。

**Step 3: 保持幂等**

所有 seed 必须可重复执行，不得因重复插入直接失败。

### Task 4: 实现 `get_site_authorized_resources` RPC

**Files:**
- Create: `dib-release-center/supabase/sql/19_create_get_site_authorized_resources_rpc.sql`

**Step 1: 先写返回结构**

返回字段至少包含：
- `resourceId`
- `resourceCode`
- `resourceName`
- `resourceType`
- `bindingScope`
- `pluginCode`
- `configVersion`
- `configPayload`
- `capabilities`

**Step 2: 实现最小查询**

按：
- `site_id`
- `plugin_code`
- `status = Active`

查询 `resource_bindings` 和 `resources`。

**Step 3: 合并密钥负载**

把 `resources.config_payload` 与 `resource_secrets.secret_payload` 合并为最终 `configPayload`。

**Step 4: 授权执行权限**

授予 `anon, authenticated, service_role` 执行权限，保持与现有运行时 RPC 一致。

### Task 5: 实现 `discover_site_resources` RPC

**Files:**
- Create: `dib-release-center/supabase/sql/20_create_discover_site_resources_rpc.sql`

**Step 1: 返回最小三段结果**

返回：
- `availableToApply`
- `authorized`
- `pendingApplications`

**Step 2: `authorized` 复用授权查询逻辑**

保持与 `get_site_authorized_resources` 的字段语义一致。

**Step 3: `availableToApply` 最小实现**

先返回：
- 当前未绑定到该站点该插件的有效资源
- `visibility_scope` 允许公开或站点可见的资源

**Step 4: `pendingApplications` 最小实现**

查询当前 `site_id + plugin_code` 下未关闭申请。

### Task 6: 实现 `apply_site_resource` RPC

**Files:**
- Create: `dib-release-center/supabase/sql/21_create_apply_site_resource_rpc.sql`

**Step 1: 写最小插入逻辑**

插入：
- `site_id`
- `plugin_code`
- `resource_id`
- `reason`
- `status = Submitted`

**Step 2: 返回当前客户端契约**

返回：
- `success`
- `message`
- `applicationId`
- `status`

**Step 3: 保持幂等和最小校验**

至少校验：
- 资源存在
- 站点存在
- 插件编码非空

### Task 7: 更新 `dib-release-center` 文档

**Files:**
- Modify: `dib-release-center/README.md`
- Modify: `dib-release-center/docs/PROD101_RELEASE_CENTER_OPERATIONS_GUIDE.md`

**Step 1: 更新当前状态**

在 `README.md` 中补充：
- 资源中心最小运行时表
- 3 个新 RPC

**Step 2: 更新运维手册**

补充：
- 如何检查 3 个 RPC 是否存在
- 如何插入测试资源和绑定
- 如何验证客户端已同步到授权资源

### Task 8: 客户端端到端验证

**Files:**
- Modify: `digital-intelligence-bridge.UnitTests/ReleaseCenterServiceTests.cs`
- Modify: `digital-intelligence-bridge.UnitTests/PluginHostContextResourceTests.cs`

**Step 1: 补客户端资源中心契约测试**

覆盖：
- `get_site_authorized_resources` 返回成功时写缓存
- `discover_site_resources` 返回结构正确
- `apply_site_resource` 返回结构正确

**Step 2: 串行运行目标测试**

Run: `dotnet test digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --filter "ReleaseCenterServiceTests|PluginHostContextResourceTests|PatientRegistrationRuntimeResourceSettingsTests" --no-restore -m:1 -v minimal`
Expected: 相关测试全部通过。

**Step 3: 串行构建主项目与测试项目**

Run: `dotnet build digital-intelligence-bridge/digital-intelligence-bridge.csproj --no-restore -m:1 -c Debug -v minimal`
Expected: 构建通过。

Run: `dotnet build digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj --no-restore -m:1 -v minimal`
Expected: 构建通过。

### Task 9: 真实环境回归

**Files:**
- Modify: `C:\\Users\\Administrator\\AppData\\Local\\DibClient\\appsettings.json`（仅本机验证，不提交）

**Step 1: 确认资源中心配置**

确认：
- `ReleaseCenter.Enabled = true`
- `ReleaseCenter.BaseUrl`
- `ReleaseCenter.Channel`
- `ReleaseCenter.AnonKey`

**Step 2: 执行 SQL**

把 `17` 到 `21` 号 SQL 脚本应用到 `dib_release` schema。

**Step 3: 插入测试资源与绑定**

至少让：
- `patient-registration`
- `registration-db`
- 当前 `SiteId`

形成一条有效 `Active / PluginAtSite` 绑定。

**Step 4: 重启客户端并检查日志**

检查：
- 不再出现资源中心 `404`
- 生成 `%LocalAppData%\\DibClient\\resource-cache\\authorized-resources.json`
- `patient-registration` 能读取到 `registration-db`

### Task 10: 提交收尾验证

**Files:**
- Verify only

**Step 1: 文档语言检查**

Run: `powershell -ExecutionPolicy Bypass -File scripts/check-doc-lang.ps1`
Expected: 通过。

**Step 2: 差异检查**

Run: `git diff --check`
Expected: 仅允许既有 LF/CRLF 警告，不得出现新的格式错误。

**Step 3: 汇报结果**

汇报：
- 哪些 SQL 已新增
- 哪些 RPC 已新增
- 客户端是否已生成授权缓存
- `PatientRegistration` 是否已通过正式资源中心链路取得连接
