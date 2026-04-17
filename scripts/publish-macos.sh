#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/src/XboxOneStorageConverter.Cli/XboxOneStorageConverter.Cli.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
TARGET_FRAMEWORK="net10.0"

if [[ -n "${RID:-}" ]]; then
  TARGET_RID="$RID"
else
  case "$(uname -m)" in
    arm64|aarch64)
      TARGET_RID="osx-arm64"
      ;;
    x86_64|amd64)
      TARGET_RID="osx-x64"
      ;;
    *)
      echo "Unsupported macOS architecture: $(uname -m)" >&2
      exit 1
      ;;
  esac
fi

dotnet publish "$PROJECT_PATH" \
  -c "$CONFIGURATION" \
  -r "$TARGET_RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  "$@"

OUTPUT_PATH="$ROOT_DIR/src/XboxOneStorageConverter.Cli/bin/$CONFIGURATION/$TARGET_FRAMEWORK/$TARGET_RID/publish/xbox-storage-converter"

echo
echo "Published executable:"
echo "  $OUTPUT_PATH"
