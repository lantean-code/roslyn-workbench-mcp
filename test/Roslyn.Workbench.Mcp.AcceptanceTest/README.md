# Published Host Acceptance Tests

These tests launch a published Roslyn Workbench MCP executable through the official MCP C# `StdioClientTransport`. They consume only the executable and its public MCP protocol; the project has no production project references.

The acceptance build also assembles deterministic external query and mutation fixture packages into `TestAssets/Plugins` using build-only project references with `ReferenceOutputAssembly=false`. The acceptance test assembly receives no production or plugin-fixture compile reference. The query package includes its entry assembly, dependency manifest and deliberately private `NuGet.Versioning` dependency. The mutation package includes its entry assembly and dependency manifest. Both packages can publish a file-based readiness signal and await an explicit release signal, which lets protocol cancellation and concurrency cases coordinate without sleeps.

An assembly fixture publishes the Release Host to a unique temporary directory before the first acceptance test, sets `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH` for the test process and removes the published files after the assembly finishes. This makes the suite directly runnable from Visual Studio Test Explorer and `dotnet test` without a separate publish step.

## Run the suite

Run the project directly or use the platform wrappers. The wrappers preserve platform-specific command handling and forward additional arguments, while the assembly fixture owns publishing and cleanup:

```bash
dotnet test test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj --configuration Release
```

### Linux and macOS

```bash
./test/Roslyn.Workbench.Mcp.AcceptanceTest/run-acceptance-tests.sh
```

### Windows PowerShell

```powershell
.\test\Roslyn.Workbench.Mcp.AcceptanceTest\run-acceptance-tests.ps1
```

Additional arguments are passed to `dotnet test`. For example, append `--filter FullyQualifiedName~WorkspaceLifecycleAcceptanceTests` to run a subset.

## Run against an existing publish

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH` to an absolute executable path when the published Host must be retained or was produced separately. The assembly fixture validates and uses an explicitly configured executable without publishing or deleting it.

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

Set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_RETAIN_ROOT=true` while diagnosing a failure to retain a failed scenario root. Without it, scenario workspaces and state are removed during asynchronous fixture disposal. Retained failure roots include `process.txt` and `server.stderr.log` alongside the scenario workspace and state.

The assembly fixture clears `ROSLYN_WORKBENCH_SENTRY_DSN` for the duration of the acceptance assembly so published-process coverage deterministically exercises the stderr logging fallback and cannot contact Sentry. It restores the previous value during disposal.

## Published response envelope

Every successful tool response uses the same outer structured-content shape:

```json
{
  "ok": true,
  "data": {}
}
```

The object inside `data` remains specific to lifecycle, query or mutation tools. Failed tool responses use `ok: false`, `error` and an optional structured `continuation` instead.
