# MCLCS — Minecraft 启动器 (WPF)

> **当前版本：v2.4.1** · 语言：C# / WPF / .NET 8 · 平台：Windows

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
- **其他**：年度报告、CLI 命令行、中/英多语言、文件变更检测、资源包格式修复。

## 更新日志

- **v2.4.1**（当前）：UI 图标迁移为外部 PNG（亮/暗双主题）；新增“适配高分辨率屏幕”开关（启用 2x 图标）；移除 CurseForge 预留；修复 WPF 隐式 using 同名冲突与皮肤编辑器闭合标签笔误。
- **v2.4**：重写收官——四色索引贴主标签、工具箱全局侧边栏、21 个面板、AI 助手、皮肤编辑器（3D 预览）、HUD 叠加、年度报告、挂机工作流。
- **v2.2.3**：编译修复与 UV 校准；确立 Linux 下 Roslyn 跨平台编译 WPF 的方法。
- **v2.0 – v2.1**：WPF 重写期，引入下载中心、崩溃智能修复、存档降级、多语言与暗亮主题。
- **v0.1 – v1.1**：WPF 起步，下载中心、Modrinth 接入、崩溃分析与智能修复诞生。

> 更完整的历史快照见 `history/`。

## 编译事项

- **环境**：Windows + .NET 8 SDK（WPF 仅 Windows 运行）。
- **发布（自包含 EXE）**：
  ```powershell
  dotnet publish src/MCLCS.App/MCLCS.App.csproj -c Release -r win-x64 --self-contained
  ```
- **Linux 交叉编译校验**：可用 Roslyn 直接引用 .NET 8 参考程序集完成 App 层编译校验（详见 `tools/build-app-linux.sh` 与 `docs/BUILD.md`）。
- **UI 图标**：PNG 资源编译进程序集（Resource），不暴露到输出目录。

## 说明

- 本项目当前尚未完成 Windows 环境实测，使用风险自负；欢迎持续追踪。
- 镜像策略：下载优先 BMCLAPI，失败回退官方；Java 安装用 Adoptium，地图用像素茶艺 API。
