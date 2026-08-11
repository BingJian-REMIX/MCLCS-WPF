---
title: bug文档
---

# Bug 归档与状态

- 本文档归档测试过程中发现的 bug 与 UI 修改项，并跟踪修复状态。
- 标记说明：`✅ 已修复` / `⚠️ 待处理（功能缺口）` / `🆕 本轮新增`

---

## 一、UI / 交互（历史项 1–28，均在前期回合修复）

| # | 描述 | 状态 |
|---|------|------|
| 1 | 启动器默认亮色模式启动 | ✅ 已修复 |
| 2 | 设置页工具页右边副侧边栏移除（功能重复） | ✅ 已修复 |
| 3 | 暗色模式文字应为白色（强对比度） | ✅ 已修复 |
| 4 | 索引贴大页动画缺失 | ✅ 已修复 |
| 5 | 外观设置未保存（未持久化） | ✅ 已修复 |
| 6 | 从设置页进入下载页崩溃 | ✅ 已修复 |
| 7 | 无法将下载任务挂入下载队列 | ✅ 已修复 |
| 8 | 启动器未全屏时索引贴大页失效 | ✅ 已修复 |
| 9 | 加载器选择下拉框位置重叠 | ✅ 已修复 |
| 10 | 字体缩放失效 | ✅ 已修复 |
| 11 | 侧边栏/按键/滑动开关主题色失效 | ✅ 已修复 |
| 12 | 全局/mod 等搜索栏失效 | ✅ 已修复 |
| 13 | 移除地图下载副标签内重复搜索栏 | ✅ 已修复 |
| 14 | 下载页队列未合并至全局搜索栏右侧进度弹窗 | ✅ 已修复 |
| 15 | 背景图片应用失败 | ✅ 已修复 |
| 16 | Minecraft 下载副页做成各版本折叠卡片式（避免无响应） | ✅ 已修复 |
| 17 | mod/资源包卡片适配亮暗色并模糊化封面 | ✅ 已修复 |
| 18 | 全局搜索栏重定向到设置子项 | ✅ 已修复 |
| 19 | 索引贴上文字居中 | ✅ 已修复 |
| 20 | 下载页暗色模式字体发黑 | ✅ 已修复 |
| 21 | Minecraft 核心游戏文件无法下载 | ✅ 已修复（含 Piston v2 升级，见下） |
| 22 | 设置-启动子项添加"选择 Minecraft 游戏路径" | ✅ 已修复 |
| 23 | 移除原生 WPF 按键样式，沿用设计样式 | ✅ 已修复 |
| 24 | 创建下拉框全局样式并应用 | ✅ 已修复（本轮：新增无 x:Key 的全局 ComboBox 隐式样式） |
| 25 | Minecraft 下载队列持续置灰 | ✅ 已修复（本轮：`CurrentInstallVersion` setter 显式 `RaiseCanExecuteChanged()`） |
| 26 | 最小化到托盘开关无效 | ✅ 已修复（2026-08-12：新增 `Services/TrayIconService.cs`，用 Win32 `Shell_NotifyIcon` P/Invoke 实现，刻意不用 WinForms 以免与 WPF 类型撞名；`MainWindow` 处理 `StateChanged` 在开启时隐藏到托盘，托盘图标双击/右键「打开主界面」恢复、「退出」关闭）。`*2026-08-12 重新审查：逐项核查托盘代码——消息窗口/HwndSource 钩子、图标加载回退、右键菜单、Dispose 删除图标均无误；仅依赖用户绘制的 tray.ico（缺失亦不影响功能）*` |
| 27 | 配置外置登录时异常卡死 | ✅ 已修复（本轮：`AddAuthlibAccount` 改为 `async Task` + await，事件处理器改 `async void`） |
| 28 | hub 附加层未生效 | ✅ 已修复（2026-08-12：触发逻辑上移到 `GameLauncher.GameProcessStarted`，覆盖全部启动路径；移除 1.5s 竞态。`*2026-08-12 重新审查补全*`：原 `OnTick` 把 `Sample` 第二参数（应为最大堆内存 MB）误传为 `OnlyWhenGameForeground ? 0 : FontSize`——导致默认配置内存百分比永不显示、关闭该开关后内存读数变成垃圾值（如 `500 / 12 MB (4166%)`），且"仅前台显示"功能实为**空操作**。已改为从启动参数 `-Xmx` 解析真实最大堆内存传入 HUD，并实现真正的前台判定（`GetForegroundWindow`）；`OnlyWhenGameForeground` 默认值改为 `false`（常显，避免非前台时 HUD 看起来空白）） |

---

## 二、本轮（2026-08-10）新增并修复的安装器缺陷

在"逐项修复"排查中，对安装器做了真实代码审查，发现并修复 3 个版本选择缺陷：

| # | 文件 | 缺陷 | 修复 |
|---|------|------|------|
| 🆕 I-1 | `src/MCLCS.Core/Installers/FabricInstaller.cs` | 当版本元数据为空列表时 `entries.First()` 抛 `InvalidOperationException` 崩溃 | 改为 `FirstOrDefault()` + 空值抛明确异常 |
| 🆕 I-2 | `src/MCLCS.Core/Installers/QuiltInstaller.cs` | 用 `StringComparer.OrdinalIgnoreCase` 字典序选最新版本，导致 `"0.9.1" > "0.10.0"` 之类误选 | 改用语义化 `VersionComparer.Instance` |
| 🆕 I-3 | `src/MCLCS.Core/Installers/NeoForgeInstaller.cs` | 同上，matched 分支与全量回退分支均用字典序比较 | 两处均改用 `VersionComparer.Instance` |

新增复用组件：`src/MCLCS.Core/Utils/VersionComparer.cs`（语义化版本比较器，数值段按数值、非数值段按字典序，正确处理 `0.9.1 < 0.10.0`、`1.20.4-9.0.0 < 1.20.4-49.0.0`）。

---

## 三、Piston v2 元数据源升级（2026-08-10）

官方已废弃 `launchermeta.mojang.com`（v1），现行核心文件源为 Piston v2：

- `GameConstants.cs`：官方清单主机名升级为 `https://piston-meta.mojang.com`，清单改为 `version_manifest_v2.json`；BMCLAPI 镜像同步为 v2 清单。
- `VersionManifest.cs`：类注释更新，说明 v2 新增 `sha1` / `complianceLevel` 字段。
- `NetworkDiagnostics.cs`：默认诊断端点更新为 Piston v2 清单与 BMCLAPI v2 清单。

验证：Piston v2 清单、client.jar（`piston-data.mojang.com/v1/objects/<sha1>/client.jar`）、资源索引均可 200 获取；BMCLAPI v2 清单返回 302 需跟随。

---

## 四、待处理（功能缺口，需较大实现）

（#26 最小化到托盘、#28 hub 附加层 已于 2026-08-12 修复，见上表。）

---

## 备注

- 托盘图标：将绘制好的 `tray.ico` 放到 `src/MCLCS.App/Resources/tray.ico`（或输出目录根 `tray.ico`），构建时自动复制到输出目录（`MCLCS.App.csproj` 已加条件项）；图标缺失时托盘仍可正常工作，只是无图标。
- 最小化到托盘为「常驻托盘图标」模式：图标始终存在，提供「打开主界面 / 退出」；仅当设置项「最小化到托盘」开启时，点最小化才隐藏主窗口。
- 除上述外，`OllamaStatusToBrushConverter.ConvertBack` 抛 `NotImplementedException` 属转换惯例（OneWay 不触发），非实际崩溃，未改动。
- 主程序 `dotnet build src/MCLCS.App/MCLCS.App.csproj -c Debug`：**0 错误**（仅 26 个历史 warning，与本轮回合无关）。
