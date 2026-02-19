# 2026-02-19 Supabase 运行时验收记录

## 验收目标
- 验证客户端运行时配置文件可被正确生成并加载。
- 验证通过运行时配置访问 Supabase `dib` schema 的 `todos` 表。

## 执行命令

```powershell
./scripts/new-runtime-config.ps1 -SupabaseUrl "<from local user config>" -SupabaseAnonKey "<from local user config>" -Schema "dib"
./scripts/verify-supabase-runtime.ps1
```

## 实际结果
- 运行时配置写入：`C:\Users\User\AppData\Local\UniversalTrayTool\appsettings.runtime.json`
- Supabase 验证接口：`GET /rest/v1/todos?select=id&limit=1`
- 返回状态：`200`
- 返回体：非空数组（至少包含一个 `id`）
- GitHub Actions `Supabase Runtime Check`：`22185147286`（`success`）

## 结论
- 当前机器上的运行时配置链路可用。
- 可进入后续手工 UI 回归（托盘显示/切换、设置持久化等）。
