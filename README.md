# MCLCS 发布归档（history 分支）

> 本分支（history）**仅归档各版本发布产物**，不随源码主线（main）入库。
> 源码主线见 `main` 分支；历史演进快照见主线 `history/` 目录；开发文档见主线 `docs/` 目录。

本 README 由主线 `history/README.md`（项目历史档案）迁移而来，用于说明本归档分支的内容与定位。

## 本分支内容

| 路径 | 说明 |
|---|---|
| `dist/` | 各版本发布包（ZIP 与源码快照），作为对应 Release 的附件 |

## 时间线

```
批处理 / PowerShell 原型（.bat / .ps1 手搓期，本仓库最早雏形）
  └─ UI 迭代原型（纯 HTML 探索期）
       └─ v0.1 – v1.1（WPF 起步 + 下载中心 + 崩溃修复）
            └─ v2.0 – v2.4（重写期：四色索引贴、工具箱、AI、皮肤编辑器）
                 └─ T0–T4 里程碑（见 dist/）
                      └─ v2.4.2（当前主线，见 main 分支根 src/）
```

## dist/ 目录说明

| 目录 / 文件 | 内容 |
|---|---|
| `dist/T0-baseline` … `dist/T4` | 任务式补全过程中的源码快照（T0–T4 里程碑） |
| `dist/v2.2-final` | v2.2 收官终版快照（源码 zip + 备份 HTML） |
| `dist/v2.4.2/MCLCS-v2.4.2-portable.zip` | v2.4.2 发布包：GUI 启动器（`MCLCS.App.exe`）+ CLI（`mclcs.exe`），依赖 .NET 8 运行时 |
| `dist/v2.4.2/MCLCS-v2.4.2-singlefile.zip` | v2.4.2 自包含发布包：免运行时，GUI + CLI 均已打包 |

## 与主线版本对照

| 阶段 | 位置 | 说明 |
|---|---|---|
| T0–T4 里程碑 | 本分支 `dist/T0-baseline` … `dist/T4` | 任务式补全过程中的源码快照 |
| v2.2-final | 本分支 `dist/v2.2-final` | 收官终版快照 |
| 当前源码 | `main` 分支根 `src/` | v2.4.2 实际代码 |
| 历史演进快照 | `main` 分支 `history/` | 原型与旧版本源码、HTML 运行快照 |

## 下载与使用

- 发布包以 ZIP 形式提供，作为对应版本 Release 的附件，不在源码主线中。
- `v2.4.2` 包含 GUI 与 CLI 两种形态，提供 portable（依赖运行时）与 single-file（自包含）两种打包。
- 源码主线（main）下载后仅含当代源码与文档，**不含** dist/ 构建产物。
