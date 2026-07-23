# Published Host Acceptance Tests

These tests launch a published Roslyn Workbench MCP executable through the official MCP C# `StdioClientTransport`. They consume only the executable and its public MCP protocol; the project has no production project references.

The acceptance build also assembles deterministic external query and mutation fixture packages into `TestAssets/Plugins` using build-only project references with `ReferenceOutputAssembly=false`. The acceptance test assembly receives no production or plugin-fixture compile reference. The query package includes its entry assembly, dependency manifest and deliberately private `NuGet.Versioning` dependency. The mutation package includes its entry assembly and dependency manifest. Both packages can publish a file-based readiness signal and await an explicit release signal, which lets protocol cancellation and concurrency cases coordinate without sleeps.

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH` to the absolute path of the exact Release-published executable being tested. The suite does not search build output or infer a configuration.

## Linux and macOS

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --configuration Release \
  --output /tmp/roslyn-workbench-mcp-acceptance-publish
ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH=/tmp/roslyn-workbench-mcp-acceptance-publish/Roslyn.Workbench.Mcp \
  dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj \
  --configuration Release
```

## Windows PowerShell

```powershell
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj `
  --configuration Release `
  --output (Join-Path $env:TEMP 'roslyn-workbench-mcp-acceptance-publish')
$env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = Join-Path $env:TEMP 'roslyn-workbench-mcp-acceptance-publish\Roslyn.Workbench.Mcp.exe'
dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj `
  --configuration Release
```

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_RETAIN_ROOT=true` while diagnosing a failure to retain a failed scenario root. Without it, scenario workspaces and state are removed during asynchronous fixture disposal.
Retained failure roots include `process.txt` and `server.stderr.log` alongside the scenario workspace and state.

## Published response envelope

Every successful tool response uses the same outer structured-content shape:

```json
{
  "ok": true,
  "data": {}
}
```

The object inside `data` remains specific to lifecycle, query or mutation tools. Failed tool responses use `ok: false`, `error` and optional `next` instead.
