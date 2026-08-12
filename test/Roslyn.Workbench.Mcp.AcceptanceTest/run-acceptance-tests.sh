#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/../.." && pwd)"
artifacts_arguments=()

if [[ -r /proc/sys/kernel/osrelease ]] && grep -qi microsoft /proc/sys/kernel/osrelease; then
    artifacts_arguments=(--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp)
fi

cd "$repository_root"

echo "Running published Host acceptance tests..."
dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj \
    --configuration Release \
    "${artifacts_arguments[@]}" \
    "$@"
