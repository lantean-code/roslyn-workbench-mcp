#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/../.." && pwd)"
publish_root="$repository_root/artifacts/performance/publish/$(date -u +%Y%m%d-%H%M%S)-$$"
host_output="$publish_root/host"
runner_output="$publish_root/runner"
artifacts_arguments=()

if [[ -r /proc/sys/kernel/osrelease ]] && grep -qi microsoft /proc/sys/kernel/osrelease; then
    artifacts_arguments=(--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp)
fi

if (( $# == 0 )); then
    set -- list
fi

cd "$repository_root"
mkdir -p "$publish_root"

echo "Restoring pinned diagnostic tools..."
dotnet tool restore

echo "Publishing Roslyn Workbench Host (Release)..."
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
    --configuration Release \
    --output "$host_output" \
    "${artifacts_arguments[@]}"

echo "Publishing performance runner (Release)..."
dotnet publish tools/Roslyn.Workbench.Mcp.Performance/Roslyn.Workbench.Mcp.Performance.csproj \
    --configuration Release \
    --output "$runner_output" \
    "${artifacts_arguments[@]}"

host_path="$host_output/Roslyn.Workbench.Mcp"
if [[ ! -x "$host_path" ]]; then
    host_path="$host_output/Roslyn.Workbench.Mcp.dll"
fi

runner_path="$runner_output/Roslyn.Workbench.Mcp.Performance"
echo "Published binaries: $publish_root"

if [[ -x "$runner_path" ]]; then
    exec "$runner_path" "$@" --host "$host_path" --framework-root "$repository_root"
fi

exec dotnet "$runner_output/Roslyn.Workbench.Mcp.Performance.dll" \
    "$@" \
    --host "$host_path" \
    --framework-root "$repository_root"
