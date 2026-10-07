#!/usr/bin/env bash
# HeroSignalMauiBridge.xcframework üretir (cihaz + simülatör). macOS + Xcode 15+ gerekir.
# Kullanım: ./build-xcframework.sh [çıktı klasörü]   (varsayılan: <bu klasör>/build)
# Çıktı yalnızca verilen klasöre yazılır; sdk/ios altına derleme çıktısı yazılmaz (DerivedData da çıktı klasöründedir).
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${1:-$HERE/build}"
SCHEME="HeroSignalMauiBridge"
DERIVED="$OUT/DerivedData"
XCFRAMEWORK="$OUT/$SCHEME.xcframework"

mkdir -p "$OUT"
rm -rf "$XCFRAMEWORK" "$OUT/ios.xcarchive" "$OUT/ios-simulator.xcarchive"

archive() {
  local destination="$1" name="$2"
  (cd "$HERE" && xcodebuild archive \
    -scheme "$SCHEME" \
    -destination "$destination" \
    -archivePath "$OUT/$name.xcarchive" \
    -derivedDataPath "$DERIVED" \
    -quiet \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES)
}

# Arşivdeki framework yolu Xcode sürümüne göre değişebildiğinden aranarak bulunur.
framework_in() {
  local archive="$1" path
  path="$(find "$archive/Products" -type d -name "$SCHEME.framework" -print -quit)"
  if [[ -z "$path" ]]; then
    echo "HeroSignal: $archive içinde $SCHEME.framework bulunamadı" >&2
    exit 1
  fi
  echo "$path"
}

archive "generic/platform=iOS" "ios"
archive "generic/platform=iOS Simulator" "ios-simulator"

xcodebuild -create-xcframework \
  -framework "$(framework_in "$OUT/ios.xcarchive")" \
  -framework "$(framework_in "$OUT/ios-simulator.xcarchive")" \
  -output "$XCFRAMEWORK"

echo "HeroSignal: $XCFRAMEWORK üretildi"
