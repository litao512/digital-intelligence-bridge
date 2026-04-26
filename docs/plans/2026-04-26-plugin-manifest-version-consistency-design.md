# 插件清单版本一致性设计

## 背景

病人登记插件发布包中的 `plugin.json.version` 已由 `scripts/publish-plugin-release.ps1` 按发布参数写入，例如当前测试版本为 `1.0.3-dev.1`。升级烟测显示客户端下载、预安装、激活和宿主加载均成功，但插件模块 `PatientRegistrationPlugin.GetManifest()` 仍返回硬编码版本 `0.1.0`。

这会造成两个元数据来源并存：

- 发布事实来源：发布包内的 `plugin.json`。
- 模块自报来源：`PatientRegistrationPlugin.GetManifest()` 中的硬编码对象。

## 目标

将发布包内 `plugin.json` 作为插件版本事实来源，让模块自报清单与发布清单保持一致，并通过发布脚本测试防止回归。

## 设计

`PatientRegistrationPlugin.GetManifest()` 优先从当前插件程序集所在目录读取 `plugin.json`，解析为 `PluginManifest` 后返回。程序集目录在打包、客户端激活和隔离加载场景下都指向插件发布目录，因此可以覆盖发布包真实版本。

当 `plugin.json` 缺失、无法读取、无法解析或缺少关键字段时，方法回退到内置清单。这个回退只服务于开发调试和异常兜底，不作为发布版本来源。

`scripts/test-publish-plugin-release.ps1` 在完成测试发布后增加两类断言：

- `publish/plugin.json.version` 必须等于测试发布版本。
- 从 `publish/PatientRegistration.Plugin.dll` 加载模块并调用 `GetManifest()`，返回版本必须等于 `plugin.json.version`。

## 不做的事

- 不修改 `IPluginModule` 接口。
- 不改变客户端宿主当前以 `plugin.json` 发现插件的机制。
- 不引入全局生成代码机制；病人登记插件当前只需要读取随包清单即可闭环。

## 发布修复策略

代码修复验证通过后，重新构建并上传当前测试版本 `1.0.3-dev.1` 的插件包，覆盖测试环境中同版本资产和版本记录。当前系统尚未上线，同版本测试包修复不会影响正式用户。
