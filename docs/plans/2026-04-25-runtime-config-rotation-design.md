# 运行配置轮换设计

## 目标

为当前双配置架构补充可审计的配置轮换能力，解决 `TD-002` 中“密钥轮换依赖人工覆盖、容易误操作”的问题。

本设计不恢复 `appsettings.runtime.json`，也不新增第三层配置文件。

## 当前架构约束

正式配置只允许两层：

1. 安装目录 `appsettings.json`
2. `%LOCALAPPDATA%/DibClient/appsettings.json`

安装目录配置承载发布基线，例如 `ReleaseCenter.BaseUrl`、`ReleaseCenter.Channel`、`ReleaseCenter.AnonKey`。用户目录配置只承载本机状态与白名单字段。

## 方案比较

### 方案一：扩展旧运行时配置脚本

优点是改动小。缺点是会继续强化 `appsettings.runtime.json` 这一旧口径，和当前正式配置架构冲突。

结论：不采用。

### 方案二：新增独立配置轮换脚本

新增 `scripts/rotate-runtime-config.ps1`，对指定目标 `appsettings.json` 执行：

1. 校验新配置文件
2. 备份旧目标文件
3. 写入新配置
4. 写入后再次校验
5. 失败时保留旧配置

优点是边界清晰，不影响客户端加载逻辑，也不依赖发布流程。适合用于安装目录基线配置或沙箱配置轮换。

结论：采用。

### 方案三：把轮换能力集成进发布脚本

优点是发布时自动化程度更高。缺点是发布脚本已经负责打包和产物生成，继续加入本机运行配置轮换会混淆“发布产物”和“目标机状态”。

结论：后续如有远程部署流程再考虑。

## 设计

新增独立脚本 `scripts/rotate-runtime-config.ps1`。

参数：

- `SourcePath`：新配置文件路径。
- `TargetPath`：待轮换的目标配置文件路径。
- `BackupDirectory`：备份目录，未指定时使用目标文件所在目录。

校验规则：

- 文件必须是合法 JSON。
- 必须包含 `ReleaseCenter` 节。
- 当 `ReleaseCenter.Enabled = true` 时，必须包含有效的 `BaseUrl`、`Channel`、`AnonKey`。
- 如存在 `Supabase.Url`，必须是 `http` 或 `https` 地址。
- 如存在 `Supabase.Schema`，不得包含空格。

写入规则：

- 目标存在时先备份，备份文件名包含时间戳。
- 新配置先写入目标目录下的临时文件，再替换目标文件。
- 替换后重新校验目标文件。
- 如果替换后校验失败，且存在备份，则恢复备份。

## 测试

新增 `scripts/test-rotate-runtime-config.ps1` 作为轻量脚本测试，不引入 Pester 依赖。

覆盖：

1. 有效新配置会替换目标配置，并生成旧配置备份。
2. 无效新配置会失败，且目标配置保持不变。

## 验收

1. 脚本测试通过。
2. 文档语言检查通过。
3. `git diff --check` 无新增格式错误。
4. 技术债清单中 `TD-002` 更新为当前双配置架构下已完成。
