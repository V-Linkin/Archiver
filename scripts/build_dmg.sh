#!/bin/bash
set -e

# ============================================================
# 拾屿 Archiver — 本地 DMG 打包（不上传 Release）
# 用法: ./scripts/build_dmg.sh [version]
# 示例: ./scripts/build_dmg.sh 1.1.16
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_DIR"

# ---------- 版本号 ----------
VERSION="${1:-$(grep -A1 'CFBundleShortVersionString' Info.plist | grep string | sed 's/.*<string>\(.*\)<\/string>.*/\1/')}"
ENGLISH_NAME="Archiver"
DMG_NAME="${ENGLISH_NAME}_v${VERSION}.dmg"
APP_DISPLAY_NAME="拾屿"

echo "======================================"
echo "  拾屿 Archiver — Local DMG Builder"
echo "  版本: v${VERSION}"
echo "  DMG: ${DMG_NAME}"
echo "======================================"

# ---------- 1. 确保 XcodeGen 生成项目 ----------
echo ""
echo "[1/5] 生成 Xcode 项目..."
if command -v xcodegen &> /dev/null; then
    xcodegen generate --spec "$PROJECT_DIR/project.yml"
else
    echo "  xcodegen 未安装，跳过（确保 .xcodeproj 已存在）"
fi

# ---------- 2. 构建 Release ----------
echo ""
echo "[2/5] 构建 Release..."
BUILD_DIR="$PROJECT_DIR/build"
rm -rf "$BUILD_DIR"

xcodebuild build \
    -project "$PROJECT_DIR/Archiver.xcodeproj" \
    -scheme Archiver \
    -configuration Release \
    -derivedDataPath "$BUILD_DIR" \
    -destination 'platform=macOS' \
    CODE_SIGN_IDENTITY="-" \
    CODE_SIGNING_ALLOWED=NO \
    2>&1 | tail -3

APP_PATH="$BUILD_DIR/Build/Products/Release/Archiver.app"
if [ ! -d "$APP_PATH" ]; then
    echo "❌ 构建失败，找不到 App: $APP_PATH"
    exit 1
fi
echo "  ✅ 构建成功: $APP_PATH"

# ---------- 2.5. Ad-hoc 签名 ----------
echo ""
echo "[2.5/5] 签名 App..."
codesign --force --deep --sign - "$APP_PATH"
echo "  ✅ 签名完成"

# ---------- 3. 创建 DMG 临时目录 ----------
echo ""
echo "[3/5] 打包 DMG..."
DMG_TEMP="$PROJECT_DIR/build/dmg_temp"
DMG_OUTPUT="$PROJECT_DIR/build/${DMG_NAME}"
rm -rf "$DMG_TEMP" "$DMG_OUTPUT"
mkdir -p "$DMG_TEMP"

# 复制 App（重命名为中文名）
cp -R "$APP_PATH" "$DMG_TEMP/${APP_DISPLAY_NAME}.app"

# 创建 Applications 快捷方式
ln -s /Applications "$DMG_TEMP/Applications"

# ---------- 4. 生成 DMG ----------
echo ""
echo "[4/5] 生成 DMG 镜像..."
hdiutil create \
    -volname "$APP_DISPLAY_NAME" \
    -srcfolder "$DMG_TEMP" \
    -ov \
    -format UDZO \
    -imagekey zlib-level=9 \
    "$DMG_OUTPUT"

echo "  ✅ DMG 已生成: $DMG_OUTPUT"
ls -lh "$DMG_OUTPUT"

# ---------- 5. 清理 ----------
echo ""
echo "[5/5] 清理临时文件..."
rm -rf "$DMG_TEMP" "$BUILD_DIR"
echo "  ✅ 清理完成"

echo ""
echo "======================================"
echo "  🎉 本地 DMG 打包完成!"
echo "  路径: ${DMG_OUTPUT}"
echo "  ⚠️  未上传到 GitHub Release"
echo "======================================"
