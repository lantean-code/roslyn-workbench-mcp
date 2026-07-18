# Published Host Acceptance Tests

These tests launch a published Roslyn Workbench MCP executable through the official MCP C# `StdioClientTransport`. They consume only the executable and its public MCP protocol; the project has no production project references.

The acceptance build also assembles the existing `HostQueryPluginFixture` into `TestAssets/Plugins/HostQuery` using a build-only project reference with `ReferenceOutputAssembly=false`. The acceptance test assembly receives no production or plugin-fixture compile reference. Only the plugin entry assembly, dependency manifest and deliberately private `NuGet.Versioning` dependency are copied into the external package asset.

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH` to the exact executable produced by the configuration being tested. The suite does not guess between Debug and Release output.

## Linux and macOS

Debug:

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj -c Debug
ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH="$PWD/src/Roslyn.Workbench.Mcp/bin/Debug/net10.0/publish/Roslyn.Workbench.Mcp" \
  dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj -c Debug
```

Release uses `-c Release` for both commands and this explicit path:

```text
src/Roslyn.Workbench.Mcp/bin/Release/net10.0/publish/Roslyn.Workbench.Mcp
```

## Windows PowerShell

Debug:

```powershell
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj -c Debug
$env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = Join-Path $PWD 'src\Roslyn.Workbench.Mcp\bin\Debug\net10.0\publish\Roslyn.Workbench.Mcp.exe'
dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj -c Debug
```

Release uses `-c Release` for both commands and this explicit assignment:

```powershell
$env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = Join-Path $PWD 'src\Roslyn.Workbench.Mcp\bin\Release\net10.0\publish\Roslyn.Workbench.Mcp.exe'
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
