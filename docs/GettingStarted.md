# Getting started

## Prerequisites

- A supported Roslyn Workbench executable or a source checkout built with the .NET 10 SDK selected by `global.json`.
- An MCP client that can start a local stdio server.
- The .NET SDKs and build tooling required by the solutions or projects you intend to load.

## Platform support

Windows and Linux are the supported release platforms. macOS is implemented on a best-effort basis using native advisory locking and the shared Unix atomic-write path, but it has not yet been validated on macOS hardware or a hosted macOS runner. It is not a pull-request gate or authoritative performance baseline.

## Build from source

From the repository root:

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --configuration Release \
  --output artifacts/publish/Roslyn.Workbench.Mcp/release
```

The published executable is placed beneath `artifacts/publish/Roslyn.Workbench.Mcp/release`.

Without additional build input, approved external reports use the stderr logging dispatcher. To produce an application-owned Sentry build, set `ROSLYN_WORKBENCH_SENTRY_DSN` while compiling or publishing; the DSN is embedded into the executable and is not read from the runtime environment. See [Configuration](Configuration.md#build-time-error-report-provider).

## Connect a client

Configure the MCP client to launch the absolute path to the published `Roslyn.Workbench.Mcp` executable. Client configuration formats differ, but the equivalent process configuration is:

```json
{
  "command": "/absolute/path/to/Roslyn.Workbench.Mcp",
  "args": ["--state-directory", "/absolute/path/to/roslyn-workbench-state"]
}
```

The server communicates over standard input and standard output. Protocol data uses stdout; operational logging uses stderr.

## Trust the workspace before opening it

Open only a fully trusted workspace. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. Roslyn Workbench does not sandbox this code. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox before opening it.

## First workflow

1. Call `server-status` with `detail` set to `Full` and review component status, startup fallbacks, recovery state and the published tool count.
2. After establishing that the workspace and its build inputs are fully trusted, call `workspace-open` with the absolute path to a `.sln`, `.slnx` or `.csproj`. If the project requires caller-specific MSBuild configuration, include the optional allowlisted `msBuildProperties`; use `artifactsPath` only when the build itself requires a non-default artifacts location. Standard SDK, NuGet and Visual Studio locations are discovered through normal MSBuild evaluation. Evaluated documents outside the workspace root are queryable but read-only. A solution may contain unsupported languages or non-SDK-style projects; they are skipped with load diagnostics. At least one supported SDK-style C# project must remain.
3. Use query tools against the loaded workspace.
4. Before any mutation, read [Workspaces and transactions](WorkspacesAndTransactions.md) and check `workspace-status`.

The server starts without a loaded workspace. It can keep multiple workspaces open, but only one loaded workspace may own the active transaction slot.
