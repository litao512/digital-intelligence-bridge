# 2026-02-20 Supabase 写操作端到端回归清单

## 目标
- 覆盖 `dib.todos` 的新增（POST）、更新（PATCH）、删除（DELETE）全链路。
- 校验异常场景的降级行为与日志提示，确保桌面端不中断。

## 前置条件
- 已在 Supabase 执行 `database/migrations/2026-02-19-create-dib-schema-up.sql`，schema `dib` 可用。
- 通过 `./scripts/new-runtime-config.ps1 -SupabaseUrl ... -SupabaseAnonKey ... -Schema dib` 写入 `%LOCALAPPDATA%/UniversalTrayTool/appsettings.runtime.json`。
- 连通性自检：`./scripts/verify-supabase-runtime.ps1` 返回 HTTP 200，`Accept-Profile: dib`。
- 建议准备唯一前缀（如 `supabase-e2e-YYYYMMDDHHmm`），测试后用 REST 清理残留数据。
- 参考日志位置：`%LOCALAPPDATA%/UniversalTrayTool/logs/*.log`；关注 `SupabaseTodoRepository` 警告。

## 检查项

### 1) 新增成功（POST /rest/v1/todos）
1. 启动应用，进入“待办事项”，添加标题 `supabase-e2e-add-<ts>`，填写描述/标签/截止日期。
2. 预期 UI：列表新增一条，统计面板总数 +1。
3. 预期后端：PowerShell 验证
   ```powershell
   $headers = @{
     apikey = $env:SUPABASE_ANON_KEY
     Authorization = "Bearer $env:SUPABASE_ANON_KEY"
     "Accept-Profile" = "dib"
   }
   Invoke-RestMethod -Method Get -Uri "$env:SUPABASE_URL/rest/v1/todos?title=eq.supabase-e2e-add-<ts>&select=id,title,is_completed,priority,category,tags,due_date" -Headers $headers
   ```
   返回数组包含新增行，`is_completed=false`，字段与 UI 输入一致。

### 2) 更新成功（PATCH /rest/v1/todos?id=eq.{id}）
1. 在列表勾选第 1 步创建的任务为完成，再次勾掉（覆盖 true/false 双向）。
2. 预期 UI：状态切换、完成时间显示变化。
3. 预期后端：同一 `id` 的 `is_completed` 与 `completed_at` 随勾选切换；`updated_at` 变更。

### 3) 删除成功（DELETE /rest/v1/todos?id=eq.{id}）
1. 点击目标行“删除”，确认列表移除；统计面板同步减少。
2. REST 验证：`GET /rest/v1/todos?title=eq.supabase-e2e-add-<ts>` 返回空数组。
3. 追加批量删除验证（可选）：创建 2 条完成态任务，点击“清除已完成”，确认 `DELETE /rest/v1/todos?is_completed=eq.true` 生效且列表清空完成态。

### 4) 异常处理 - 后端不可达/网络失败
1. 将运行时配置中的 `Supabase.Url` 改为无效主机，重启应用。
2. 添加任务 `supabase-e2e-offline`。
3. 预期 UI：任务正常出现在列表，应用无崩溃。
4. 预期日志：`Failed to add todo to Supabase` 或 `Todo 已在本地更新，但写入 Supabase 失败` 警告出现。
5. REST 验证：真实 Supabase 中不存在 `supabase-e2e-offline` 记录（仅本地内存回退）。
6. 还原正确配置后重新启动。

### 5) 异常处理 - 鉴权失败（401/403）
1. 将 `Supabase.AnonKey` 改为错误值，重启应用。
2. 删除一条已有任务或执行“清除已完成”。
3. 预期 UI：操作完成但无崩溃。
4. 预期日志：`Failed to delete todo from Supabase. Status=401/403` 或 `Todo 已在本地删除，但同步 Supabase 失败`。
5. REST 验证：原记录仍留在 Supabase（因写入拒绝），需手动清理；恢复正确密钥后重试。

### 6) 收尾
- 使用前缀筛选删除测试数据：`DELETE /rest/v1/todos?title=like.supabase-e2e-%`（Accept-Profile/Content-Profile: dib）。
- 记录执行日期、真实 URL、使用的 anon key 来源（不写入密钥值）到测试凭证登记处。
