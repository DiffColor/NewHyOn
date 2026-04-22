#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <target> <artifact-prefix> <output-dir>" >&2
  exit 1
fi

target="$1"
artifact_prefix="$2"
output_dir="$3"
repo_root="$(pwd)"
sdk_root="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}"

if [[ -z "$sdk_root" ]]; then
  echo "ANDROID_SDK_ROOT or ANDROID_HOME must be set." >&2
  exit 1
fi

apksigner="$(find "$sdk_root/build-tools" -mindepth 2 -maxdepth 2 -type f -name apksigner | sort -V | tail -n 1)"
if [[ -z "$apksigner" || ! -x "$apksigner" ]]; then
  echo "apksigner not found under: $sdk_root/build-tools" >&2
  exit 1
fi

mkdir -p "$output_dir"

abs_path() {
  local rel="$1"
  local dir
  dir="$(cd "$(dirname "$rel")" && pwd)"
  printf '%s/%s\n' "$dir" "$(basename "$rel")"
}

build_gradle_release() {
  local root="$1"
  local assemble_task="$2"
  local output_rel="$3"
  local output_name="$4"
  local keystore_rel="$5"
  local store_pass="$6"
  local key_alias="$7"
  local key_pass="$8"

  local keystore_path
  keystore_path="$(abs_path "$keystore_rel")"

  printf "sdk.dir=%s\n" "$sdk_root" > "$root/local.properties"
  chmod +x "$root/gradlew"

  (
    cd "$root"
    export ANDROID_KEYSTORE_PATH="$keystore_path"
    export ANDROID_KEYSTORE_PASSWORD="$store_pass"
    export ANDROID_KEY_ALIAS="$key_alias"
    export ANDROID_KEY_PASSWORD="$key_pass"
    unset ANDROID_NDK_HOME ANDROID_NDK_ROOT NDK_HOME NDK_ROOT
    ./gradlew --no-daemon clean "$assemble_task" -x lintVitalRelease -x lint
  )

  local apk_path="$root/$output_rel"
  if [[ ! -f "$apk_path" ]]; then
    echo "APK not found: $apk_path" >&2
    exit 1
  fi

  "$apksigner" verify --verbose "$apk_path"
  cp "$apk_path" "$output_dir/$output_name"
}

build_gradle_release_from_find() {
  local root="$1"
  local assemble_task="$2"
  local search_dir="$3"
  local output_name="$4"
  local keystore_rel="$5"
  local store_pass="$6"
  local key_alias="$7"
  local key_pass="$8"

  local keystore_path
  keystore_path="$(abs_path "$keystore_rel")"

  printf "sdk.dir=%s\n" "$sdk_root" > "$root/local.properties"
  chmod +x "$root/gradlew"

  (
    cd "$root"
    export ANDROID_KEYSTORE_PATH="$keystore_path"
    export ANDROID_KEYSTORE_PASSWORD="$store_pass"
    export ANDROID_KEY_ALIAS="$key_alias"
    export ANDROID_KEY_PASSWORD="$key_pass"
    unset ANDROID_NDK_HOME ANDROID_NDK_ROOT NDK_HOME NDK_ROOT
    ./gradlew --no-daemon clean "$assemble_task" -x lintVitalRelease -x lint
  )

  local apk_path
  apk_path="$(find "$search_dir" -maxdepth 1 -type f -name "*.apk" ! -name "*-unsigned.apk" | head -n 1)"
  if [[ -z "$apk_path" || ! -f "$apk_path" ]]; then
    echo "Signed APK not found under: $search_dir" >&2
    exit 1
  fi

  "$apksigner" verify --verbose "$apk_path"
  cp "$apk_path" "$output_dir/$output_name"
}

build_quber_release() {
  local quber_root="$1"
  local output_name="$2"
  local quber_keystore="Player/Android/_shared/platformkeys/quber/platform.jks"

  build_gradle_release \
    "$quber_root" \
    ":app:assembleRelease" \
    "app/build/outputs/apk/release/app-release.apk" \
    "$output_name" \
    "$quber_keystore" \
    "zbqj2636" \
    "quber" \
    "zbqj2636"
}

build_player_release_pair() {
  local quber_root="$1"
  local gl_root="$2"
  local pair_prefix="$3"
  local quber_keystore="Player/Android/_shared/platformkeys/quber/platform.jks"

  build_quber_release \
    "$quber_root" \
    "${pair_prefix}-quber.apk"

  build_gradle_release \
    "$gl_root" \
    ":app:assembleRelease" \
    "app/build/outputs/apk/release/app-release.apk" \
    "${pair_prefix}-gl.apk" \
    "$quber_keystore" \
    "zbqj2636" \
    "quber" \
    "zbqj2636"
}

case "$target" in
  player)
    build_player_release_pair \
      "Player/Android/Quber/Quber_Player" \
      "Player/Android/GL/GL_Player" \
      "$artifact_prefix"
    ;;
  notifier)
    build_quber_release \
      "Player/Android/Quber/Notifier" \
      "${artifact_prefix}-quber.apk"
    ;;
  usbinstaller)
    build_quber_release \
      "Player/Android/Quber/USBInstaller_4launcher" \
      "${artifact_prefix}-quber.apk"
    ;;
  launcher)
    build_gradle_release_from_find \
      "Player/Android/TurtleLauncher" \
      ":TurtleLauncher:assembleRelease" \
      "Player/Android/TurtleLauncher/TurtleLauncher/build/outputs/apk/release" \
      "${artifact_prefix}.apk" \
      "Player/Android/_shared/platformkeys/quber/platform.jks" \
      "zbqj2636" \
      "quber" \
      "zbqj2636"
    ;;
  watchdog)
    build_gradle_release_from_find \
      "Player/Android/WatchDog_4launcher" \
      ":app:assembleRelease" \
      "Player/Android/WatchDog_4launcher/app/build/outputs/apk/release" \
      "${artifact_prefix}.apk" \
      "Player/Android/_shared/platformkeys/quber/platform.jks" \
      "zbqj2636" \
      "quber" \
      "zbqj2636"
    ;;
  *)
    echo "Unsupported target: $target" >&2
    exit 1
    ;;
esac
