# Light Dock

[English](README.en.md) | 简体中文

**把常用入口放在手边，让桌面回归清爽。**

Light Dock 是一款面向 Windows 的轻量桌面图标栏。它把常用程序、文件、文件夹和回收站集中在一级入口，把更多资料收进可展开的二级分组。你可以通过拖放整理项目、按用途创建分组，在需要时展开浏览，用完后收起。

桌面文件越来越多时，找一个常用入口往往要扫视整屏；全部塞进一排启动栏，又会让图标栏越拉越长。Light Dock 用“常用入口 + 按需展开的资料分组”分担这两种需求。收纳保留原文件的位置，退出时恢复桌面图标，便于尝试和回退。

- **常用的放前面**：一级最多 12 个图标，点击直达。
- **更多的按用途收纳**：二级分组支持拖放、排序与滚动。
- **需要时展开，不用时收起**：让资料入口保持可达，减少桌面视觉拥挤。
- **保留自己的习惯**：原文件不搬家，中英界面可切换，外观和悬停提示可调整。

[下载最新版](https://github.com/ChaiTaishuai/LightDock/releases/latest) · [全部版本](https://github.com/ChaiTaishuai/LightDock/releases)

## 使用

Windows 11 x64，或 Windows 10 1607 及以上 x64 且已安装 .NET Framework 4.8。Windows 11 原生圆角及毛玻璃效果在 Windows 10 上可能不同；非本机系统尚未实机验收。

完整解压发布 ZIP 到当前用户可写、路径固定的位置，例如“文档/LightDock”，然后运行 `bin/LightDock.exe`。不要只从 ZIP 内打开 EXE，也不要只复制 EXE；保留其配置文件和目录结构。首次运行使用默认布局，不携带开发者的桌面路径。设置保存在程序上一级的 `data` 文件夹；源码工作区为 `data/settings.xml`，发布包亦为 `data/settings.xml`。勿放在需要管理员权限才能写入的位置。

## 功能

- 顶部图标栏、悬停放大、浅色毛玻璃和系统圆角，普通窗口可覆盖 Dock。
- 一级最多 12 个图标；二级分组、组内滚动、拖拽插入与跨组移动。
- 文件、文件夹、快捷方式、图片预览和回收站；回收站通过 Shell 事件刷新状态图标。
- 双击桌面空白处隐藏或恢复 Dock、桌面图标和任务栏；快捷键临时呼出。
- 收纳图标保留原文件及位置；移出或退出时恢复，异常终止由守护进程尝试恢复。
- 毛玻璃悬停卡片、名称与路径分层；延迟 0.5–2 秒，步长 0.1 秒，默认 1 秒。
- 设置中可选择跟随系统、简体中文或 English。文件名和自定义分组名保留原样。
- Per-Monitor V2 DPI 布局；自由拖动位置；分组布局采用横向。

## 开机自启

设置 → 启动 → “登录 Windows 时启动 Light Dock”。默认关闭，仅登记当前账户。登录时等待 Explorer 桌面就绪，已有 Dock 运行时不再启动第二个实例。Windows 可以延迟执行自启。

移动程序目录、切换版本后，需在新位置重新开启。若在 Windows 的启动应用设置中禁用了 Light Dock，还需在那里重新启用。卸载前关闭自启并通过托盘退出，再删除解压目录；删除 `data` 会清除个人设置。

## 安全与签名

此构建 **未进行 Authenticode 签名**，不是“已认证无病毒”版本。没有为发布包添加杀毒排除或防火墙放行规则。SHA-256 校验值用于比对发布文件，不等同于可信发布者签名。本地测试不代表所有 Windows 版本与杀毒产品的通过认证。

## 历史版本与下载

`main` 展示当前主线。点击下表的“源码”查看对应版本的完整文件，点击“下载”获取可运行程序。GitHub 页面左上角的分支选择器也可切换到 **Tags**。历史标签保留发布时的文件；补充的详细指南维护在当前主线。

|版本|主要变化|详细指南|源码|下载|
|---|---|---|---|---|
|0.1.0|单层稳定基线|[说明](docs/versions/v0.1.0.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.1.0)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.1.0)|
|0.2.0-beta.1|二级分组|[说明](docs/versions/v0.2.0-beta.1.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.1)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.1)|
|0.2.0-beta.2|回收站与拖放改进|[说明](docs/versions/v0.2.0-beta.2.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.2)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.2)|
|0.2.0-beta.3|回收站事件刷新|[说明](docs/versions/v0.2.0-beta.3.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.3)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.3)|
|0.2.0-beta.4|悬停信息卡|[说明](docs/versions/v0.2.0-beta.4.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.2.0-beta.4)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.2.0-beta.4)|
|0.3.0-beta.1|中英文界面|[说明](docs/versions/v0.3.0-beta.1.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.3.0-beta.1)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.3.0-beta.1)|
|0.3.0|当前稳定主线|[说明](docs/versions/v0.3.0.md)|[源码](https://github.com/ChaiTaishuai/LightDock/tree/v0.3.0)|[下载](https://github.com/ChaiTaishuai/LightDock/releases/tag/v0.3.0)|

## 验证和构建

运行 `./build.ps1`。测试结果写入 `dist`，发布包仅包含程序、运行配置、说明及校验值，不包含测试报告、日志或个人配置。`./package-release.ps1` 生成明确标记为 unsigned 的 ZIP。

本机已完成配置/布局、英文窗口、分组、回收站事件及桌面恢复回归。实际注销登录、不同实体分辨率显示器、其他电脑和多款杀毒软件尚未验收。

旧 `docs` 中的开发分析保留为历史资料；当前运行方式以本 README 为准。


