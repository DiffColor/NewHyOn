#!/usr/bin/env sh

set -eu

if ! command -v go >/dev/null 2>&1; then
  echo "Go 1.23 or later is required." >&2
  exit 1
fi

project_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
output_dir="$project_dir/bin"
version="${1:-1.0.0}"

cd "$project_dir"
go test ./...

for target in \
  windows/amd64 windows/arm64 \
  linux/amd64 linux/arm64 \
  macos/amd64 macos/arm64
do
  os=${target%/*}
  arch=${target#*/}
  goos=$os
  target_dir="$output_dir/$os-$arch"
  output_path="$target_dir/NtpServer"

  if [ "$os" = "windows" ]; then
    output_path="${output_path}.exe"
  elif [ "$os" = "macos" ]; then
    goos=darwin
  fi

  mkdir -p "$target_dir"
  echo "Building $os/$arch..."
  CGO_ENABLED=0 GOOS="$goos" GOARCH="$arch" \
    go build -buildvcs=false -trimpath -ldflags "-s -w -X main.version=$version" -o "$output_path" .
done

printf 'Built Windows, Linux, and macOS binaries under %s.\n' "$output_dir"
