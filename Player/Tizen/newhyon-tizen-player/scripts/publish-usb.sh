#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WEB_WGT="${PROJECT_ROOT}/Build/NewHyOnTizenPlayer.wgt"
PUBLISH_DIR="${PROJECT_ROOT}/publish/SSSP"
PUBLISH_WGT="${PUBLISH_DIR}/NewHyOnTizenPlayer.wgt"

resolve_tizen_cli() {
  if [[ -n "${TIZEN_CLI:-}" ]]; then
    command -v "${TIZEN_CLI}" >/dev/null 2>&1 || {
      printf 'TIZEN_CLI를 실행할 수 없습니다: %s\n' "${TIZEN_CLI}" >&2
      exit 1
    }
    command -v "${TIZEN_CLI}"
    return
  fi

  if command -v tz >/dev/null 2>&1; then
    command -v tz
    return
  fi

  if command -v tizen >/dev/null 2>&1; then
    command -v tizen
    return
  fi

  printf 'Tizen CLI를 찾지 못했습니다. TIZEN_CLI를 지정하거나 tz/tizen을 PATH에 추가하십시오.\n' >&2
  exit 1
}

resolve_tizen_profile() {
  local profile="${NEWHYON_TIZEN_PROFILE:-${SAMPLE_NEWHYON_TIZEN_PROFILE:-${PLAYER_TIZEN_PROFILE:-${TIZEN_PROFILE_NAME:-turtlelab-partner}}}}"
  printf '%s' "${profile}"
}

TIZEN_CLI_PATH="$(resolve_tizen_cli)"
TIZEN_PROFILE="$(resolve_tizen_profile)"

cd "${PROJECT_ROOT}"

TIZEN_CLI="${TIZEN_CLI_PATH}" NEWHYON_TIZEN_PROFILE="${TIZEN_PROFILE}" npm run package:wgt
bash "${SCRIPT_DIR}/verify-sssp-package.sh" "${WEB_WGT}"

mkdir -p "${PUBLISH_DIR}"
rm -f "${PUBLISH_DIR}"/*.wgt "${PUBLISH_DIR}"/*.tpk "${PUBLISH_DIR}/sssp_config.xml"
cp "${WEB_WGT}" "${PUBLISH_WGT}"

WGT_SIZE_KB="$(du -k "${PUBLISH_WGT}" | awk '{print $1}')"
WGT_VERSION="$(unzip -p "${PUBLISH_WGT}" config.xml | perl -ne 'if (/<widget[^>]*\sversion="([^"]+)"/) { print $1; exit }')"
[[ -n "${WGT_VERSION}" ]] || {
  printf 'WGT version을 config.xml에서 읽지 못했습니다.\n' >&2
  exit 1
}
cat > "${PUBLISH_DIR}/sssp_config.xml" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<widget>
    <ver>${WGT_VERSION}</ver>
    <size>${WGT_SIZE_KB}</size>
    <widgetname>NewHyOnTizenPlayer</widgetname>
    <webtype>tizen</webtype>
</widget>
EOF

printf 'USB publish created: %s\n' "${PUBLISH_DIR}"
printf 'SSSP WGT: %s\n' "${PUBLISH_WGT}"
printf 'SSSP config: %s\n' "${PUBLISH_DIR}/sssp_config.xml"
