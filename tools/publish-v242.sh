#!/bin/bash
# MCLCS v2.4.2 发布脚本
# 发布 GUI 启动器 + CLI 工具，生成 portable 与 single-file 两种 ZIP 包
set -e

export DOTNET_ROOT=/opt/dotnet10
DOTNET=/opt/dotnet10/dotnet
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/dist/v2.4.2"
RID="win-x64"

echo "=== MCLCS v2.4.2 Publish ==="
echo "Root: $ROOT"

mkdir -p "$OUT"

# --- 1. GUI 启动器 (portable) ---
echo ""
echo "[1/4] Publishing GUI (portable, framework-dependent)..."
$DOTNET publish "$ROOT/src/MCLCS.App/MCLCS.App.csproj" \
    -c Release \
    -f net8.0-windows \
    -r $RID \
    -p:EnableWindowsTargeting=true \
    -p:PublishReadyToRun=false \
    --output "$OUT/gui-portable"

# --- 2. CLI 工具 (portable) ---
echo ""
echo "[2/4] Publishing CLI (portable, framework-dependent)..."
$DOTNET publish "$ROOT/tools/MCLCS.Cli/MCLCS.Cli.csproj" \
    -c Release \
    -f net8.0-windows \
    -r $RID \
    -p:EnableWindowsTargeting=true \
    -p:PublishReadyToRun=false \
    --output "$OUT/cli-portable"

# --- 3. GUI 启动器 (single-file) ---
echo ""
echo "[3/4] Publishing GUI (single-file, self-contained)..."
$DOTNET publish "$ROOT/src/MCLCS.App/MCLCS.App.csproj" \
    -c Release \
    -f net8.0-windows \
    -r $RID \
    -p:EnableWindowsTargeting=true \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    --output "$OUT/gui-singlefile"

# --- 4. CLI 工具 (single-file) ---
echo ""
echo "[4/4] Publishing CLI (single-file, self-contained)..."
$DOTNET publish "$ROOT/tools/MCLCS.Cli/MCLCS.Cli.csproj" \
    -c Release \
    -f net8.0-windows \
    -r $RID \
    -p:EnableWindowsTargeting=true \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -p:PublishReadyToRun=false \
    --output "$OUT/cli-singlefile"

# --- 打包 ZIP ---
echo ""
echo "=== Packaging ZIP ==="
cd "$OUT"

# 合并 portable
mkdir -p "MCLCS-v2.4.2-portable"
cp -r gui-portable/* "MCLCS-v2.4.2-portable/"
cp -r cli-portable/* "MCLCS-v2.4.2-portable/"
zip -r "MCLCS-v2.4.2-portable.zip" "MCLCS-v2.4.2-portable/" > /dev/null

# 合并 single-file
mkdir -p "MCLCS-v2.4.2-singlefile"
cp -r gui-singlefile/* "MCLCS-v2.4.2-singlefile/"
cp -r cli-singlefile/* "MCLCS-v2.4.2-singlefile/"
zip -r "MCLCS-v2.4.2-singlefile.zip" "MCLCS-v2.4.2-singlefile/" > /dev/null

echo "=== Done ==="
ls -la "$OUT"/*.zip
echo ""
echo "Portable ZIP:  $(du -h "$OUT/MCLCS-v2.4.2-portable.zip" | cut -f1)"
echo "SingleZIP:     $(du -h "$OUT/MCLCS-v2.4.2-singlefile.zip" | cut -f1)"
