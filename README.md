# MCLCS — Minecraft 启动器 (WPF)

> **当前版本：v2.4.2** · 语言：C# / WPF / .NET 8 · 平台：Windows

MCLCS（Minecraft Launcher CSharp）是一个用 C# / WPF 实现的 Minecraft 启动器，覆盖版本安装、启动、崩溃修复、下载、Mod 管理与工具箱等。

## 功能一览

- **启动与安装**：原版 / Fabric / Forge / Quilt / NeoForge 安装；智能 Java 选择（≥21）；启动前存档兼容性检测与降级；启动预热。
- **崩溃处理**：8 类异常识别与报告，可非破坏性自动修复（内存 / Java / 缺失库），支持始终 / 询问 / 拒绝策略。
- **下载中心**：Modrinth 搜索（版本 / 加载器过滤）；BMCLAPI 镜像优先、失败回退官方；下载队列；像素茶艺地图站接入。
- **Mod 管理**：元数据解析、依赖检查、更新检查、卸载。
- **智能推荐**：本地规则 + 热门榜单，首页 Top4，玩法分区过滤，依赖补全标记。
- **账号系统**：离线 / Microsoft / Authlib-Injector，多账号存储与切换。
- **工具箱（21 面板）**：日志、存档、截图、性能监控、网络诊断、备份、NBT 编辑、数据包冲突检测、皮肤编辑器、音乐播放器、AI 助手、挂机工作流、开发工具等。
- **外观与皮肤**：暗/亮主题、主题色、字体缩放、索引贴四色独立设置；皮肤预览与编辑。
- **HUD 叠加**：独立窗口实时显示 FPS / 内存 / CPU / GPU / 延迟，跟随游戏窗口。
- **AI 助手**：外部 API 或本地 Ollama 部署，崩溃解读 / 推荐理由 / Mod 翻译 / 语音助手。
- **挂机工作流**：离线 Token 配置帧率 / 渲染距离 / 音量 / 视角 / 模拟按键 / 鼠标连点 / 循环。
- **多语言**：中 / 英双语（zh_CN / en_US），运行时即时切换，无需重启。
- **其他**：年度报告、CLI 命令行、文件变更检测、资源包格式修复。

## 更新日志

- **v2.4.2**（当前）：新增中英双语（zh_CN / en_US）运行时即时切换（核心页面）；CLI 从 .NET 6 升级至 .NET 8，与 GUI 同框架；发布包同时包含 GUI 启动器（`MCLCS.App.exe`）与 CLI（`mclcs.exe`），提供 portable（依赖运行时）与 single-file（自包含）两种 ZIP。
- **v2.4.1**：UI 图标迁移为外部 PNG（亮/暗双主题）；新增“适配高分辨率屏幕”开关（启用 2x 图标）；移除 CurseForge 预留；修复 WPF 隐式 using 同名冲突与皮肤编辑器闭合标签笔误。
- **v2.4**：重写收官——四色索引贴主标签、工具箱全局侧边栏、21 个面板、AI 助手、皮肤编辑器（3D 预览）、HUD 叠加、年度报告、挂机工作流。
- **v2.2.3**：编译修复与 UV 校准；确立 Linux 下 Roslyn 跨平台编译 WPF 的方法。
- **v2.0 – v2.1**：WPF 重写期，引入下载中心、崩溃智能修复、存档降级、多语言与暗亮主题。
- **v0.1 – v1.1**：WPF 起步，下载中心、Modrinth 接入、崩溃分析与智能修复诞生。

> 更完整的历史快照见 `history/`。

## 下载与安装

发布包（v2.4.2）同时包含 GUI 启动器（`MCLCS.App.exe`）与 CLI（`mclcs.exe`），提供两种形态：

| 包 | 说明 | 依赖 |
|---|---|---|
| `MCLCS-v2.4.2-portable.zip` | GUI + CLI，依赖 .NET 8 运行时 | 需先安装 [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) |
| `MCLCS-v2.4.2-singlefile.zip.00` / `.01` | 自包含免运行时版（各含完整 .NET 运行时） | 无需任何依赖 |

> **single-file 分卷说明**：自包含包约 128MB，超过多数平台单文件附件上限（100MB），故按 90MB 切分为 `.00` / `.01` 两个分卷。该切分为**字节级精确**，合并后即还原为原始 ZIP，任意解压工具均可使用。

**合并分卷**：

- Windows（CMD / PowerShell）：
  ```powershell
  copy /b MCLCS-v2.4.2-singlefile.zip.00 + MCLCS-v2.4.2-singlefile.zip.01 MCLCS-v2.4.2-singlefile.zip
  ```
- Linux / macOS：
  ```bash
  cat MCLCS-v2.4.2-singlefile.zip.* > MCLCS-v2.4.2-singlefile.zip
  ```

合并得到 `MCLCS-v2.4.2-singlefile.zip`，解压后即可使用。发布包归档于 `history` 分支的 `dist/`，作为对应 Release 的附件。

## 编译与发布

- **环境**：Windows + .NET 8 SDK（WPF 仅 Windows 运行）。
- **发布 GUI（自包含 EXE）**：
  ```powershell
  dotnet publish src/MCLCS.App/MCLCS.App.csproj -c Release -r win-x64 --self-contained
  ```
- **发布 CLI**：
  ```powershell
  dotnet publish tools/MCLCS.Cli/MCLCS.Cli.csproj -c Release -r win-x64 --self-contained
  ```
- **Linux 交叉编译校验**：可用 Roslyn 直接引用 .NET 8 参考程序集完成 App / CLI 层编译校验（详见 `docs/BUILD.md`）。
- **CLI 命令**：`launch` / `list` / `install` / `modpack` / `mods` / `skin` / `version`。

## 仓库结构

| 路径 | 说明 |
|---|---|
| `src/` | 当代源码（Core / App） |
| `tools/` | CLI（`MCLCS.Cli`）与辅助工具 |
| `tests/` | 单元测试 |
| `history/` | 历史演进快照（原型与旧版本源码） |
| `docs/` | 开发文档与构建说明 |
| `dist/`（仅 `history` 分支） | 各版本发布包（ZIP），不随源码主线入库 |

> 发布包（dist/）归档于 `history` 分支；源码主线（main）只包含当代源码与文档，不含构建产物。

## 法律声明

本软件以 **MIT 许可证** 开源发布，详见 `LICENSE`。

- 本软件的分发物**不包含 Minecraft 核心游戏文件**（如版本 jar、assets、libraries）；这些资源由用户在运行游戏时从 **Mojang 官方源及其授权镜像**（如 BMCLAPI）按需下载，用户须持有合法的正版账号。
- 外置登录（Authlib-Injector）仅用于 **littleskin 等皮肤站**及**用户自有 / 授权的私服**，不用于绕过正版验证。
- 本项目与 Mojang / Microsoft 无关，非官方产品。
