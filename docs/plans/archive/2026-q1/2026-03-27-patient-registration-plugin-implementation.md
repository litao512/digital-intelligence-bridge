# 无手机患者就诊登记与纸质二维码插件 实施计划

> **执行要求：** 使用 `executing-plans` 按任务逐步执行，并在每个任务后做回归验证。

**目标：** 在 DIB 电脑端新增“就诊登记+纸质身份二维码打印”插件，并与 `hospital-appointment-h5` 医生扫码流程联动，支持无手机患者现场新增治疗项目。

**架构：** 采用“统一患者主键、入口分流、诊疗结果汇合”方案。DIB 插件负责建档、登记、打印；H5 医生端负责扫码识别与启动治疗。两端共用同一 Supabase 数据库，统一写入诊疗事实表。

**技术栈：** DIB（.NET + Avalonia + 插件宿主）、H5（Vue3 + TypeScript + Supabase RPC）、PostgreSQL（Supabase）

---

### 任务 1：数据库模型扩展（共享）

**文件：**
- 新增：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/database/50_create_patient_profiles_and_visit_registrations.sql`
- 新增：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/database/tests/patient_registration_contract.sql`
- 修改：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/database/README.md`

**步骤：**
1. 先写数据库契约测试脚本，覆盖：建档去重、诊疗信息可空登记、扫码上下文读取。
2. 执行契约脚本（预期失败），确认当前库不具备新能力。
3. 实现迁移：新增 `patient_profiles`、`patient_visit_registrations`、索引与约束；补 `get_patient_service_context_by_qr` RPC。
4. 重新执行契约脚本（预期通过）。
5. 更新数据库迁移文档与执行顺序说明。

**验证命令：**
- `psql "$DATABASE_URL" -f database/50_create_patient_profiles_and_visit_registrations.sql`
- `psql "$DATABASE_URL" -f database/tests/patient_registration_contract.sql`

---

### 任务 2：H5 医生扫码上下文改造

**文件：**
- 修改：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/src/services/serviceSession.ts`
- 修改：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/src/views/doctor/ScanQRCodeView.vue`
- 新增：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/src/types/patientRegistration.ts`
- 新增：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/tests/unit/doctorScanRegistrationContext.test.ts`

**步骤：**
1. 先补单测：扫码后读取上下文、无预填项目时可现场新增项目。
2. 运行单测（预期失败）。
3. 接入新 RPC：`get_patient_service_context_by_qr`，并在扫码结果弹窗展示登记信息。
4. 保持既有规则：二维码可 JSON/纯文本，至少选择 1 项才能启动服务。
5. 运行新增与相关回归单测（预期通过）。

**验证命令：**
- `npm run test -- --run tests/unit/doctorScanRegistrationContext.test.ts tests/unit/doctorScanQuery.test.ts tests/unit/serviceSession.test.ts`
- `npm run build`

---

### 任务 3：DIB 插件骨架与菜单接入

**文件：**
- 新增目录：`C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/plugins-src/PatientRegistration.Plugin/`
- 新增：`.../PatientRegistration.Plugin.csproj`
- 新增：`.../PatientRegistrationPlugin.cs`
- 新增：`.../plugin.json`
- 新增：`.../plugin.settings.json`
- 新增：`.../Views/PatientRegistrationHomeView.axaml`
- 新增：`.../Views/PatientRegistrationHomeView.axaml.cs`
- 新增：`.../ViewModels/PatientRegistrationViewModel.cs`

**步骤：**
1. 先写最小插件契约测试（可加载、可出现菜单、可创建页面）。
2. 运行测试（预期失败）。
3. 实现插件入口与菜单项（`patient-registration.home`）。
4. 实现基础页面与空状态 ViewModel。
5. 运行 DIB 单元测试与插件加载回归（预期通过）。

**验证命令：**
- `dotnet test C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/digital-intelligence-bridge.UnitTests/digital-intelligence-bridge.UnitTests.csproj`

---

### 任务 4：DIB 就诊登记表单与保存逻辑

**文件：**
- 新增：`.../Services/IPatientRegistrationRepository.cs`
- 新增：`.../Services/PatientRegistrationRepository.cs`
- 新增：`.../Models/PatientProfileDraft.cs`
- 新增：`.../Models/VisitRegistrationDraft.cs`
- 修改：`.../ViewModels/PatientRegistrationViewModel.cs`
- 修改：`.../Views/PatientRegistrationHomeView.axaml`
- 新增测试：`.../digital-intelligence-bridge.UnitTests/PatientRegistrationViewModelTests.cs`

**步骤：**
1. 先写 ViewModel 测试：必填校验、空诊疗二次确认、保存成功。
2. 运行测试（预期失败）。
3. 实现仓储：患者查重（证件）、建档/更新、登记记录写入、身份码复用/生成。
4. 表单联动：科室/医生/项目/时段可选，个人信息必填。
5. 运行测试（预期通过）。

**验证命令：**
- `dotnet test .../digital-intelligence-bridge.UnitTests.csproj --filter "PatientRegistration"`

---

### 任务 5：DIB 打印预览与补打

**文件：**
- 新增：`.../Views/PatientRegistrationPrintView.axaml`
- 新增：`.../ViewModels/PatientRegistrationPrintViewModel.cs`
- 新增：`.../Services/IPrintService.cs`
- 新增：`.../Services/PrintService.cs`
- 修改：`.../Views/PatientRegistrationHomeView.axaml.cs`

**步骤：**
1. 先写打印数据模型测试：二维码码值、患者明文掩码、登记号。
2. 运行测试（预期失败）。
3. 实现打印预览与系统打印调用；支持“登记后立即打印”和“记录页补打”。
4. 增加打印失败提示与重试。
5. 运行测试与手工验证（预期通过）。

**验证命令：**
- `dotnet test .../digital-intelligence-bridge.UnitTests.csproj --filter "Print|PatientRegistration"`

---

### 任务 6：联调与文档

**文件：**
- 新增：`C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/docs/05-operations/PATIENT_REGISTRATION_PLUGIN_GUIDE.md`
- 修改：`C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/README.md`
- 新增：`C:/Users/Administrator/.openclaw/workspace-coding/OnlineAppointment/hospital-appointment-h5/docs/03-features/PATIENT_REGISTRATION_INTEGRATION_SPEC.md`

**步骤：**
1. 补操作文档：窗口登记、打印、补打、异常处理。
2. 补联动文档：H5 医生扫码读取登记上下文与现场新增项目。
3. 更新两个仓库的文档索引。
4. 完整回归：无手机路径、有手机路径、医生端路径。

**验证命令：**
- DIB：`dotnet build C:/Users/Administrator/.openclaw/workspace-his/digital-intelligence-bridge/digital-intelligence-bridge/digital-intelligence-bridge.csproj -c Release`
- H5：`npm run build`

---

### 任务 7：提交与发布准备

**步骤：**
1. 按任务分批提交（数据库、H5、DIB、文档）。
2. 汇总验证证据（构建日志、测试结果、手工联调记录）。
3. 输出上线检查清单（迁移顺序、插件发布目录、回滚方案）。

**建议提交信息：**
- `feat(db): 新增患者档案与就诊登记模型`
- `feat(doctor-scan): 接入登记上下文并支持现场补录项目`
- `feat(dib-plugin): 新增就诊登记与纸质二维码打印插件`
- `docs: 补充就诊登记插件与联动操作指南`
