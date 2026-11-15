# 发布检查与缺项 / Release audit

检查日期：2026-11-15。完整度按源码、构建脚本、运行配置、产物和已有验证记录判断，不给没有依据的百分比。

|顺序|版本|位置|本轮构建|状态与限制|
|---|---|---|---|---|
|1|0.1.0|backups/mainline-before-v0.3.0-20261115|通过|单层稳定基线；EXE 内部版本 0.0.0.0|
|2|0.2.0-beta.1|experimental/grouped-dock|通过|分组；EXE 内部版本 0.0.0.0；早期桌面恢复逻辑|
|3|0.2.0-beta.2|experimental/grouped-dock-beta2|通过|回收站、恢复修复、轮询刷新；EXE 内部版本 0.0.0.0|
|4|0.2.0-beta.3|experimental/grouped-dock-beta3|通过|回收站事件刷新；EXE 内部版本 0.0.0.0；README 保留旧轮询说明|
|5|0.2.0-beta.4|experimental/grouped-dock-beta4|通过|悬停信息卡；版本资源正确|
|6|0.3.0-beta.1|experimental/localized-dock-beta1|通过|中英切换；版本资源正确|
|7|0.3.0|根目录|通过|当前主线；版本资源正确；已有干净分发包|

以上是开发演进顺序，不是已经存在的 GitHub 发布时间。原始工作区没有 Git 历史，不能还原精确开发提交时间；不伪造历史发布时间。beta.2 目录已包含后续修复，发布的是当前保留快照。

## 发布需要补充的内容

|项目|当前状态|处理方式|
|---|---|---|
|目标 GitHub 仓库|已确认 ChaiTaishuai/LightDock|仓库公开，插件写入已验证|
|Git 提交及版本标签|7 个源码提交已上传，标签由发布流程创建|每个版本建立独立对应源码提交及标签，避免所有标签指向最新代码|
|双语介绍|已准备 README.md / README.en.md|发布时一并上传|
|Release 文件|本地包已准备，公开包由 Actions 重新构建|7 个 ZIP、独立 SHA256、双语说明、清单|
|稳定版与预发布标记|已识别|5 个 beta 设置 prerelease，仅 0.3.0 设置 latest|
|许可证|未发现授权选择|由作者决定；不能擅自指定 MIT 等许可；不阻止无许可证发布|
|旧 EXE 版本资源|前 4 版为 0.0.0.0|保持历史源码，说明中披露，以标签和清单识别|
|历史说明|部分过时或互相矛盾|Release 说明作为当前快照说明；历史 README 仅作为记录|
|签名|未签名|包名明确 unsigned；签名可选，不能宣称已认证|
|跨电脑、混合 DPI、登录验收|未完成|公开已知验证边界，不能宣称完整实机验收|
|截图|未整理|可选，后续补充真实界面截图|

本轮重新编译全部 7 版，没有启动接管桌面的交互测试。历史主线记录包含 248 项基础断言、106 项分组断言、46 项桌面恢复检查；这不是本轮重新执行结果。

## GitHub 官方依据

- https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository
- https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases

Release 关联标签，可附加二进制附件；GitHub 自动生成的源码 ZIP 不替代可运行分发包。按上述顺序发布，保留真实发布时刻。启用不可变 Release 时，先在草稿中上传完整附件再正式发布。

