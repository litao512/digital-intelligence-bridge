# 运行时目录说明

## 1. 目标

客户端是桌面托盘宿主，程序目录与运行时目录必须职责清晰。

当前正式规则：

1. 程序目录只放安装包内容
2. `%LocalAppData%\DibClient` 放所有可变运行时数据

不做旧目录兼容，不允许再回到“程序目录 + AppData 混用”的模式。

## 2. 目录职责

### 2.1 程序目录

程序目录是安装包内容所在目录。

只允许放：

- `digital-intelligence-bridge.exe`
- 程序依赖 DLL
- 安装目录 `appsettings.json`
- 只读资源文件

不允许放：

- 日志
- 正式插件
- 插件缓存
- 预安装目录
- 备份目录
- 用户状态配置

### 2.2 AppData 运行时目录

运行时根目录固定为：

- `%LocalAppData%\DibClient`

该目录承载所有运行时可变数据。

## 3. 目录结构

```text
%LocalAppData%\DibClient\
├── appsettings.json
├── logs\
├── plugins\
├── release-cache\
│   └── plugins\
├── release-staging\
│   └── plugins\
└── release-backups\
    └── plugins\
```

## 4. 各目录说明

### 4.1 `appsettings.json`

本机用户状态配置。

用于保存：

- `SiteId`
- `SiteName`
- `SiteRemark`
- 用户偏好

### 4.2 `logs\`

日志目录。

规则：

1. 日志统一写入该目录
2. 不允许再写回程序目录 `logs\`

### 4.3 `plugins\`

正式生效插件目录。

规则：

1. 宿主启动时只从这里扫描运行时插件
2. 插件激活后写入这里
3. 菜单只展示已加载插件
4. 默认路径是 `%LocalAppData%\DibClient\plugins`
5. 如果设置 `DIB_CONFIG_ROOT`，则实际路径切换为 `<DIB_CONFIG_ROOT>\plugins`
6. 仓库根目录 `plugins\` 仅用于源码仓库内的样例与发布物整理，不应被当作正式运行目录

### 4.4 `release-cache\plugins\`

下载缓存目录。

用于保存从发布中心下载的插件 zip 包。

### 4.5 `release-staging\plugins\`

预安装目录。

用于解压插件包，生成待生效目录。

该目录内容不会直接视为已生效插件。

### 4.6 `release-backups\plugins\`

备份目录。

用于保存重启激活前的旧插件目录。

## 5. 插件生效流程

插件从发布中心下载到真正生效，固定经过三段目录：

1. `release-cache\plugins\`
2. `release-staging\plugins\`
3. `plugins\`

流程说明：

1. 下载插件包
2. 生成预安装目录
3. 重启 DIB
4. 启动时把预安装目录激活到正式 `plugins\`

因此：

- 下载完成不等于生效
- 预安装完成不等于生效
- 必须重启后才生效

## 6. 运维与排障

排障时优先查看以下位置：

1. `%LocalAppData%\DibClient\appsettings.json`
2. `%LocalAppData%\DibClient\logs`
3. `%LocalAppData%\DibClient\plugins`
4. `%LocalAppData%\DibClient\release-staging\plugins`

不要优先检查程序目录是否存在运行时插件或日志，因为那不再是正式行为。

### 6.1 插件目录判定顺序

当怀疑“插件没有加载到最新版本”时，先按以下顺序确认：

1. 先看是否设置了 `DIB_CONFIG_ROOT`
2. 再确认实际运行时插件目录是否为 `%LocalAppData%\DibClient\plugins`
3. 再确认目标插件目录下的 `plugin.json`、主程序集和依赖文件是否已更新

不要先看仓库根目录 `plugins\`，也不要先看 `plugins-src\`。

### 6.2 手工回归证据路径

手工回归插件加载时，优先使用以下证据链：

1. 运行时插件目录中的实际文件
2. `%LocalAppData%\DibClient\logs\app-*.log`
3. 日志中的插件初始化或加载失败信息

推荐判断口径：

1. 目录里有文件，不等于插件已成功加载
2. 以日志中的“插件已初始化”或“插件加载失败”作为最终判定依据
3. 当多个插件同时存在时，不要用某一个插件失败去否定另一个插件的回归结果

## 7. 相关文档

- [CONFIGURATION_ARCHITECTURE_SPEC.md](./CONFIGURATION_ARCHITECTURE_SPEC.md)
- [PLUGIN_PACKAGING_GUIDE.md](./PLUGIN_PACKAGING_GUIDE.md)
- [NEW_MACHINE_SETUP_GUIDE.md](./NEW_MACHINE_SETUP_GUIDE.md)
