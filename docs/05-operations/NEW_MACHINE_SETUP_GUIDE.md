# 新电脑安装与初始化指南

## 1. 适用场景

本文档用于在全新电脑上安装 DIB 客户端，并完成基础插件初始化。

当前标准链路分为两段：

1. 通过 Web 下载基础包
2. 在 DIB 内完成站点登记和插件初始化

## 2. 下载基础包

当前稳定版基础包下载地址：

- `http://101.42.19.26:8000/storage/v1/object/public/dib-releases/clients/stable/1.0.2/dib-win-x64-portable-1.0.2.zip`

建议：

1. 下载到本地目录，例如 `D:\DIB`
2. 解压后直接运行，不要在压缩包内双击启动

## 3. 启动客户端

解压后运行：

- `digital-intelligence-bridge.exe`

当前分发包为 `self-contained`，不需要额外安装 `.NET Desktop Runtime`。

## 4. 首次登记站点信息

进入设置页，在发布中心区域填写：

1. `站点名称`
2. `站点备注`（可选）

然后点击：

- `保存站点信息`

说明：

1. `SiteId` 会自动生成，无需人工填写
2. 站点名称用于发布中心识别当前终端

## 5. 初始化本机插件

在设置页点击：

- `初始化本机插件`

该动作会自动串行执行：

1. 检查更新
2. 下载插件包
3. 生成预安装目录

成功后界面会提示：

- 基础插件已就绪，重启 DIB 后生效

## 6. 重启生效

点击：

- `重启 DIB`

重启后，预安装目录中的插件会激活到正式插件目录。

## 7. 默认行为说明

新站点首次注册后：

1. 默认进入 `base / 基础授权`
2. 默认获得基础插件授权

当前基础插件为：

1. `patient-registration`（就诊登记）

## 8. 安装完成后的验证

完成初始化并重启后，应确认：

1. 首页能正常打开
2. 左侧菜单中出现已安装插件
3. “就诊登记”插件可以打开

## 9. 常见问题

### 9.1 提示未配置发布中心

原因：

- 本机用户配置缺少 `ReleaseCenter` 关键字段

处理：

1. 关闭 DIB
2. 删除 `%LocalAppData%\UniversalTrayTool\appsettings.json`
3. 重新启动客户端

### 9.2 初始化完成但插件未出现

原因：

- 还未重启 DIB

处理：

1. 点击 `重启 DIB`
2. 重启后再看菜单

### 9.3 插件下载失败

优先检查：

1. 浏览器能否访问发布中心
2. 浏览器能否访问基础包和 manifest 下载地址
3. `%LocalAppData%\UniversalTrayTool\release-cache\plugins\stable` 是否生成文件

## 10. 相关文档

- [CONFIGURATION_ARCHITECTURE_SPEC.md](./CONFIGURATION_ARCHITECTURE_SPEC.md)
- [RUNTIME_DIRECTORY_GUIDE.md](./RUNTIME_DIRECTORY_GUIDE.md)
- [DIB_CONFIG_SAFETY_GUIDE.md](./DIB_CONFIG_SAFETY_GUIDE.md)
- [PLUGIN_PACKAGING_GUIDE.md](./PLUGIN_PACKAGING_GUIDE.md)
