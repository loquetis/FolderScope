#!/usr/bin/env bash
# Build FolderScope tests and a self-contained single-file Windows x64 exe.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-"$ROOT/artifacts/win-x64"}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

cd "$ROOT"
rm -rf "$OUT"
mkdir -p "$OUT"

dotnet test "$ROOT/tests/FolderScope.Core.Tests/FolderScope.Core.Tests.csproj" -c Release --nologo

dotnet publish "$ROOT/src/FolderScope.App/FolderScope.App.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -p:IncludeSymbols=false \
  -o "$OUT"

if [[ ! -f "$OUT/FolderScope.exe" ]]; then
  echo "FolderScope.exe was not produced." >&2
  exit 1
fi

extra="$(find "$OUT" -mindepth 1 ! -name 'FolderScope.exe' -print)"
if [[ -n "$extra" ]]; then
  echo "Publish output is not a single file:" >&2
  printf '%s\n' "$extra" >&2
  exit 1
fi

bytes="$(wc -c < "$OUT/FolderScope.exe" | tr -d ' ')"
hash="$(sha256sum "$OUT/FolderScope.exe" | awk '{print $1}')"
echo "FolderScope.exe  ${bytes} bytes  sha256 ${hash}"
echo "$OUT/FolderScope.exe"
