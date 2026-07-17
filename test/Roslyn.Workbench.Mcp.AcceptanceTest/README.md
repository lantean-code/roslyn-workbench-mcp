# Published Host Acceptance Tests

These tests launch a published Roslyn Workbench MCP executable through the official MCP C# `StdioClientTransport`. They consume only the executable and its public MCP protocol; the project has no production project references.

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH` to the exact executable produced by the configuration being tested. The suite does not guess between Debug and Release output.

## Linux and macOS

Debug:

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj -c Debug --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH=/tmp/artifacts/roslyn-workbench-mcp/publish/Roslyn.Workbench.Mcp/debug/Roslyn.Workbench.Mcp \
  dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj -c Debug --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

Release uses `-c Release` for both commands and this explicit path:

```text
/tmp/artifacts/roslyn-workbench-mcp/publish/Roslyn.Workbench.Mcp/release/Roslyn.Workbench.Mcp
```

## Windows PowerShell

Debug:

```powershell
$artifactsPath = Join-Path ([System.IO.Path]::GetTempPath()) 'artifacts\roslyn-workbench-mcp'
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj -c Debug --artifacts-path=$artifactsPath
$env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = Join-Path $artifactsPath 'publish\Roslyn.Workbench.Mcp\debug\Roslyn.Workbench.Mcp.exe'
dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj -c Debug --artifacts-path=$artifactsPath
```

Release uses `-c Release` for both commands and this explicit assignment:

```powershell
$env:ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH = Join-Path $artifactsPath 'publish\Roslyn.Workbench.Mcp\release\Roslyn.Workbench.Mcp.exe'
```

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_RETAIN_ROOT=true` while diagnosing a failure to retain a failed scenario root. Without it, scenario workspaces and state are removed during asynchronous fixture disposal.
