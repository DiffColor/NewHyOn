#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd "${PROJECT_ROOT}/../../.." && pwd)"
FTP_SERVICE_ROOT="${REPO_ROOT}/Player/Tizen/newhyon-ftp-downloader-service/NewHyOnFtpDownloader"
FTP_SERVICE_PROJECT="${FTP_SERVICE_ROOT}/NewHyOnFtpDownloader.csproj"
WEB_WGT="${PROJECT_ROOT}/Build/NewHyOnTizenPlayer.wgt"
HYBRID_WEB_WGT="${PROJECT_ROOT}/Build/NewHyOnTizenPlayer.hybrid-source.wgt"
HYBRID_WGT="${PROJECT_ROOT}/Build/NewHyOnTizenPlayer.hybrid.wgt"
PUBLISH_DIR="${PROJECT_ROOT}/publish/SSSP"
PUBLISH_WGT="${PUBLISH_DIR}/NewHyOnTizenPlayer.wgt"
TPK_NAME="NewHyOnFtpD01-1.0.0"
TPK_OUTPUT_DIR="${FTP_SERVICE_ROOT}/bin/Release/tizen50"
TPK_DEFAULT="${TPK_OUTPUT_DIR}/${TPK_NAME}.tpk"
TPK_PARTNER_DIR="${TPK_OUTPUT_DIR}/partner"
TPK_PARTNER="${TPK_PARTNER_DIR}/${TPK_NAME}.partner.tpk"
PUBLISH_TPK="${PUBLISH_DIR}/${TPK_NAME}.partner.tpk"

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

command -v dotnet >/dev/null 2>&1 || {
  printf 'dotnet CLI를 찾지 못했습니다.\n' >&2
  exit 1
}

cd "${PROJECT_ROOT}"

TIZEN_CLI="${TIZEN_CLI_PATH}" NEWHYON_TIZEN_PROFILE="${TIZEN_PROFILE}" npm run package:wgt
TIZEN_CLI="${TIZEN_CLI_PATH}" \
NEWHYON_TIZEN_PROFILE="${TIZEN_PROFILE}" \
NEWHYON_TIZEN_CONTENT_PATH="res/wgt/index.html" \
NEWHYON_TIZEN_OUTPUT_NAME="$(basename "${HYBRID_WEB_WGT}")" \
npm run package:wgt

dotnet build "${FTP_SERVICE_PROJECT}" -c Release -v:minimal

[[ -f "${TPK_DEFAULT}" ]] || {
  printf 'TPK 빌드 산출물을 찾지 못했습니다: %s\n' "${TPK_DEFAULT}" >&2
  exit 1
}

mkdir -p "${TPK_PARTNER_DIR}"
"${TIZEN_CLI_PATH}" pack \
  -t tpk \
  -s "${TIZEN_PROFILE}" \
  -b "${TPK_DEFAULT}" \
  -o "${TPK_PARTNER}"

"${TIZEN_CLI_PATH}" pack \
  -t wgt \
  -s "${TIZEN_PROFILE}" \
  -b "${HYBRID_WEB_WGT}" \
  -k "${TPK_PARTNER}" \
  -o "${HYBRID_WGT}"

bash "${SCRIPT_DIR}/verify-hybrid-package.sh" "${HYBRID_WGT}"

mkdir -p "${PUBLISH_DIR}"
cp "${HYBRID_WGT}" "${PUBLISH_WGT}"
cp "${TPK_PARTNER}" "${PUBLISH_TPK}"

WGT_SIZE_KB="$(du -k "${PUBLISH_WGT}" | awk '{print $1}')"
WIDGET_VERSION="$(unzip -p "${PUBLISH_WGT}" config.xml | perl -ne 'if (/<widget\b[^>]*\bversion="([0-9]+\.[0-9]+\.[0-9]+)"/) { print $1; exit }')"

[[ -n "${WIDGET_VERSION}" ]] || {
  printf 'WGT config.xml에서 앱 버전을 읽지 못했습니다.\n' >&2
  exit 1
}

cat > "${PUBLISH_DIR}/sssp_config.xml" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<widget>
    <ver>${WIDGET_VERSION}</ver>
    <size>${WGT_SIZE_KB}</size>
    <widgetname>NewHyOnTizenPlayer</widgetname>
    <webtype>tizen</webtype>
</widget>
EOF

printf 'USB publish created: %s\n' "${PUBLISH_DIR}"
printf 'Hybrid WGT: %s\n' "${PUBLISH_WGT}"
printf 'FTP service TPK: %s\n' "${PUBLISH_TPK}"
