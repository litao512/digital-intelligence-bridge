# 就诊登记效率优化 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将无手机患者就诊登记界面改造成高效率录入流程，减少前台单笔登记耗时并降低误填率。

**Architecture:** 保持现有仓储与打印服务不变，仅在 PatientRegistration 插件的 ViewModel 与 Avalonia 视图层做增量改造。通过“身份信息优先 + 诊疗信息折叠 + 固定操作栏 + 保存并下一位”的交互实现效率提升，并用单元测试约束关键行为。

**Tech Stack:** .NET 10, Avalonia UI, xUnit

---

### Task 1: 行为测试先行（TDD Red）

**Files:**
- Modify: `digital-intelligence-bridge.UnitTests/PatientRegistrationViewModelTests.cs`

1. 新增失败测试：`SaveForNextAsync` 成功后清空身份字段、保留诊疗默认。
2. 新增失败测试：当选择科室后，医生列表仅展示该科室医生。
3. 运行针对性测试并确认先失败。

### Task 2: ViewModel 实现（TDD Green）

**Files:**
- Modify: `plugins-src/PatientRegistration.Plugin/ViewModels/PatientRegistrationViewModel.cs`

1. 增加 `SaveForNextAsync` 与重置策略（清空患者字段，保留诊疗字段）。
2. 增加按科室过滤医生集合与选中医生修正逻辑。
3. 增加分区展开状态与状态提示增强。

### Task 3: 视图改造

**Files:**
- Modify: `plugins-src/PatientRegistration.Plugin/Views/PatientRegistrationHomeView.axaml`
- Modify: `plugins-src/PatientRegistration.Plugin/Views/PatientRegistrationHomeView.axaml.cs`

1. 改为“身份信息/诊疗信息/确认与打印”分区布局。
2. 增加底部固定操作栏与“保存并下一位”按钮。
3. 增加键盘快捷键（Ctrl+S、Ctrl+P）与按钮事件绑定。

### Task 4: 回归验证

**Files:**
- N/A

1. 运行新增与既有 PatientRegistration 相关单测。
2. 运行 UnitTests 全量或最小必要集。
3. 记录未覆盖风险（如真实 UI 焦点跳转行为需手工验证）。
