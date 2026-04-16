#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/NewHyOn.Settings.Wpf.csproj"
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
