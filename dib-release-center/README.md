# dib-release-center

`dib-release-center` 是 DIB 的发布中心子项目，用于承载发布管理页面、版本清单生成、Supabase 数据访问与资产发布脚本。

## 目标

- 提供一个最小可构建的 Vue 3 + TypeScript + Vite 管理端
- 提供插件版本、客户端版本与 manifest 的统一发布入口
- 作为 DIB 客户端与 Supabase 之间的发布中枢

## 目录职责

- `src/web/`：页面与视图层
- `src/services/`：业务服务与外部能力封装
- `src/repositories/`：数据访问层
- `src/contracts/`：类型契约与数据结构定义
- `src/utils/`：通用工具函数
- `scripts/`：PowerShell 发布脚本
- `supabase/sql/`：数据库结构与查询脚本
- `storage/manifests/`：生成的 manifest 产物与存储约定

## 当前状态

当前阶段已经具备：

- 管理员登录
- 发布资产登记与文件上传
- 插件版本发布
- 客户端版本发布
- `client-manifest` / `plugin-manifest` 发布
- `prod101` 生产部署入口

## prod101 运维

- 发布中心入口：`http://101.42.19.26:8000/release-center/`
- 运维总手册：`docs/PROD101_RELEASE_CENTER_OPERATIONS_GUIDE.md`
- SQL 执行清单：`docs/PROD101_SQL_RUNBOOK.md`
- 本地健康检查：`scripts/prod101-health-check.ps1`
- 服务器常用操作：`scripts/prod101-supabase-ops.sh`

## 当前生产基线

- schema：`dib_release`
- bucket：`dib-releases`
- 插件包：统一使用 `Supabase Storage`
- 客户端包：统一使用 `Supabase Storage`
- manifest：统一使用 `Supabase Storage`
- `supabase-storage` 文件上限：`256 MB`
