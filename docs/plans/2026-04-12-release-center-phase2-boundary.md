# 发布中心 Phase2 边界清单（站点授权）

## 1. 目标

本阶段只做“站点授权闭环”，不扩展到无关体验优化。

目标闭环：

1. 客户端具备 `site identity + heartbeat` 上报
2. 服务端具备站点授权基础数据结构与运行时 RPC
3. 发布中心可管理站点分组、授权策略、站点覆盖
4. 运行时插件清单可按站点裁剪
5. 具备基础站点统计页（只要求可用，不做复杂交互优化）

## 2. 分支与工作区

- 分支：`feat/release-center-phase2-site-authorization`
- 工作区：`.worktrees/release-center-phase2-site-authorization`
- 基线：`feat/dib-release-center`（当前 `4d722f6`）

## 3. 纳入范围（必须）

### 3.1 数据层与 RPC

建议纳入提交：

- `278ea3b` `feat(release-center): add site authorization schema`
- `39de78d` `feat(release-center): add runtime site rpc flow`
- `0284e66` `feat(release-center): default new sites to base authorization`（默认授权基线）

关键对象：

- `site_groups`
- `sites`
- `group_plugin_policies`
- `site_plugin_overrides`
- `site_heartbeats`
- `register_site_heartbeat(...)`
- `get_site_plugin_manifest(...)`

### 3.2 发布中心服务与页面

建议纳入提交：

- `8d76bf6` `feat(release-center): add site authorization repositories`
- `9813bdb` `feat(release-center): scope plugin manifest by site`
- `1e7568a` `feat(release-center): add site management page`
- `9294d3b` `feat(release-center): add site analytics page`
- `257d318` `feat(release-center): add site policy management pages`
- `eb6bfe9` `docs(release-center): document site authorization workflow`

### 3.3 客户端联动

建议纳入提交：

- `36b69ef` `feat(dib): add site identity and heartbeat`
- `34adf5e` `feat(dib): show site authorization summary in settings`

## 4. 排除范围（本阶段不做）

以下提交可延后到 Phase2.5/Phase3，不要混入第一批：

- `9226932` `enhance site analytics overview`
- `41a0420` `add site filtering and bulk assignment`
- `d9587e6` `link site analytics to site actions`
- `960535e` `add site override filtering`
- `54abff5` `add quick site assignment from analytics`
- `8500344` `add analytics issue rows filtering`
- `6eae0ce` `refine analytics issue table filters`
- `7ff067a` `highlight assigned site in analytics`
- `6b81698` `filter analytics issues by client version`
- `5f4d6d9` `add quick site management filters`
- `fa7317b` `reset site management filters`
- `d75fbf0` `align analytics filter reset behavior`
- `942cdff` `align site filter layouts`

说明：这些都偏“交互优化”，不影响站点授权闭环成立。

## 5. 推荐落地顺序

1. 先落数据结构与 RPC（3.1）
2. 再落发布中心仓储与基础页面（3.2）
3. 最后落客户端联动（3.3）
4. 每一组通过后再进入下一组

## 6. 每组最小验收

### 6.1 数据层

1. SQL 全部可执行
2. `register_site_heartbeat` 可调用成功
3. `get_site_plugin_manifest` 可返回按站点结果

### 6.2 发布中心

1. 可查看站点列表
2. 可配置分组与策略
3. 可查看基础统计页

### 6.3 客户端

1. 启动后生成/持久化站点标识
2. 检查更新流程触发 heartbeat
3. 拉取插件清单时携带站点上下文

## 7. 风险控制

1. 每批 cherry-pick 后先跑本地构建与单测
2. 不引入 `.env`、本地脚本输出等本地文件
3. 发现冲突涉及“第一阶段已收口逻辑”时，优先保持第一阶段行为不回退
