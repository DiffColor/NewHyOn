#!/usr/bin/env bash

resolve_app_version() {
  local repo_root="$1"
  local tag="${APP_VERSION:-}"

  if [[ -z "$tag" ]]; then
    tag="$(git -C "$repo_root" describe --tags --exact-match 2>/dev/null || true)"
  fi

  if [[ ! "$tag" =~ ^([A-Za-z0-9._-]+-)?v([0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?)$ ]]; then
    printf '지원하지 않는 앱 버전 태그입니다: %s\n' "${tag:-<empty>}" >&2
    printf 'APP_VERSION 또는 현재 Git 태그를 v<major>.<minor>.<build>[.<revision>] 형식으로 지정하십시오.\n' >&2
    return 1
  fi

  APP_VERSION_TAG="$tag"
  APP_VERSION_NUMERIC="${BASH_REMATCH[2]}"
  export APP_VERSION="$APP_VERSION_TAG"
  export APP_VERSION_TAG APP_VERSION_NUMERIC
}
