# MCLCS v2.2.3 — 编译修复 + UV 校准 + 后续任务

> 2026-08-06，Linux 沙箱交叉编译验证通过（Core: 0 错误, App: 0 错误, 7 个无害警告）

---

## 本轮已完成

### 1. App(WPF) 首次在沙箱内完整编译通过

此前 MCLCS.App (net8.0-windows WPF) 在 Linux 沙箱下一直无法编译。本轮通过：
- 从华为云 NuGet 镜像拉取 `Microsoft.WindowsDesktop.App.Ref` / `Microsoft.NETCore.App.Ref` 参考程序集
- 用 `csc.dll` (Roslyn) 直接编译全部 C# 源码 + 模拟 XAML partial 桩
- 实现 Core 与 App 双程序集 0 错误通过

### 2. 修复 10 处真实编译错误

| # | 文件 | 错误 | 修复方式 |
|---|------|------|----------|
| 1 | `MCLCS.App.csproj` | `UseWindowsForms=true` 引入全局 `System.Drawing`/`System.Windows.Forms`，与 WPF 类型大面积撞名 (CS0104) | 移除 `UseWindowsForms`，注释说明原因 |
| 2 | `Services/UIService.cs` | `FolderBrowserDialog` 依赖 WinForms | 改用 .NET 8 WPF 原生 `OpenFolderDialog` |
| 3 | `Controls/SkinModel3D.cs` | 缺 `using System.Windows.Media.Imaging` (CS0246: BitmapImage) | 补 using |
| 4 | `Controls/SkinPreview3D.cs` | `ModelVisual3D.Content = ModelVisual3D` 类型错误；`Viewport3D.Background` 不存在 | Content→Children；Background 移到 UserControl |
| 5 | `ViewModels/VisibilityHelper.cs` | `BooleanToVisibilityConverter` 无 `TrueValue`/`FalseValue` 属性 | 复用已有 `InverseBoolToVisibilityConverter.Instance` |
| 6 | `ViewModels/DownloadPageViewModel.cs` | 同名 `VersionEntry` 歧义；`DownloadQueueItem` 缺 `Summary`/`Kind`/`Slug`；缺 `System.IO` | 完全限定名 + 补字段 + 补 using |
| 7 | `ViewModels/SettingsViewModel.cs` | `AiMode` 属性遮蔽同名枚举 (CS1061)；缺 `System.Text` | 完全限定名 + 补 using |
| 8 | `ViewModels/HomeViewModel.cs` | 缺 `System.Net.Http` | 补 using |
| 9 | `Themes/Icons.cs` | `TryGetValue` 失败时 `d` 未赋值 | 先 `ContainsKey` 回退后再 `TryGetValue` |
| 10 | `Views/HomeView.xaml` | ComboBox 误用 `DisplayMemberBinding`（应为 `DisplayMemberPath`） | 修正属性名 |

### 3. SkinModel3D UV 表按权威 Mojang 模板彻底校准

**修正前**：左臂/左腿 UV 错误放在 y20 带（与躯干/右臂重叠），帽子放在 y24-40 区域，左裤在 x20。

**修正后**（与 64×64 标准布局完全一致）：

| 部件 | 区域 | Front 坐标 |
|------|------|------------|
| 头 L1 | x0-32 / y0-16 | (8,8) |
| 躯干 L1 | x16-40 / y16-32 | (20,20) |
| 右臂 L1 | x40-56 / y16-32 | (44,20) |
| 左臂 L1 | x32-48 / y48-64 | (36,52) |
| 右腿 L1 | x0-16 / y16-32 | (4,20) |
| 左腿 L1 | x16-32 / y48-64 | (20,52) |
| 帽子 L2 | x32-64 / y0-16 | (40,8) |
| 外套 L2 | x16-40 / y32-48 | (20,36) |
| 右袖 L2 | x40-56 / y32-48 | (44,36) |
| 左袖 L2 | x48-64 / y48-64 | (52,52) |
| 右裤 L2 | x0-16 / y32-48 | (4,36) |
| 左裤 L2 | x0-16 / y48-64 | (4,52) |

**UV 占用验证**：12 部件 × 6 面，0 越界，0 重叠，3264/4096 像素覆盖。

**64×32 旧皮肤**：新增 `LegacyLeftArm`/`LegacyLeftLeg` 镜像复用分支。

### 4. XAML 绑定属性反射校验

通过 MetadataLoadContext 反射 App.dll + Core.dll，对 267 条顶层 Binding 做属性存在性检查，确认全部有效（模板内绑定已排除）。

### 5. 全量 XAML 属性误用扫描

检查了全部 .xaml 文件的 DisplayMemberBinding/Background/Text/Content 误用，仅发现 HomeView 一处已修复。

---

## 本轮修改文件清单

```
src/MCLCS.App/
  MCLCS.App.csproj                          ← 移除 UseWindowsForms
  Services/UIService.cs                     ← FolderBrowserDialog → OpenFolderDialog
  Services/LauncherService.cs               ← 补 System.IO + System.Net.Http
  Controls/SkinModel3D.cs                   ← UV 表全量校准 + 64×32 legacy 分支
  Controls/SkinPreview3D.cs                 ← Content→Children, Background 修正
  ViewModels/VisibilityHelper.cs            ← 复用 InverseBoolToVisibilityConverter
  ViewModels/DownloadPageViewModel.cs       ← 类型歧义 + 缺字段 + 缺 using
  ViewModels/DownloadCenterViewModel.cs     ← DownloadQueueItem 补 Summary/Kind/Slug
  ViewModels/SettingsViewModel.cs           ← AiMode 歧义 + 补 System.Text
  ViewModels/HomeViewModel.cs               ← 补 System.Net.Http
  Themes/Icons.cs                           ← 未赋值变量修复
  Views/HomeView.xaml                       ← DisplayMemberBinding → DisplayMemberPath
  Views/RecommendationView.xaml.cs          ← 补 System.Windows.Controls
```

---

## 后续待办（需 Windows 环境）

### P0 — 必须 Windows 实测
- [ ] App(WPF) 在 Visual Studio / `dotnet build` 下完整编译并启动
- [ ] 皮肤 3D 预览目测验证（slim 双臂 3px / classic 4px / 64×32 legacy / 第二层叠加厚度）
- [ ] NeoForge / Quilt 安装器端到端测试
- [ ] 下载队列统一入队执行（Task #17 收尾）

### P1 — 功能补全
- [ ] 下载页地图"附加资源"按钮（像素茶艺 API）
- [ ] 整合包在线浏览（Modrinth/CurseForge）
- [ ] 智能推荐卡片"不感兴趣"交互
- [ ] 皮肤编辑器 36 面独立画布
- [ ] 音乐播放器三音源集成

### P2 — 增强
- [ ] 128×128 HD 皮肤支持（不同 UV 布局，四肢 8px）
- [ ] AI 助手语音输入（系统原生语音识别）
- [ ] 年度报告 AI 解读
- [ ] 光影配置 Token 编码/解析
- [ ] 文件变更检测弹窗汇总
- [ ] CLI 命令行完整实现

### 技术债务
- [ ] 7 个 CS 警告（async 无 await / 未等待调用 / nullability）建议清理
- [ ] Core 工程 8 个预存 CA1416 警告（net6.0 引用 Windows API）

---

## 编译验证环境

```
SDK: .NET 6.0.301 (csc/Roslyn)
参考程序集: Microsoft.WindowsDesktop.App.Ref 8.0.29
           Microsoft.NETCore.App.Ref 8.0.29
NuGet 源: https://repo.huaweicloud.com/repository/nuget/v3/index.json
编译命令: dotnet /usr/share/dotnet/sdk/6.0.301/Roslyn/bincore/csc.dll
          -target:library -langversion:latest -nullable:enable
产出: MCLCS.Core.dll (516 KB) + MCLCS.App.dll (273 KB)
```
