#!/usr/bin/env bash

set -euo pipefail

PACKAGE_PATH="${1:-}"

if [[ -z "${PACKAGE_PATH}" || ! -f "${PACKAGE_PATH}" ]]; then
  printf '검증할 하이브리드 WGT를 찾지 못했습니다: %s\n' "${PACKAGE_PATH:-}" >&2
  exit 1
fi

ROOT_CONFIG="$(unzip -p "${PACKAGE_PATH}" config.xml)"
CONTENT_PATH="$(printf '%s' "${ROOT_CONFIG}" | perl -ne 'if (/<content\s+src="([^"]+)"/) { print $1; exit }')"
ICON_PATH="$(printf '%s' "${ROOT_CONFIG}" | perl -ne 'if (/<icon\s+src="([^"]+)"/) { print $1; exit }')"

if [[ "${CONTENT_PATH}" != "res/wgt/index.html" ]]; then
  printf '하이브리드 WGT 시작 경로가 올바르지 않습니다: %s\n' "${CONTENT_PATH:-없음}" >&2
  exit 1
fi

if ! unzip -Z1 "${PACKAGE_PATH}" | rg -Fxq "${CONTENT_PATH}"; then
  printf '하이브리드 WGT 시작 파일이 없습니다: %s\n' "${CONTENT_PATH}" >&2
  exit 1
fi

if [[ "${ICON_PATH}" != "newhyon-app-icon.png" ]]; then
  printf '하이브리드 WGT 런처 아이콘 경로가 올바르지 않습니다: %s\n' "${ICON_PATH:-없음}" >&2
  exit 1
fi

if ! unzip -Z1 "${PACKAGE_PATH}" | rg -Fxq "${ICON_PATH}"; then
  printf '하이브리드 WGT 아이콘 파일이 없습니다: %s\n' "${ICON_PATH:-없음}" >&2
  exit 1
fi

if ! unzip -p "${PACKAGE_PATH}" res/wgt/tizen_web_project.yaml | rg -Fxq 'build_type: Release'; then
  printf '하이브리드 WGT가 Release 빌드가 아닙니다.\n' >&2
  exit 1
fi

printf 'Hybrid WGT verified: build=Release entry=%s icon=%s\n' "${CONTENT_PATH}" "${ICON_PATH}"
