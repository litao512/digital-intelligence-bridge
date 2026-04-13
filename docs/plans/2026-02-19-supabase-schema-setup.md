# Supabase Schema 配置说明（`dib`）

> 状态：历史记录（不作为当前待办）
> 最后复核：2026-04-13

日期：2026-02-19

## 目的
为本项目使用独立 schema `dib`，避免与其他项目表结构混用。

## 应用迁移
1. 打开本地部署 Supabase 实例的 SQL Editor。
2. 执行：`database/migrations/2026-02-19-create-dib-schema-up.sql`

## 回滚（如需）
执行：`database/migrations/2026-02-19-create-dib-schema-down.sql`

## 通过 REST 验证
- 接口：`GET /rest/v1/todos?select=id&limit=1`
- 请求头：
  - `apikey: <anon key>`
  - `Authorization: Bearer <anon key>`
  - `Accept-Profile: dib`

预期结果：
- 返回 `200` 且响应体为数组（空数组也视为正常）
