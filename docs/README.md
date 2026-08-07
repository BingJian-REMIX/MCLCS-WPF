# MCLCS — Minecraft 启动器 (WPF)

> **当前版本：v2.4 final** — Core 0 错误 · App 0 错误（Roslyn 跨平台编译）· 单元測试 57/57 通过
> 281 个源文件 · 约 39K 行代码 · 21 个工具箱面板 · 4 个主标签（四色索引贴）

MCLCS（Minecraft Launcher CSharp）是一个使用 **C# / WPF / .NET 8** 实现的 Minecraft 启动器，
覆盖原版 / Fabric / Forge / Quilt / NeoForge 安装、智能 Java 选择、参数解析与变量替换、原生库处理、
崩溃分析与**非破坏性智能修复**、Modrinth / 像素茶艺地图站API接入、**Microsoft / Authlib-Injector
登录、多账号管理、CLI 命令行、Mod 管理、皮肤编辑器（含 3D 预览）、HUD 叠加、年度报告、挂机工作流**等。

---

## 主界面（四色索引贴）

顶部标题栏右侧四个主标签，从右向左为 **游戏（绿）/ 下载（蓝）/ 工具箱（橙）/ 设置（灰）**，
圆角长条相邻重叠、选中向左展开并带同色细线，切换时细线平滑滑动 200ms。
点击后无需钉住，悬停展开侧边栏，移出延迟自动收起。

| 主标签 | 核心内容 |
|---|---|
| **游戏** | 快速启动区（版本/用户名/内存/启动）· 局域网游戏卡片 · 服务器列表（延迟灯）· 智能推荐（Top4 + 玩法分区）· 统计区（最近版本/本周时长/崩溃次数/年度报告入口） |
| **下载** | 副标签 Mod / 光影 / 材质包 / 整合包 / 地图 · 全局搜索栏（关键词/版本/加载器过滤，切换副标签保留输入）· 卡片网格 · 底部下载队列（进度/暂停/取消）· 地图集成像素茶艺 API |
| **工具箱** | 左侧副标签 21 个功能面板（见下） |
| **设置** | 副标签 通用 / 启动 / 下载 / 推荐 / 账号 / AI / 外观 / 关于 |

---

## 功能一览

### 游戏启动与版本安装
- **智能 Java 选择**（≥21，自动探测 Temurin/Oracle）；arguments 条件规则与变量替换；classpath 构建含 natives jar；`-Djava.library.path` / `-Dorg.lwjgl.librarypath` 配置；内存/用户名/JVM 参数自定义。
- **版本安装**：原版（JSON+核心JAR+libraries+natives+资源索引）· Fabric（合并+自动装 Fabric API）· Forge（BMCLAPI 优先，失败回退官方，运行 installer）· Quilt / NeoForge · Modrinth `.mrpack` 整合包（CurseForge `.zip` 尚未接入）。
- **启动前存档兼容性检测**：扫描 `saves/*/level.dat` 的 DataVersion，过高则弹窗三选项：① 安装对应版本 ② 降级（A 改写 DataVersion / B Amulet 转换，强制备份、原档保留）③ 忽略；降级联动崩溃时提示回滚/换方案/装原版。
- **启动预热**（设置开关）：后台预读最近 7 天内游玩前 2 的版本的 Java 与核心库到系统缓存，不阻塞、不实际运行 Java。

### 崩溃智能处理
- 识别 8 类异常（OutOfMemoryError / UnsupportedClassVersionError / ClassNotFoundException / OpenGL 等），输出报告与修复建议。
- 可自动修复（内存/Java 不兼容/缺失库），用户确认后修复并重启，循环至成功或不可修复。
- 策略：始终 / 询问 / 拒绝；全程不删不改游戏原文件。

### 下载中心
- Modrinth 搜索，按版本 / 加载器（Fabric/Forge/Quilt/NeoForge）/ 类型过滤。
- 镜像策略：BMCLAPI 优先，失败回退官方；下载队列管理，进度回传状态栏。
- 地图（像素茶艺 API `https://goto.pixelmap.cc/api/open/v1/maps`）：分类/版本/排序下拉，详情窗，解压至 `saves`，自定义 User-Agent。

### Mod 管理
- 扫描解析元数据（fabric.mod.json / mods.toml）；依赖检查、更新检查、卸载；依赖补全红色标记。

### 智能推荐与依赖补全
- 本地规则引擎（必装/场景/更新推荐）+ 热门榜单（Modrinth 周榜/总榜，每小时缓存）；首页 Top4 卡片；玩法分区过滤；一键安装、不感兴趣。

### 工具箱（21 面板）
日志管理 · 存档管理（内嵌成就）· 截图管理 · 性能/实例监控 · 网络诊断 · 快捷方式 · 冗余清理 ·
整合包导入导出 · 备份管理器（定时/数量限制/手动恢复前自动备份）· NBT 编辑器 · 数据包冲突检测 ·
服务器资源包缓存 · 文件变更检测（启动/焦点回归时检测手动丢入文件，右下角非阻塞通知 5s）·
年度报告（12.31 展示，统计启动次数/时长/最爱/挂机比例/成就，AI 解读，导出 Token 分享）·
AI 助手 · 音乐播放器（本地/在线/MC原声，游戏启动自动暂停）· 挂机工作流配置 · 开发工具（Mod骨架/NBT/资源包创建/命令表）·
皮肤编辑器（36 面 UV 映射 + 3D 预览，对称绘制，导出 PNG）· 光影配置 Token（短码编码/复制/导入）· 成就展示。

### 皮肤与外观
- **皮肤预览**：Mojang Sessionserver API 查询，区分 slim/classic；离线皮肤编辑与导出。
- **多语言**：简体中文 / English（zh_CN / en_US），内置无需额外文件。
- **主题**：暗/亮主题 + 主题色 / 字体缩放 / 背景图 / 索引贴四色独立设置，偏好持久化。

### HUD 叠加
- 设置开关（默认关），非侵入独立窗口、点击穿透、跟随游戏窗口；全屏不隐藏、位置可拖拽、字体可调；显示 FPS/内存/CPU/GPU/延迟，数据失败静默处理；仅在游戏运行时激活。

### AI 助手
- 总开关默认关（零占用）；部署方式：外部 API（填 Key 即用）或本地 Ollama（一键安装 + 模型拉取 + 服务状态灯）。
- 本地模型：Qwen2.5-Coder-1.5B / InternLM2-1.8B / Phi-3.5-mini-3.8B。
- 功能子开关：崩溃解读 / 推荐理由 / Mod 翻译；语音助手（系统原生识别，不可用时置灰）；崩溃解读/配装推荐为**显式触发**。

### 挂机工作流与 Token
- 工作流 Token（离线解析）：`v1F10;D4;L3;K39;C1-500;*0`，动作类型含帧率限制/渲染距离/音量/视角/模拟按键/鼠标连点/循环标记。
- 完全离线解析，复制/导入后自动填充配置界面。

### 账号系统
- 离线（离线 UUID）/ Microsoft（OAuth2 设备流）/ Authlib-Injector（serverUrl/email/password）；多账号存储与切换。

### CLI 命令行
- `launch / list / install / modpack / mods / skin / version`

---

## 解决方案结构

```
MCLCS/
├── MCLCS.sln
├── src/
│   ├── MCLCS.Core/          # 平台无关核心逻辑（net6.0，无 WPF 引用，Linux 可编译）
│   │   ├── Ai/              # Assistant, OllamaManager
│   │   ├── Auth/            # Offline / Microsoft / AuthlibInjector
│   │   ├── Download/        # HttpDownloader, MirrorPolicy, ModrinthClient,
│   │   │                   #   PixelmapClient, MapInstaller,
│   │   │                   #   ModrinthModpackSource, ModpackSource, ExtraResourceInstaller
│   │   ├── Installers/      # Vanilla / Fabric / Forge / Quilt / NeoForge /
│   │   │                   #   ModpackInstaller / LibraryRepair
│   │   ├── Launcher/        # JavaDetector, JavaInstaller, ArgumentProcessor,
│   │   │                   #   ClasspathBuilder, VersionMerger, GameLauncher,
│   │   │                   #   CrashAnalyzer, CrashDetector, CrashRepairEngine,
│   │   │                   #   CrashRepairPolicy, LaunchPrewarmer, RuleEvaluator
│   │   ├── Save/            # Nbt, DataVersionMap, SaveCompatibilityDetector,
│   │   │                   #   SaveDowngrader, DowngradeCrashLinkage, SaveModels
│   │   ├── Servers/         # LanServerScanner, ServerListStore, ServerPinger
│   │   ├── Skin/            # SkinFetcher
│   │   ├── Statistics/      # AnnualReport, PlaytimeTracker
│   │   ├── Hud/             # HudConfig, HudMetricsProvider
│   │   ├── Resources/       # ResourcePackRepairer, ServerResourcePackCache
│   │   ├── Tokens/          # AfkWorkflowToken, ShaderConfigToken
│   │   ├── Toolbox/         # BackupManager, DataPackConflictDetector, FileChangeDetector,
│   │   │                   #   LogManager, ModpackExporter, MusicPlayer, NbtEditor,
│   │   │                   #   NetworkDiagnostics, RedundantFileCleaner, ScreenshotManager,
│   │   │                   #   SeedLibrary, ShortcutGenerator, SkinEditor, ToolboxCatalog
│   │   ├── Recommend/       # RecommendationEngine, RuleEngine, HotRanking, GameplayCategory
│   │   ├── Mods/            # ModManager, ModEntry, ModMetadataParser
│   │   ├── Profiles/        # LauncherProfile, ProfileStore, AccountStore, VersionIsolation
│   │   ├── Localization/    # LocaleManager (zh_CN / en_US)
│   │   ├── Theme/           # ThemeManager (Light / Dark)
│   │   ├── Mvvm/            # ObservableObject, RelayCommand（零依赖）
│   │   └── Utils/           # GameConstants, PathEx, Unzip, HashUtil, Elevation, IconCache
│   └── MCLCS.App/           # WPF 界面（net8.0-windows，仅 Windows 运行）
│       ├── Views/           # 游戏/下载/工具箱/设置 + 21 工具箱面板 + HUD/年度报告等窗口
│       ├── ViewModels/      # MVVM 视图模型
│       ├── Controls/        # ModalDialog, SkinPreview3D, SkinModel3D
│       ├── Converters/      # BoolToColor / LogSeverityToColor / StringToBrush 等
│       ├── Themes/          # DarkTheme.xaml / LightTheme.xaml / Palette.xaml / Icons.cs
│       └── Services/        # LauncherService, LaunchCoordinator, ToastService, UIService
├── tests/MCLCS.Core.Tests/  # xUnit 单元测试（57/57 通过）
├── tools/
│   ├── MCLCS.Cli/           # 命令行启动器
│   ├── MCLCS.SelfCheck/     # 离线自检
│   └── build-app-linux.sh   # Linux 下 Roslyn 跨平台编译脚本
├── dist/                    # 各里程碑备份（T0–T4, v2.2-final）
└── docs/                    # README / BUILD / 备份说明
```

---

## 快速使用

### GUI
1. 在「设置」配置 Java 路径、内存、用户名、主题、语言并保存。
2. 在「下载」页选择类型与版本号安装（或导入整合包）。
3. 在「游戏」页选版本一键启动；崩溃自动弹分析报告，可一键修复。
4. 在「下载 → 下载中心」搜索 Mod/光影/材质包/地图，加入队列批量下载。
5. 在「工具箱」使用 21 个面板（日志、存档、皮肤编辑器、音乐、AI、挂机工作流、年度报告等）。
6. 在「设置 → 账号」增删切换离线 / Microsoft / Authlib-Injector 账号。
7. 在「设置 → AI」开启总开关，选外部 API 或本地 Ollama 部署。

### CLI
```powershell
mclcs launch 1.20.1 --username Steve --memory 4096
mclcs list
mclcs install fabric 1.20.1
mclcs modpack modrinth mypack.mrpack
mclcs mods list
mclcs mods check
mclcs mods updates
mclcs skin Notch
mclcs version
```

---

## 构建

### Windows（生成 EXE）
需 Windows + .NET 8 SDK：
```powershell
dotnet publish src/MCLCS.App/MCLCS.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Linux（跨平台编译校验）
使用 Roslyn 直接引用 .NET 8 参考程序集（华为云镜像）完成 App 层交叉编译，配合 XAML 桩生成，
可在 Linux CI 上验证 App 0 错误。详见 `tools/build-app-linux.sh` 与 [BUILD.md](./BUILD.md)。

---

## 镜像策略

所有下载优先走 **BMCLAPI**（`bmclapi2.bangbang93.com`），失败自动回退官方源。
Java 自动安装使用 Adoptium API，Modrinth 使用官方 API，
地图使用像素茶艺 API（`https://goto.pixelmap.cc`）。

---

## 温馨提示
- 本项目提交于26/8/7，此时还未完成Windows环境测试，不保证项目可用。
- 建议对该项目持续追踪，万一哪一天完成测试并修复了最终bug（icon相关）呢？

## 已知限制与后续路线

见 [BUILD.md](./BUILD.md)。
