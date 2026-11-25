#!/bin/sh
# Reuse the source-only Roslyn inventory with the installed SDK; no downloads.
set -eu
TOOL=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO=$(CDPATH= cd -- "$TOOL/../.." && pwd)
DOTNET=${1:-dotnet}
mkdir -p "$TOOL/.build/home" "$TOOL/.build/tmp"
export DOTNET_CLI_HOME="$TOOL/.build/home" TMPDIR="$TOOL/.build/tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
"$DOTNET" build "$TOOL/extractor/Extractor.csproj" --configfile "$TOOL/extractor/NuGet.Config" --artifacts-path "$TOOL/.build/artifacts" -p:NuGetAudit=false -p:UseSharedCompilation=false --nologo --tl:off
python3 "$TOOL/test_extractor.py" "$DOTNET" "$TOOL/.build/artifacts/bin/Extractor/debug/Extractor.dll"
"$DOTNET" "$TOOL/.build/artifacts/bin/Extractor/debug/Extractor.dll" "$REPO/compiler/Common" "$TOOL/.build/inventory.json"
python3 "$TOOL/test_revisions.py" "$DOTNET"
python3 "$TOOL/revisions.py" "$DOTNET" --publish
