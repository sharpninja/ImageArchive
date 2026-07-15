#!/usr/bin/env bash
# Unix counterpart of build.ps1. Windows uses the Archive file attribute; here we compare
# mtimes of build sources against the host binary.
set -eo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
BUILD_DIR="$SCRIPT_DIR/build"
BUILD_PROJECT="$BUILD_DIR/_build.csproj"
HOST_CONFIG="${NUKE_BUILD_CONFIGURATION:-Debug}"
HOST_DLL="$BUILD_DIR/bin/$HOST_CONFIG/_build.dll"

need_build=0
if [[ ! -f "$HOST_DLL" ]]; then
  need_build=1
else
  # Rebuild if any non-bin/obj source under build/ is newer than the host binary.
  while IFS= read -r -d '' f; do
    if [[ "$f" -nt "$HOST_DLL" ]]; then
      need_build=1
      break
    fi
  done < <(find "$BUILD_DIR" \( -path '*/bin/*' -o -path '*/obj/*' \) -prune -o \
    -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.props' -o -name '*.targets' -o -name '*.json' \) -print0 2>/dev/null)
fi

if [[ "$need_build" -eq 1 ]]; then
  echo "Nuke host: building ($HOST_CONFIG) — sources newer than binary or binary missing."
  dotnet build "$BUILD_PROJECT" -c "$HOST_CONFIG" --nologo
else
  echo "Nuke host: up-to-date; using $HOST_DLL"
fi

dotnet exec "$HOST_DLL" -- "$@"
