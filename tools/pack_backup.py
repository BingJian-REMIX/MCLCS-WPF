#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MCLCS 源码备份打包器
----------------------------------------------------------------------
把仓库源码打成 ZIP，并内嵌进一个「完全自包含」的 HTML：
  · 断网可浏览全部源码（浏览器原生 deflate-raw 解压，无外部依赖）
  · 一键还原出与仓库一致的 ZIP
  · 概览页 + docs/PENDING.md 待开发清单

用法：
    python3 tools/pack_backup.py --tag T0-baseline \
        --title "梯队开工前基线" --overview docs/backup-notes/T0.md

以后每完成一个梯队跑一次即可，例如 --tag T1。
"""
import argparse
import base64
import datetime
import os
import re
import sys
import zipfile
from pathlib import Path

# ── 收录 / 排除规则 ────────────────────────────────────────────────
INCLUDE_EXT = {
    ".cs", ".xaml", ".csproj", ".sln", ".md", ".json", ".config",
    ".props", ".targets", ".yml", ".yaml", ".py", ".manifest",
    ".txt", ".editorconfig", ".ps1", ".sh", ".html", ".css",
}
INCLUDE_NAME = {".gitignore", ".editorconfig", "NuGet.config", "Directory.Build.props"}
EXCLUDE_DIR = {"obj", "bin", ".git", ".vs", ".idea", "node_modules", "dist", "packages", "TestResults"}
MAX_FILE_BYTES = 2 * 1024 * 1024  # 单文件上限，防止误收大二进制


def collect(root: Path):
    """遍历仓库，返回 [(绝对路径, 仓库相对路径)]，稳定排序。"""
    items = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if d not in EXCLUDE_DIR and not d.startswith(".git"))
        for fn in sorted(filenames):
            p = Path(dirpath) / fn
            if p.suffix.lower() not in INCLUDE_EXT and fn not in INCLUDE_NAME:
                continue
            try:
                if p.stat().st_size > MAX_FILE_BYTES:
                    print(f"  [skip 过大] {p.relative_to(root)}")
                    continue
            except OSError:
                continue
            items.append((p, p.relative_to(root).as_posix()))
    return items


def count_lines(path: Path) -> int:
    try:
        with path.open("rb") as f:
            return f.read().count(b"\n") + 1
    except OSError:
        return 0


def detect_version(root: Path) -> str:
    csproj = root / "src" / "MCLCS.App" / "MCLCS.App.csproj"
    if csproj.exists():
        m = re.search(r"<Version>\s*([^<]+?)\s*</Version>", csproj.read_text(encoding="utf-8", errors="ignore"))
        if m:
            return m.group(1)
    return "0.0.0"


def main() -> int:
    ap = argparse.ArgumentParser(description="MCLCS 源码备份打包器")
    ap.add_argument("--tag", required=True, help="备份标记，如 T0-baseline / T1 / T2")
    ap.add_argument("--title", default="", help="标题副文本")
    ap.add_argument("--overview", default="", help="概览说明 Markdown 文件路径（相对仓库根）")
    ap.add_argument("--root", default=str(Path(__file__).resolve().parent.parent))
    ap.add_argument("--out", default="", help="输出目录，默认 <root>/dist/<tag>")
    args = ap.parse_args()

    root = Path(args.root).resolve()
    tpl_path = Path(__file__).resolve().parent / "backup_template.html"
    if not tpl_path.exists():
        print(f"✗ 缺少模板 {tpl_path}")
        return 1

    out_dir = Path(args.out) if args.out else root / "dist" / args.tag
    out_dir.mkdir(parents=True, exist_ok=True)

    version = detect_version(root)
    date = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    zip_name = f"MCLCS-{args.tag}-source.zip"
    html_name = f"MCLCS-{args.tag}-源码备份.html"
    zip_path = out_dir / zip_name
    html_path = out_dir / html_name

    # 1) 收集
    print(f"▸ 扫描 {root}")
    items = collect(root)
    if not items:
        print("✗ 没有收集到任何文件")
        return 1
    total_bytes = sum(p.stat().st_size for p, _ in items)
    total_lines = sum(count_lines(p) for p, _ in items)
    print(f"  收录 {len(items)} 个文件 / {total_lines} 行 / {total_bytes/1024:.1f} KB")

    # 2) 打 ZIP（固定时间戳，保证同内容产出一致）
    print(f"▸ 打包 {zip_path.name}")
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for p, rel in items:
            zi = zipfile.ZipInfo(rel, date_time=(2026, 1, 1, 0, 0, 0))
            zi.compress_type = zipfile.ZIP_DEFLATED
            zi.external_attr = 0o644 << 16
            z.writestr(zi, p.read_bytes())
    zip_bytes = zip_path.read_bytes()
    print(f"  ZIP {len(zip_bytes)/1024:.1f} KB（压缩率 {len(zip_bytes)/total_bytes*100:.1f}%）")

    # 3) 概览 Markdown
    ov_md = ""
    if args.overview:
        ovp = root / args.overview
        if ovp.exists():
            ov_md = ovp.read_text(encoding="utf-8")
        else:
            print(f"  [warn] 概览文件不存在：{ovp}")
    if "</script" in ov_md.lower():
        print("✗ 概览 Markdown 含 </script，会破坏 HTML")
        return 1

    # 4) 生成 HTML
    print(f"▸ 生成 {html_path.name}")
    html = tpl_path.read_text(encoding="utf-8")
    repl = {
        "__TITLE__": f"MCLCS {args.tag} 源码备份" + (f" — {args.title}" if args.title else ""),
        "__TAG__": args.tag,
        "__VERSION__": version,
        "__DATE__": date,
        "__NFILES__": str(len(items)),
        "__NLINES__": f"{total_lines:,}",
        "__SRCKB__": f"{total_bytes/1024:.0f}",
        "__ZIPKB__": f"{len(zip_bytes)/1024:.0f}",
        "__ZIP_NAME__": zip_name,
        "__SELF_NAME__": html_name,
        "__OVERVIEW_MD__": ov_md,
        "__ZIP_B64__": base64.b64encode(zip_bytes).decode("ascii"),
    }
    for k, v in repl.items():
        html = html.replace(k, v)

    left = re.findall(r"__[A-Z_]+__", html)
    if left:
        print(f"✗ 仍有未替换占位符：{sorted(set(left))}")
        return 1

    html_path.write_text(html, encoding="utf-8")
    size = html_path.stat().st_size
    print(f"  HTML {size/1024:.1f} KB")
    print()
    print("✅ 完成")
    print(f"   {html_path}")
    print(f"   {zip_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
