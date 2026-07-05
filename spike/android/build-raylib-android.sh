#!/usr/bin/env bash
# Build Raylib as a STATIC library (libraylib.a) for Android.
#
# This is the reliable, supported path (the shared-.so build is broken upstream:
# raysan5/raylib#4484). Produces libs/<abi>/libraylib.a for static-linking into
# the NativeAOT module.
#
# Usage:  ./build-raylib-android.sh [abi]        (default: arm64-v8a)
#   abi ∈ { arm64-v8a, armeabi-v7a, x86_64, x86 }
#
# Requires: ANDROID_NDK_HOME, cmake, git, ninja.  UNTESTED in CI — run locally.
set -euo pipefail

ABI="${1:-arm64-v8a}"
RAYLIB_TAG="6.0"          # matches Raylib-cs 8.0.0's TargetRaylibTag
API="${ANDROID_API_VERSION:-29}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$HERE/libs/$ABI"

: "${ANDROID_NDK_HOME:?Set ANDROID_NDK_HOME to your Android NDK path}"

WORK="$HERE/.raylib-src"
if [ ! -d "$WORK" ]; then
  git clone --depth 1 --branch "$RAYLIB_TAG" https://github.com/raysan5/raylib.git "$WORK"
fi

BUILD="$WORK/build-android-$ABI"
cmake -S "$WORK" -B "$BUILD" -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI="$ABI" \
  -DANDROID_PLATFORM="android-$API" \
  -DPLATFORM=Android \
  -DBUILD_SHARED_LIBS=OFF \
  -DBUILD_EXAMPLES=OFF \
  -DCMAKE_BUILD_TYPE=Release

cmake --build "$BUILD" --target raylib

mkdir -p "$OUT"
find "$BUILD" -name 'libraylib.a' -exec cp {} "$OUT/" \;
echo "==> Wrote $OUT/libraylib.a"
