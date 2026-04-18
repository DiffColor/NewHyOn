#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILES=()
while IFS= read -r project_file; do
  PROJECT_FILES+=("$project_file")
done < <(find "$SCRIPT_DIR" -maxdepth 1 -type f -name '*.csproj' | sort)

if [ "${#PROJECT_FILES[@]}" -eq 0 ]; then
  echo "[publish] project file not found in script directory: $SCRIPT_DIR" >&2
  exit 1
fi

if [ "${#PROJECT_FILES[@]}" -gt 1 ]; then
  echo "[publish] multiple project files found in script directory: $SCRIPT_DIR" >&2
  exit 1
fi

PROJECT_PATH="${PROJECT_FILES[0]}"
CONFIGURATION="${1:-Release}"
OUTPUT_DIR="$SCRIPT_DIR/bin/publish"

echo "[publish] project: $PROJECT_PATH"
echo "[publish] configuration: $CONFIGURATION"
echo "[publish] publish dir: $OUTPUT_DIR"

dotnet publish "$PROJECT_PATH" -c "$CONFIGURATION" -p:PublishDir="$OUTPUT_DIR"

echo
echo "[publish] output:"
echo "$OUTPUT_DIR"
echo
ls -lh "$OUTPUT_DIR"
