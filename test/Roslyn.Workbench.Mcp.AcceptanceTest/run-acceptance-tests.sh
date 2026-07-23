#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/../.." && pwd)"
temporary_root="${TMPDIR:-/tmp}/roslyn-workbench-mcp/acceptance"
publish_parent="$temporary_root/publish"
mkdir -p "$publish_parent"
publish_root="$(mktemp -d "$publish_parent/$(date -u +%Y%m%d-%H%M%S)-XXXXXX")"
host_output="$publish_root/host"
artifacts_arguments=()

cleanup() {
    if [[ -n "${publish_root:-}" && -d "$publish_root" ]]; then
        rm -rf -- "$publish_root"
    fi
}

trap cleanup EXIT

if [[ -r /proc/sys/kernel/osrelease ]] && grep -qi microsoft /proc/sys/kernel/osrelease; then
    artifacts_arguments=(--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp)
fi

cd "$repository_root"

echo "Publishing Roslyn Workbench Host (Release)..."
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
    --configuration Release \
    --output "$host_output" \
    "${artifacts_arguments[@]}"

host_path="$host_output/Roslyn.Workbench.Mcp"
if [[ ! -x "$host_path" ]]; then
    echo "The published Host was not found at '$host_path'." >&2
    exit 1
fi

echo "Temporary published binaries: $publish_root"
echo "Running published Host acceptance tests..."
ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH="$host_path" \
    dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj \
    --configuration Release \
    "${artifacts_arguments[@]}" \
    "$@"
