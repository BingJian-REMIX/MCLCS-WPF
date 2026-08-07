# MCLCS — Minecraft 启动器

一个使用 **C# / WPF / .NET 8** 实现的 Minecraft 启动器，覆盖原版 / Fabric / Forge 安装、
智能 Java 选择、参数解析与变量替换、原生库处理、崩溃分析、**崩溃智能修复**、Modrinth 下载中心、
**Microsoft / Authlib-Injector 登录、多账号管理、CLI 命令行、Mod 管理（含依赖检查）、
整合包安装（Modrinth .mrpack + CurseForge .zip）、皮肤预览、多语言（zh_CN / en_US）、
亮色/暗色主题切换、工具箱、下载队列、AI 助手**。

> v1.1 新增（当前版本）：
> - **四页式界面**：首页（快速启动 + 游玩统计 + 智能推荐）、下载（版本列表/安装/下载中心/Mod 管理 Tab）、工具箱、设置；底部状态栏实时显示 Java、已装数量、运行实例、下载进度与网络状态。
> - **工具箱 10 面板**：日志（按级别着色/筛选/导出）、存档、种子（提取/创建世界/搜索）、截图（打包分享）、性能（实例/时长/CPU）、网络诊断（LED 状态）、快捷方式、冗余文件清理、整合包导入导出、AI 助手。
> - **设置 7 大类**：常规、启动、下载、智能推荐、账号、AI、外观、关于（共 8 个分区）。
> - **下载队列**：可加入队列、批量开始、单项暂停/取消，进度实时回传状态栏。
> - **多账号**：离线 / Microsoft（设备流 OAuth2）/ Authlib-Injector；设置内增删切换。
> - **AI 助手**：分层设置——总开关默认关闭（关闭时零资源占用）；开启后可选「外部 API（填 Key 即用）」或「本地部署 Ollama」；本地部署支持一键安装 Ollama、模型拉取（Qwen2.5-Coder-1.5B / InternLM2-1.8B / Phi-3.5-mini-3.8B）、服务状态指示灯；外部 API 支持地址/Key/模型与测试连接（按地址自动补全模型名）；崩溃解读、推荐理由、Mod 翻译三项能力可独立开关。
> - **统一启动协调器**：首页与版本列表共用 `LaunchCoordinator`，统一「存档兼容提示 → 缺失依赖自动安装 → 启动 → 崩溃自动修复循环」。
>
> v0.4 新增：**崩溃智能处理模块** —— 游戏崩溃后自动分析崩溃报告，识别可修复问题
> （内存不足 / Java 不兼容 / 缺失依赖库），提供"尝试自动修复"按钮，确认后自动修复并重新启动，
> 循环直至成功或无法继续修复；在设置中提供"始终开启 / 每次询问 / 始终拒绝"策略。
> 所有修复操作均不删除、不修改游戏原文件。
>
> v0.5 新增：**存档兼容性与降级** —— ① §二.4 启动前扫描 `saves/*/level.dat` 的 DataVersion，
> 与目标游戏版本比较，存档版本过高时弹窗给出「降级 / 安装对应版本 / 忽略」三选项；
> ② §三 存档降级：方案 A（改写 DataVersion 快速降级）、方案 B（调用 Amulet 真正转换），
> 操作前强制备份、输出变更摘要、原档始终保留；③ §四.2 降级联动：若崩溃疑似由降级引起，
> 在崩溃报告中提供「回滚备份 / 改用其他方式 / 安装存档原版本」三选项。另含 Oracle JDK 自动安装、
> Mod 冲突处理与缺失前置自动安装、智能依赖与推荐系统。
>
> 版本命名已**通用化**：同时兼容旧方案 `1.X.Y`（如 1.20.1 / 1.21.11）与新方案 `YY.M[.P]`
> （即年份后二位.月份[.补丁]，如 26.1 / 26.1.2 / 26.2，自 1.21.x 之后、跳过不存在的 1.22.0 起启用），
> DataVersion 对照表已同步扩充至 26.x。
>
> v0.3 新增：CurseForge 整合包（.zip）安装、Mod 依赖检查（fabric.mod.json / mods.toml 解析）、
> 皮肤预览（Mojang API）、多语言支持（简体中文/English）、亮色/暗色主题切换、
> CLI 扩展（modpack/mods/skin 命令）、logging 日志参数注入。

---

## 功能一览

| 模块 | 说明 |
|---|---|
| 主界面（四页） | 首页 / 下载 / 工具箱 / 设置 四个页面切换；底部状态栏显示 Java、已装版本数、运行实例、下载进度、网络状态；页面切换带淡入+位移动画（可在设置关闭） |
| 首页 | 快速启动下拉 + 一键启动、游玩统计卡片（时长/次数）、Top 4 智能推荐卡片（区分依赖补全/同类替换/玩法推荐） |
| 游戏启动 | 智能 Java 选择（≥21）、arguments 条件规则与变量替换、classpath 构建、natives 解压、内存/用户名/JVM 参数自定义、退出后崩溃检测、log4j 配置注入 |
| 崩溃分析 | 识别 OutOfMemoryError / UnsupportedClassVersionError / ClassNotFoundException / OpenGL 等 8 种异常，展示完整报告与手动建议 |
| 崩溃智能修复 | 自动识别可修复问题（内存不足 / Java 不兼容 / 缺失库）；"尝试自动修复"按钮；确认后修复并重启、循环直至成功或不可修复；设置策略：始终开启 / 每次询问 / 始终拒绝；全程不删不改游戏原文件 |
| 启动协调器 | 首页与版本列表共用 `LaunchCoordinator`：存档兼容提示 → 缺失依赖自动安装 → 启动 → 崩溃自动修复循环 |
| 存档管理 | §二.4 启动前兼容性检测（DataVersion 比对）+ §三 存档降级（方案 A 改写 DataVersion / 方案 B Amulet，强制备份、原档保留）+ §四.2 降级联动（回滚备份 / 改用其他方式 / 安装原版本） |
| 版本安装 | 原版 / Fabric / Forge / Modrinth 整合包（.mrpack）/ CurseForge 整合包（.zip） |
| 下载中心 | Modrinth 搜索，按版本/加载器（Fabric/Forge/Quilt/NeoForge）/类型过滤；支持加入**下载队列**，后台批量下载、单项暂停/取消、进度回传状态栏 |
| 账号系统 | 离线 / Microsoft（设备流 OAuth2）/ Authlib-Injector（Yggdrasil）；多账号存储与切换；设置内增删 |
| Mod 管理 | 扫描已安装、元数据解析（fabric.mod.json / mods.toml）、依赖检查、更新检查、卸载 |
| 工具箱 | 10 面板：日志、存档、种子、截图、性能、网络诊断、快捷方式、冗余文件清理、整合包导入导出、AI 助手 |
| AI 助手 | 分层设置：总开关默认关（零占用）；部署方式二选一——外部 API（填 Key 即用，测试连接按地址自动补全模型名）或本地部署 Ollama（一键安装 + 模型拉取 + 服务状态灯）；本地模型 Qwen2.5-Coder-1.5B / InternLM2-1.8B / Phi-3.5-mini-3.8B；崩溃解读、推荐理由、Mod 翻译三项能力独立开关 |
| 皮肤预览 | 通过 Mojang API 查询正版玩家皮肤（支持 slim/classic 模型） |
| 多语言 | 简体中文 / English，内置翻译无需额外文件 |
| 主题 | 亮色/暗色主题切换 + 主题色/字体缩放/背景图，偏好持久化 |
| CLI 命令行 | launch / list / install / modpack / mods / skin / version |
| 辅助功能 | Java 自动安装（Temurin 21 / Oracle）、管理员权限提示、启动器自更新检查 |

**零第三方运行时依赖**：核心逻辑仅使用 .NET 内置 API，MVVM 基类自研。

---

## 解决方案结构

```
MCLCS/
├── MCLCS.sln
├── src/
│   ├── MCLCS.Core/          # 平台无关核心逻辑（net6.0，无 WPF 引用，Linux 可编译）
│   │   ├── Models/          # VersionJson / Library / Rule / FabricMeta / ForgeMeta /
│   │   │                   #   ModrinthModels / CurseForgeModels / ModMetaModels
│   │   ├── Launcher/        # JavaDetector, JavaInstaller, ArgumentProcessor,
│   │   │                   #   ClasspathBuilder, VersionMerger, GameLauncher,
│   │   │                   #   CrashDetector, CrashAnalyzer, CrashRepairEngine,
│   │   │                   #   CrashRepairPolicy, CrashRepairModels, RuleEvaluator
│   │   ├── Save/            # Nbt（读写库）, DataVersionMap, SaveCompatibilityDetector,
│   │   │                   #   SaveDowngrader, DowngradeCrashLinkage, SaveModels
│   │   ├── Installers/      # Vanilla / Fabric / Forge / ModpackInstaller /
│   │   │                   #   CurseForgeModpackInstaller / LibraryRepair
│   │   ├── Download/        # HttpDownloader, MirrorPolicy, ModrinthClient, CurseForgeClient
│   │   ├── Auth/            # IAuthenticator, Offline, Microsoft, AuthlibInjector
│   │   ├── Profiles/        # LauncherProfile, ProfileStore, AccountStore
│   │   ├── Mods/            # ModManager, ModEntry, ModMetadataParser
│   │   ├── Skin/            # SkinFetcher (Mojang Sessionserver API)
│   │   ├── Localization/    # LocaleManager (zh_CN / en_US)
│   │   ├── Theme/           # ThemeManager (Light / Dark)
│   │   ├── Mvvm/            # ObservableObject, RelayCommand（零依赖）
│   │   └── Utils/           # GameConstants, PathEx, Unzip, HashUtil, Elevation
│   └── MCLCS.App/           # WPF 界面（net8.0-windows / .NET 8，仅 Windows）
│       ├── Views/           # 四页：Home / DownloadPage / Toolbox / Settings
│       │                   #   下载子页：VersionList / Install / DownloadCenter / Mods
│       │                   #   工具箱面板：Log / Saves / Seed / Screenshot / Perf /
│       │                   #     NetworkDiag / Shortcut / RedundantClean / Modpack / AiAssist
│       │                   #   其它：CrashAnalysis / CrashReport / SaveCompatPrompt /
│       │                   #     Recommendation / Skin
│       ├── ViewModels/      # MVVM 视图模型 + StatusBarViewModel（状态栏单例）
│       ├── Converters/      # BoolToColor / LogSeverityToColor / StringToBrush
│       ├── Themes/          # DarkTheme.xaml / LightTheme.xaml
│       └── Services/        # LauncherService, LaunchCoordinator, UIService
├── tests/MCLCS.Core.Tests/  # xUnit 单元测试
├── tools/
│   ├── MCLCS.Cli/           # 命令行启动器
│   └── MCLCS.SelfCheck/     # 离线自检（265 项全部通过）
└── docs/
    ├── README.md
    └── BUILD.md
```

---

## 快速使用

### GUI
1. 启动器打开后，在「设置」中配置 Java 路径、内存、用户名、主题、语言并保存。
2. 在「下载」页的「安装新版本」中选择类型与版本号安装。
3. 在「首页」选择版本一键启动，或在「下载 → 版本列表」中启动；崩溃自动弹出分析报告。
4. 在「下载 → 下载中心」搜索 Mod/光影/资源包，可「加入队列」批量下载，单项可暂停/取消。
5. 在「工具箱」使用日志、种子、截图、性能、网络诊断、快捷方式、冗余清理、整合包、AI 助手。
6. 在「设置 → 账号」添加并切换离线 / Microsoft / Authlib-Injector 账号。
7. 在「设置 → AI 助手」开启总开关：选「外部 API」填地址/Key（可点测试连接自动补全模型名）；或选「本地部署」一键安装 Ollama 并拉取模型；在「AI 功能」中独立开关崩溃解读 / 推荐理由 / Mod 翻译。

### CLI
```powershell
mclcs launch 1.20.1 --username Steve --memory 4096
mclcs list
mclcs install fabric 1.20.1
mclcs modpack modrinth mypack.mrpack
mclcs modpack curseforge mypack.zip
mclcs mods list
mclcs mods check
mclcs mods updates
mclcs skin Notch
mclcs version
```

---

## 镜像策略

所有下载优先走 **BMCLAPI**（`bmclapi2.bangbang93.com`），失败自动回退官方源。
Java 自动安装使用 Adoptium API，Modrinth 使用官方 API，CurseForge 使用公开 v1 API。

---

## 已知限制与后续路线

见 [BUILD.md](./BUILD.md)。
