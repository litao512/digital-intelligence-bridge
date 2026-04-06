# DIB 站点授权与统计设计

**目标**
- 为每个 DIB 客户端安装实例建立稳定站点身份。
- 发布中心支持按站点分组批量授权插件，并允许少量站点级覆盖。
- 发布中心提供面向运营的站点统计分析，用于查看在线情况、版本分布、授权与安装差异。

**设计范围**
- 站点的含义限定为一个 DIB 客户端安装实例，不等同于医院、院区或业务租户。
- 本轮只做插件级授权，不做插件内部功能点授权。
- 客户端版本更新清单继续按 channel 维度发布；插件清单升级为按站点裁剪。

## 一、站点标识与注册

### 1. 站点唯一标识
- DIB 客户端首次启动时生成 `site_id`（GUID）。
- `site_id` 持久化到本地配置，后续升级不变。
- 用户可编辑 `site_name`，用于运营侧识别，例如“门诊一楼登记台”。
- 不使用 `machine_name` 或医院名称作为唯一主键，避免变更和重名问题。

### 2. 站点注册流程
- 客户端启动或执行“检查更新”时，向发布中心发送站点注册/心跳。
- 若站点不存在，则自动创建并进入默认分组 `unassigned`。
- 若站点已存在，则更新：
  - `client_version`
  - `machine_name`
  - `last_seen_at`
  - 最近检查更新时间
  - 已安装插件摘要

## 二、数据模型

### 1. `sites`
一条记录代表一个 DIB 安装实例。
建议字段：
- `id`
- `site_id`
- `site_name`
- `group_id`
- `channel`
- `client_version`
- `machine_name`
- `last_seen_at`
- `last_update_check_at`
- `last_plugin_download_at`
- `last_client_download_at`
- `installed_plugins_json`
- `is_active`
- `created_at`
- `updated_at`

### 2. `site_groups`
定义站点分组。
建议字段：
- `id`
- `group_code`
- `group_name`
- `description`
- `is_active`
- `created_at`

### 3. `group_plugin_policies`
定义组级插件授权。
建议字段：
- `id`
- `group_id`
- `plugin_package_id`
- `is_enabled`
- `min_client_version`
- `max_client_version`
- `created_at`

### 4. `site_plugin_overrides`
定义少量站点级覆盖。
建议字段：
- `id`
- `site_id`
- `plugin_package_id`
- `action`（`allow` / `deny`）
- `reason`
- `is_active`
- `created_at`

### 5. `site_heartbeats`
保留站点历史活动记录，支撑统计。
建议字段：
- `id`
- `site_id`
- `client_version`
- `channel`
- `installed_plugins_json`
- `event_type`
- `created_at`

## 三、授权规则

### 1. 规则优先级
按以下顺序计算最终插件授权：
1. 站点所属组的默认授权
2. 应用站点级覆盖（`allow` / `deny`）
3. 再按客户端版本范围过滤

### 2. 为什么采用“组授权 + 站点覆盖”
- 大部分站点应按分组集中管理。
- 少数例外站点需要个别处理。
- 如果没有覆盖层，会被迫为少量例外创建大量分组，后续维护会失控。

## 四、Manifest 策略

### 1. 客户端 Manifest
- 继续按 `channel` 生成。
- 当前不按站点差异化客户端更新。

### 2. 插件 Manifest
从当前“按 channel 发布全部插件”，升级为“按站点裁剪插件列表”。
输入参数：
- `channel`
- `site_id`
- `client_version`

输出结果：
- 当前站点被授权且兼容的插件列表。

这意味着不同站点即使都在 `stable`，拉取到的 `plugin-manifest` 也可以不同。

## 五、统计分析范围

### 1. 站点概览
- 总站点数
- 活跃站点数
- 近 24 小时活跃站点数
- 未分组站点数
- 各分组站点数

### 2. 客户端版本分布
- 各客户端版本站点数
- 落后版本站点列表
- 最近未检查更新的站点列表

### 3. 插件授权与安装分布
- 每个插件被授权给多少站点
- 每个插件实际安装于多少站点
- 已授权但未安装的站点
- 已安装但当前不再授权的站点

### 4. 最近活动
- 最近心跳时间
- 最近更新检查时间
- 最近插件下载时间
- 最近客户端下载时间

## 六、发布中心界面建议

### 1. 站点管理页
- 站点列表
- 站点搜索
- 分组筛选
- 最近活跃时间
- 客户端版本
- 分组调整
- 查看站点当前授权结果

### 2. 站点统计页
- 总览卡片
- 分组分布表
- 客户端版本分布表
- 插件授权/安装差异表
- 最近活动列表

## 七、实施顺序建议

### 一期：站点接入与授权
1. DIB 客户端生成并持久化 `site_id`
2. 客户端注册/心跳上报
3. 新增站点与分组授权表
4. 发布中心增加站点管理页
5. 插件 manifest 改为按站点裁剪

### 二期：统计分析
1. 新增站点统计查询
2. 发布中心增加统计页
3. 展示站点分布、版本分布与授权/安装差异

## 八、设计结论
- 站点按 DIB 安装实例建模，而不是业务机构。
- 授权以“站点分组”为主，站点级覆盖为辅。
- 插件清单按站点裁剪，客户端清单保持按 channel。
- 统计范围控制在运营可用级，不做过重的 BI 大屏。
