#!/usr/bin/env bash

set -euo pipefail

PACKAGE_PATH="${1:-}"

if [[ -z "${PACKAGE_PATH}" || ! -f "${PACKAGE_PATH}" ]]; then
  printf '검증할 SSSP WGT를 찾지 못했습니다: %s\n' "${PACKAGE_PATH:-}" >&2
  exit 1
fi

ROOT_CONFIG="$(unzip -p "${PACKAGE_PATH}" config.xml)"
CONTENT_PATH="$(printf '%s' "${ROOT_CONFIG}" | perl -ne 'if (/<content\s+src="([^"]+)"/) { print $1; exit }')"

if [[ "${CONTENT_PATH}" != "index.html" ]]; then
  printf 'SSSP WGT 시작 경로가 올바르지 않습니다: %s\n' "${CONTENT_PATH:-없음}" >&2
  exit 1
fi

PACKAGE_FILES="$(unzip -Z1 "${PACKAGE_PATH}")"
if ! printf '%s\n' "${PACKAGE_FILES}" | rg -Fxq "${CONTENT_PATH}"; then
  printf 'SSSP WGT 시작 파일이 없습니다: %s\n' "${CONTENT_PATH}" >&2
  exit 1
fi

if printf '%s\n' "${PACKAGE_FILES}" | rg -q '\.(tpk|rpm)$|^res/(tpk|wgt)/'; then
  printf 'SSSP WGT에 지원되지 않는 하이브리드 패키지 파일이 포함되어 있습니다.\n' >&2
  exit 1
fi

printf 'SSSP WGT entry verified: %s\n' "${CONTENT_PATH}"
