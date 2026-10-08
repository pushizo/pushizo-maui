#!/usr/bin/env bash
# HeroSignal MAUI iOS köprülerini XCFramework olarak üretir (macOS + Xcode 15+ gerekir).
# Kullanım: ./build-xcframeworks.sh [çıktı klasörü]   (varsayılan: ../build/xcframeworks)
# Çıktı: HeroSignalMauiBridge.xcframework, HeroSignalMauiNotificationService.xcframework (cihaz + simülatör).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="${1:-$SCRIPT_DIR/../build/xcframeworks}"
WORK_DIR="$SCRIPT_DIR/../build/xcode"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "HeroSignal: XCFramework üretimi yalnızca macOS'ta (Xcode) yapılabilir." >&2
  exit 1
fi
command -v xcodebuild >/dev/null || { echo "HeroSignal: xcodebuild bulunamadı (Xcode kurun)." >&2; exit 1; }

mkdir -p "$OUT_DIR" "$WORK_DIR"
cd "$SCRIPT_DIR"

# $1: şema/ürün adı, $2: ek xcodebuild ayarları (uzantı güvenli API vb.)
build_framework() {
  local scheme="$1"
  local extra="$2"
  local args=()
  for platform in "iOS" "iOS Simulator"; do
    local slug
    slug="$(echo "$platform" | tr ' ' '_')"
    local archive="$WORK_DIR/$scheme-$slug.xcarchive"
    rm -rf "$archive"
    # shellcheck disable=SC2086
    xcodebuild archive \
      -scheme "$scheme" \
      -destination "generic/platform=$platform" \
      -archivePath "$archive" \
      -derivedDataPath "$WORK_DIR/DerivedData" \
      SKIP_INSTALL=NO \
      BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
      $extra \
      -quiet
    # SPM dinamik ürünleri arşivde Products/usr/local/lib veya Products/Library/Frameworks altında olabilir.
    local framework
    framework="$(find "$archive/Products" -type d -name "$scheme.framework" -print -quit)"
    if [[ -z "$framework" ]]; then
      echo "HeroSignal: $scheme.framework arşivde bulunamadı ($archive)." >&2
      exit 1
    fi
    args+=(-framework "$framework")
  done
  rm -rf "$OUT_DIR/$scheme.xcframework"
  xcodebuild -create-xcframework "${args[@]}" -output "$OUT_DIR/$scheme.xcframework"
  echo "HeroSignal: $OUT_DIR/$scheme.xcframework"
}

build_framework "HeroSignalMauiBridge" ""
build_framework "HeroSignalMauiNotificationService" "APPLICATION_EXTENSION_API_ONLY=YES"
