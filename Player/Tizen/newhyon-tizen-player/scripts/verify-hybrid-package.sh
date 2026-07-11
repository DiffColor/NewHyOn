#!/usr/bin/env bash

set -euo pipefail

PACKAGE_PATH="${1:-}"

if [[ -z "${PACKAGE_PATH}" || ! -f "${PACKAGE_PATH}" ]]; then
  printf '검증할 하이브리드 WGT를 찾지 못했습니다: %s\n' "${PACKAGE_PATH:-}" >&2
  exit 1
fi

ROOT_CONFIG="$(unzip -p "${PACKAGE_PATH}" config.xml)"
CONTENT_PATH="$(printf '%s' "${ROOT_CONFIG}" | perl -ne 'if (/<content\s+src="([^"]+)"/) { print $1; exit }')"

if [[ "${CONTENT_PATH}" != "res/wgt/index.html" ]]; then
  printf '하이브리드 WGT 시작 경로가 올바르지 않습니다: %s\n' "${CONTENT_PATH:-없음}" >&2
  exit 1
fi

if ! unzip -Z1 "${PACKAGE_PATH}" | rg -Fxq "${CONTENT_PATH}"; then
  printf '하이브리드 WGT 시작 파일이 없습니다: %s\n' "${CONTENT_PATH}" >&2
  exit 1
fi

printf 'Hybrid WGT entry verified: %s\n' "${CONTENT_PATH}"
