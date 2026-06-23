#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DIST_DIR="${PROJECT_ROOT}/dist"
OUTPUT_DIR="${PROJECT_ROOT}/Build"
OUTPUT_NAME="NewHyOnTizenPlayer.wgt"
TMP_DIR="$(mktemp -d)"
STAGE_DIR="${TMP_DIR}/NewHyOnTizenPlayer"

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
  local profile="${NEWHYON_TIZEN_PROFILE:-${SAMPLE_NEWHYON_TIZEN_PROFILE:-${PLAYER_TIZEN_PROFILE:-${TIZEN_PROFILE_NAME:-}}}}"
  if [[ -z "${profile}" ]]; then
    printf 'Tizen 서명 profile이 필요합니다. NEWHYON_TIZEN_PROFILE을 지정하십시오.\n' >&2
    exit 1
  fi

  printf '%s' "${profile}"
}

cleanup() {
  rm -rf "${TMP_DIR}"
}

trap cleanup EXIT

mkdir -p "${STAGE_DIR}" "${OUTPUT_DIR}"

TIZEN_CLI_PATH="$(resolve_tizen_cli)"
TIZEN_PROFILE="$(resolve_tizen_profile)"

if [[ ! -f "${DIST_DIR}/tizen_web_project.yaml" ]]; then
  printf 'dist/tizen_web_project.yaml 파일이 없습니다. 먼저 빌드를 수행하십시오.\n' >&2
  exit 1
fi

rsync -a \
  --exclude '*.wgt' \
  --exclude '.manifest.tmp' \
  --exclude 'author-signature.xml' \
  --exclude 'signature*.xml' \
  "${DIST_DIR}/" "${STAGE_DIR}/"

if [[ "$(basename "${TIZEN_CLI_PATH}")" == "tz" ]]; then
  "${TIZEN_CLI_PATH}" pack -w "${STAGE_DIR}" -t wgt -s "${TIZEN_PROFILE}"
else
  "${TIZEN_CLI_PATH}" package -t wgt -s "${TIZEN_PROFILE}" -- "${STAGE_DIR}"
fi

PACKAGED_WGT="$(find "${STAGE_DIR}" -maxdepth 1 -type f -name '*.wgt' | sort | tail -n 1)"
[[ -n "${PACKAGED_WGT}" ]] || {
  printf 'stage 디렉터리에서 생성된 .wgt 파일을 찾지 못했습니다.\n' >&2
  exit 1
}

cp "${PACKAGED_WGT}" "${OUTPUT_DIR}/${OUTPUT_NAME}"

printf 'Clean package created: %s\n' "${OUTPUT_DIR}/${OUTPUT_NAME}"
