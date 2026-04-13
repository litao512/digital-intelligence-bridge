# 就诊登记号展示规则（DIB/H5 对齐）

## 背景

就诊登记真实主键为 `patient_visit_registrations.id`（UUID）。  
业务沟通、纸质单据和医生端界面需要更短、更易读的展示编号。

## 统一规则

- 展示格式：`REG-XXXXXXXX`
- 生成方式：`registration_id` 去掉分隔符后取前 8 位并转大写。

示例：

- `123e4567-e89b-12d3-a456-426614174000`
- 展示为 `REG-123E4567`

## 使用边界

- 可用于：
  - 打印单据展示
  - H5 页面展示
  - 医护沟通与人工核对
- 不可用于：
  - 数据库主键查询
  - 作为唯一业务标识替代 UUID

## 当前实现位置

- DIB：`PatientRegistration.Plugin.Utils.RegistrationCodeFormatter`
- H5：`src/utils/registrationCode.ts`

## 变更约束

- 若后续修改展示格式，必须同步更新 DIB 与 H5 两端实现与测试。
- 单测至少覆盖：
  - UUID 输入
  - 已带 `REG-` 前缀输入（H5）
  - 空值输入（H5）

