#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/../.." && pwd)"
temporary_root="${TMPDIR:-/tmp}/roslyn-workbench-mcp/performance"
publish_parent="$temporary_root/publish"
mkdir -p "$publish_parent"
publish_root="$(mktemp -d "$publish_parent/$(date -u +%Y%m%d-%H%M%S)-XXXXXX")"
host_output="$publish_root/host"
runner_output="$publish_root/runner"
plugin_output="$publish_root/plugins/host-query"
plugin_root="$publish_root/plugins"
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

if (( $# == 0 )); then
    set -- list
fi

cd "$repository_root"

echo "Restoring pinned diagnostic tools..."
dotnet tool restore

echo "Publishing Roslyn Workbench Host (Release)..."
ROSLYN_WORKBENCH_SENTRY_DSN= dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
    --configuration Release \
    --output "$host_output" \
    "${artifacts_arguments[@]}"

echo "Publishing scenario runner (Release)..."
dotnet publish tools/Roslyn.Workbench.Mcp.ScenarioRunner/Roslyn.Workbench.Mcp.ScenarioRunner.csproj \
    --configuration Release \
    --output "$runner_output" \
    "${artifacts_arguments[@]}"

echo "Publishing cache-calibration plugin (Release)..."
dotnet publish test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostQueryPluginFixture/Roslyn.Workbench.Mcp.HostQueryPluginFixture.csproj \
    --configuration Release \
    --output "$plugin_output" \
    "${artifacts_arguments[@]}"

host_path="$host_output/Roslyn.Workbench.Mcp"
if [[ ! -x "$host_path" ]]; then
    host_path="$host_output/Roslyn.Workbench.Mcp.dll"
fi

runner_path="$runner_output/Roslyn.Workbench.Mcp.ScenarioRunner"
echo "Temporary published binaries: $publish_root"

runner_arguments=("$@")
command_name="${runner_arguments[0],,}"
case "$command_name" in
    list|prepare|help|--help|-h)
        ;;
    *)
        runner_arguments+=(--host "$host_path" --framework-root "$repository_root" --plugin-directory "$plugin_root")
        ;;
esac

if [[ -x "$runner_path" ]]; then
    "$runner_path" "${runner_arguments[@]}"
    exit $?
fi

dotnet "$runner_output/Roslyn.Workbench.Mcp.ScenarioRunner.dll" "${runner_arguments[@]}"
