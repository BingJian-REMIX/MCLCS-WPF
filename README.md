# MCLCS — Minecraft 启动器 (WPF)

> **当前版本：v2.5.4** · C# / WPF / .NET 8 · Windows

MCLCS（Minecraft Launcher CSharp）是一个用 C# / WPF 实现的 Minecraft 启动器，覆盖版本安装、启动、崩溃修复、下载、Mod 管理与工具箱等。

- 仓库（CNB）：<https://cnb.cool/RLRS-Studio/MCLCS-WPF>
- Releases：<https://cnb.cool/RLRS-Studio/MCLCS-WPF/-/releases>

## 功能一览

- **启动与安装**：原版 / Fabric / Forge / Quilt / NeoForge 安装；智能 Java 选择（≥21）；启动前存档兼容性检测与降级；启动预热。
- **崩溃处理**：8 类异常识别与报告，可非破坏性自动修复（内存 / Java / 缺失库），支持始终 / 询问 / 拒绝策略。
- **下载中心**：Modrinth 搜索（版本 / 加载器过滤）；BMCLAPI 镜像优先、失败回退官方；下载队列；像素茶艺地图站接入。
- **Mod 管理**：元数据解析、依赖检查、更新检查、卸载。
- **智能推荐**：本地规则 + 热门榜单，首页 Top4，玩法分区过滤，依赖补全标记。
- **账号系统**：离线 / Microsoft / Authlib-Injector，多账号存储与切换。
- **工具箱（15 面板）**：日志、存档、截图、性能监控、网络诊断、备份、NBT 编辑、数据包冲突检测、皮肤编辑器、音乐播放器、AI 助手、挂机工作流、开发工具等。
- **外观与皮肤**：暗/亮主题、主题色、字体缩放、索引贴四色独立设置；皮肤预览与编辑。
- **HUD 叠加**：独立窗口实时显示 FPS / 内存 / CPU / GPU / 延迟，跟随游戏窗口。
- **AI 助手**：外部 API 或本地 Ollama 部署，崩溃解读 / 推荐理由 / Mod 翻译 / 语音助手。
- **挂机工作流**：离线 Token 配置帧率 / 渲染距离 / 音量 / 视角 / 模拟按键 / 鼠标连点 / 循环。
- **多语言**：中 / 英双语（zh_CN / en_US），运行时即时切换，无需重启。
- **其他**：年度报告、CLI 命令行、文件变更检测、资源包格式修复、最小化到托盘。

## 更新日志

- **v2.5.4** （当前）：更新源迁移至 CNB Pages（`cnb.cool` 官方静态页）托管的 `latest.json`，国内直连、稳定、免代理；更新下载改由启动器内置下载器直接拉取 CNB 发布直链（不再依赖 winget / 浏览器）；自更新改为「下载 → 解压 → 原地替换安装目录并接力启动新版本」；发布物改为单个 `MCLCS-v2.5.4-win-x64.zip`（不再切分卷）；GitHub 仓库仅作为 CNB 的代码镜像。
- **v2.5.3**：新增启动器自身崩溃捕获与日志（`mclcs_crash.log`，覆盖启动期 XAML 解析等静默退出）；崩溃自动修复新增资源包/光影类别（回滚 vanilla / 停用光影 / 清缓存，非破坏性）；新增存档损坏检测（只读，三色分级：绿=正常 / 橙=可疑 / 红=已损坏）；Mod 冲突触发的禁用在「始终」策略下也改为先弹窗确认。
- **v2.5.2**：修复开发工具，离线自检SelfCheck程序，52项断言全部PASS，修复了多项编译时错误，现启动器功能已基本完整。
- **v2.5.1**：接入多分辨率应用图标与托盘图标（MCLCS.ico）；修正下载队列按钮置灰的反馈问题（按钮恢复常显）；折叠分组 / 下拉框样式明确归入换肤任务（非缺陷）。
- **v2.5.0**：升级 Mojang 版本清单至 Piston v2；修复安装器版本选择缺陷（Fabric / Quilt / NeoForge）；修复外置登录 UI 线程卡死与全局样式；新增最小化到托盘；HUD 叠加层覆盖全部启动路径并修复内存与前台显示。
- **v2.4.2**：新增中英双语（zh_CN / en_US）运行时即时切换（核心页面）；CLI 从 .NET 6 升级至 .NET 8，与 GUI 同框架；发布包同时包含 GUI 启动器（`MCLCS.App.exe`）与 CLI（`mclcs.exe`），提供 portable（依赖运行时）与 single-file（自包含）两种 ZIP。
- **v2.4.1**：UI 图标迁移为外部 PNG（亮/暗双主题）；新增“适配高分辨率屏幕”开关（启用 2x 图标）；移除 CurseForge 预留；修复 WPF 隐式 using 同名冲突与皮肤编辑器闭合标签笔误。
- **v2.4**：重写收官——四色索引贴主标签、工具箱全局侧边栏、21 个面板、AI 助手、皮肤编辑器（3D 预览）、HUD 叠加、年度报告、挂机工作流。
- **v2.2.3**：编译修复与 UV 校准；确立 Linux 下 Roslyn 跨平台编译 WPF 的方法。
- **v2.0 – v2.1**：WPF 重写期，引入下载中心、崩溃智能修复、存档降级、多语言与暗亮主题。
- **v0.1 – v1.1**：WPF 起步，下载中心、Modrinth 接入、崩溃分析与智能修复诞生。

> 更完整的历史快照见 `history` 分支。

## 下载与安装

发布包为**自包含 single-file 版**：GUI 启动器（`MCLCS.App.exe`）与 CLI（`mclcs.exe`）合并于一个 ZIP，内嵌完整 .NET 8 运行时，**无需任何前置依赖**。

| 版本 | 资产 | 说明 |
| --- | --- | --- |
| **v2.5.4（最新）** | [`MCLCS-v2.5.4-win-x64.zip`](https://cnb.cool/RLRS-Studio/MCLCS-WPF/-/releases/download/v2.5.4/MCLCS-v2.5.4-win-x64.zip) | 自包含免运行时，解压即用 |
| 全部历史版本 | [CNB Releases](https://cnb.cool/RLRS-Studio/MCLCS-WPF/-/releases) | 各版本发布直链 |

下载后直接解压，运行 `MCLCS.App.exe` 即可。启动器内置**自动更新器**：启动时读取 CNB Pages 上的 `latest.json`，发现新版本后直接下载 CNB 发布直链、解压并原地替换安装目录、接力启动新版本，全程无需手动下载或 winget。

> v2.5.4 起发布物为单个 ZIP，不再切分卷（早期 v2.5.3 及以前的 `.00`/`.01` 分卷方式已废弃）。

## 编译与发布

- **环境**：Windows + .NET 8 SDK（WPF 仅 Windows 运行）。
- **一键发布（推荐）**：仓库内 `tools/publish-v254.sh` 会以 single-file + self-contained 方式分别发布 GUI 与 CLI，合并打包为单个 `MCLCS-v2.5.4-win-x64.zip`（即 CNB Release 资产）。
- **手动发布 GUI（自包含 single-file）**：
  ```powershell
  dotnet publish src/MCLCS.App/MCLCS.App.csproj -c Release -r win-x64 `
    -p:PublishSingleFile=true -p:SelfContained=true -p:EnableWindowsTargeting=true
  ```
- **手动发布 CLI**：
  ```powershell
  dotnet publish tools/MCLCS.Cli/MCLCS.Cli.csproj -c Release -r win-x64 `
    -p:PublishSingleFile=true -p:SelfContained=true -p:EnableWindowsTargeting=true
  ```

- **Linux 交叉编译校验**：可用 Roslyn 直接引用 .NET 8 参考程序集完成 App / CLI 层编译校验（详见 `docs/BUILD.md`）。
- **CLI 命令**：`launch` / `list` / `install` / `modpack` / `mods` / `skin` / `version`。

## 仓库结构

| 路径                      | 说明                    |
| ----------------------- | --------------------- |
| `src/`                  | 当代源码（Core / App）      |
| `tools/`                | CLI（`MCLCS.Cli`）与辅助工具 |
| `tests/`                | 单元测试                  |
| `history` 分支            | 历史演进快照（原型与旧版本源码）      |
| `docs/`                 | 开发文档与构建说明             |
| `dist/`（仅 `history` 分支） | 各版本发布包（ZIP），不随源码主线入库  |

> 发布包（dist/）归档于 `history` 分支；源码主线（main）只包含当代源码与文档，不含构建产物。

## 法律声明

本软件以 **MIT 许可证** 开源发布，详见 `LICENSE`。

- 本软件的分发物**不包含 Minecraft 核心游戏文件**（如版本 jar、assets、libraries）；这些资源由用户在运行游戏时从 **Mojang 官方源及其授权镜像**（如 BMCLAPI）按需下载，用户须持有合法的正版账号。
- 外置登录（Authlib-Injector）仅用于 **littleskin 等皮肤站**及**用户自有 / 授权的私服**，不用于绕过正版验证。
- 本项目与 Mojang / Microsoft 无关，非官方产品。
