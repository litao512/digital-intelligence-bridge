# 资源中心 API 草案

**目标**

- 定义托盘宿主与资源中心后台之间的最小接口集合。
- 覆盖单位注册、站点注册、资源发现、资源申请、授权查询和审批动作。
- 为后续客户端实现和后台接口开发提供统一契约。

## 一、接口设计原则

- 接口按业务职责拆分，不把“注册”“发现”“授权”“审批”揉成一个万能接口。
- 托盘只消费与本站点、已安装插件有关的数据，不暴露后台全量管理能力。
- 所有审批结果都应可追溯，不依赖前端隐式推断状态。
- 响应结构优先稳定清晰，字段命名与数据契约保持一致。

## 二、单位与站点注册接口

### 1. 查找或注册使用单位

`POST /api/resource-center/organizations/resolve`

**用途**

- 托盘首次注册时根据 `使用单位` 尝试查找单位。
- 若允许自动创建且单位不存在，则创建单位；否则返回待审批状态。

**请求示例**

```json
{
  "organizationName": "示例医院",
  "source": "ClientRegistration",
  "allowAutoCreate": true
}
```

**响应示例**

```json
{
  "success": true,
  "organization": {
    "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
    "organizationCode": "org-demo-hospital",
    "organizationName": "示例医院",
    "status": "Active"
  },
  "resolution": "Matched"
}
```

`resolution` 建议支持：

- `Matched`
- `Created`
- `PendingApproval`
- `Rejected`

### 2. 注册或更新站点

`POST /api/resource-center/sites/register`

**用途**

- 托盘启动时上报站点身份、单位信息、版本和已安装插件摘要。
- 若站点不存在则创建，存在则更新心跳和上下文。

**请求示例**

```json
{
  "siteId": "b84f0ac6-7f83-4a14-a65d-7e8d3ac3389e",
  "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
  "siteName": "门诊一楼登记台",
  "siteRemark": "东区大厅",
  "machineName": "DIB-KIOSK-01",
  "channel": "stable",
  "clientVersion": "1.0.0",
  "installedPlugins": [
    {
      "pluginCode": "medical-drug-import",
      "pluginVersion": "1.2.0"
    }
  ]
}
```

**响应示例**

```json
{
  "success": true,
  "site": {
    "siteId": "b84f0ac6-7f83-4a14-a65d-7e8d3ac3389e",
    "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
    "siteName": "门诊一楼登记台",
    "status": "Active"
  }
}
```

## 三、资源注册申请接口

### 1. 提交资源注册申请

`POST /api/resource-center/resources/applications/register`

**用途**

- 医疗机构或管理员申请把新资源纳入资源中心。

**请求示例**

```json
{
  "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
  "resourceName": "门诊业务 PostgreSQL",
  "resourceType": "PostgreSQL",
  "visibilityScope": "Private",
  "configPayload": {
    "host": "10.10.1.10",
    "port": 5432,
    "database": "outpatient"
  },
  "secretPayload": {
    "username": "app_user",
    "password": "***"
  },
  "reason": "供门诊业务插件使用"
}
```

**响应示例**

```json
{
  "success": true,
  "application": {
    "applicationId": "7dd406fb-a9c3-43a3-b1d8-aac1f7a96de5",
    "applicationType": "RegisterResource",
    "status": "Submitted"
  }
}
```

## 四、资源发现与授权接口

### 1. 查询可发现资源

`POST /api/resource-center/resources/discover`

**用途**

- 托盘根据当前单位、站点和已安装插件获取“可申请资源”和“已授权资源”。

**请求示例**

```json
{
  "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
  "siteId": "b84f0ac6-7f83-4a14-a65d-7e8d3ac3389e",
  "plugins": [
    {
      "pluginCode": "medical-drug-import",
      "requirements": [
        {
          "resourceType": "PostgreSQL",
          "usageKey": "business-db",
          "required": true
        }
      ]
    }
  ]
}
```

**响应示例**

```json
{
  "success": true,
  "availableToApply": [
    {
      "resourceId": "8e903740-b757-4bb1-8246-884bc0aa6617",
      "resourceCode": "postgres-outpatient-01",
      "resourceName": "门诊业务 PostgreSQL",
      "resourceType": "PostgreSQL",
      "visibilityScope": "Shared",
      "matchedPlugins": ["medical-drug-import"]
    }
  ],
  "authorized": [
    {
      "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
      "resourceCode": "ocr-gateway",
      "resourceName": "OCR 网关",
      "resourceType": "HttpService",
      "bindingScope": "PluginAtSite",
      "pluginCode": "patient-registration",
      "configVersion": 3,
      "configPayload": {
        "baseUrl": "https://ocr.example.local",
        "timeoutSeconds": 20
      },
      "secretRef": "vault://resource-center/ocr-gateway/token"
    }
  ],
  "pendingApplications": [
    {
      "applicationId": "1d7f2a4d-c5b6-4723-9dbd-bac2f3d272a9",
      "applicationType": "UseResource",
      "resourceId": "8e903740-b757-4bb1-8246-884bc0aa6617",
      "status": "UnderReview"
    }
  ]
}
```

### 2. 提交资源使用申请

`POST /api/resource-center/resources/applications/use`

**用途**

- 托盘对某个资源发起使用申请，绑定目标可细化到站点和插件。

**请求示例**

```json
{
  "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
  "siteId": "b84f0ac6-7f83-4a14-a65d-7e8d3ac3389e",
  "pluginCode": "medical-drug-import",
  "resourceId": "8e903740-b757-4bb1-8246-884bc0aa6617",
  "bindingScope": "PluginAtSite",
  "reason": "门诊药品导入插件需要读取业务库"
}
```

**响应示例**

```json
{
  "success": true,
  "application": {
    "applicationId": "3d59f899-b31a-4d87-a19e-f8160c15c7fd",
    "applicationType": "UseResource",
    "status": "Submitted"
  }
}
```

### 3. 查询已授权资源

`POST /api/resource-center/resources/authorized`

**用途**

- 托盘在启动或定时同步时获取当前站点与插件可直接使用的资源列表。

**请求示例**

```json
{
  "organizationId": "6f6e8a8f-3f7c-4e25-a985-6e1ef3ef96d9",
  "siteId": "b84f0ac6-7f83-4a14-a65d-7e8d3ac3389e",
  "pluginCodes": [
    "medical-drug-import",
    "patient-registration"
  ]
}
```

**响应示例**

```json
{
  "success": true,
  "resources": [
    {
      "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
      "resourceCode": "ocr-gateway",
      "resourceType": "HttpService",
      "pluginCode": "patient-registration",
      "bindingScope": "PluginAtSite",
      "configVersion": 3,
      "configPayload": {
        "baseUrl": "https://ocr.example.local",
        "timeoutSeconds": 20
      },
      "secretRef": "vault://resource-center/ocr-gateway/token",
      "capabilities": ["invoke"]
    }
  ]
}
```

## 五、审批接口

### 1. 查询待审批申请

`GET /api/resource-center/applications/pending`

**用途**

- 后台审批端获取待处理申请列表。

### 2. 审批动作

`POST /api/resource-center/applications/{applicationId}/decision`

**请求示例**

```json
{
  "action": "Approve",
  "comment": "业务用途明确，批准使用",
  "operator": "admin@dib.local"
}
```

`action` 建议支持：

- `Approve`
- `Reject`
- `Return`
- `Cancel`

**响应示例**

```json
{
  "success": true,
  "applicationId": "3d59f899-b31a-4d87-a19e-f8160c15c7fd",
  "status": "Approved",
  "generatedBindingIds": [
    "c985cd4c-02ca-4cc8-96be-9d9984fd0bf9"
  ]
}
```

## 六、托盘运行时资源描述对象

后台返回给托盘的运行时资源对象，建议采用统一结构：

```json
{
  "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
  "resourceCode": "ocr-gateway",
  "resourceName": "OCR 网关",
  "resourceType": "HttpService",
  "pluginCode": "patient-registration",
  "bindingScope": "PluginAtSite",
  "configVersion": 3,
  "configPayload": {
    "baseUrl": "https://ocr.example.local",
    "timeoutSeconds": 20
  },
  "secretPayload": null,
  "secretRef": "vault://resource-center/ocr-gateway/token",
  "capabilities": ["invoke"]
}
```

说明：

- `configPayload` 放非敏感配置。
- `secretPayload` 仅在第一阶段确需宿主直接解密时返回。
- 长期目标优先使用 `secretRef`，由宿主在受控环境中解析。

## 七、错误响应建议

统一错误结构建议：

```json
{
  "success": false,
  "code": "organization_not_found",
  "message": "使用单位不存在，且当前不允许自动创建。",
  "details": null
}
```

常见错误码建议：

- `organization_not_found`
- `organization_pending_approval`
- `site_disabled`
- `resource_not_visible`
- `resource_not_found`
- `binding_conflict`
- `application_already_pending`
- `approval_action_invalid`

## 八、第一阶段实现范围建议

第一阶段建议最小实现：

1. `Organization` 解析或创建
2. `Site` 注册与更新
3. 资源发现
4. 资源使用申请
5. 已授权资源查询
6. 审批动作接口

资源注册申请可与后台管理端同步推进，但不要求托盘第一阶段必须提供完整注册界面。

## 九、设计结论

- API 应围绕注册、发现、申请、授权、审批五类职责展开。
- 托盘运行时只需要消费“可申请资源”“已授权资源”“待审批申请”三类结果。
- 后续即使引入 `Nacos`、`Apollo` 或 `Vault`，业务 API 仍应保持稳定。
