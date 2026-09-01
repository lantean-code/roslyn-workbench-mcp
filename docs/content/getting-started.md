# Getting started with Roslyn Workbench

## Prerequisites

- A supported .NET 10 SDK.
- An MCP client that can start a local stdio server.
- The .NET SDKs and build tooling required by the solutions or projects you intend to load.

## Platform support

Windows x64, Linux x64 and WSL2 x64 are supported. macOS x64 and ARM64 are available on a best-effort basis until hosted validation is in place. Windows ARM64 and Linux ARM64 are not currently supported release targets.

## Install the .NET tool

Install Roslyn Workbench from NuGet.org as a global .NET tool:

```bash
dotnet tool install --global Roslyn.Workbench.Mcp
```

Verify the installed command:

```bash
roslyn-workbench-mcp --version
```

## Build from source

From the repository root:

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --configuration Release \
  --output artifacts/publish/Roslyn.Workbench.Mcp/release
```

The published executable is placed beneath `artifacts/publish/Roslyn.Workbench.Mcp/release`.

## Connect a client

Configure the MCP client to launch the installed `roslyn-workbench-mcp` command. Client configuration formats differ, but the equivalent process configuration is:

```json
{
  "command": "roslyn-workbench-mcp",
  "args": ["--state-directory", "/absolute/path/to/roslyn-workbench-state"]
}
```

The server communicates over standard input and standard output. Protocol data uses stdout; operational logging uses stderr.

For a source build, use the absolute path to the published `Roslyn.Workbench.Mcp` executable instead of the installed command.

## Trust the workspace before opening it

Open only a fully trusted workspace. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. Roslyn Workbench does not sandbox this code. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox before opening it.

## First workflow

1. Call `server-status` with `detail` set to `Full` and review component status, startup fallbacks, recovery state and the published tool count.
2. After establishing that the workspace and its build inputs are fully trusted, call `workspace-open` with the absolute path to a `.sln`, `.slnx` or `.csproj`. If the project requires caller-specific MSBuild configuration, include the optional allowlisted `msBuildProperties`; use `artifactsPath` only when the build itself requires a non-default artifacts location. Standard SDK, NuGet and Visual Studio locations are discovered through normal MSBuild evaluation. Evaluated documents outside the workspace root are queryable but read-only. A solution may contain unsupported languages or non-SDK-style projects; they are skipped with load diagnostics. At least one supported SDK-style C# project must remain.
3. Use query tools against the loaded workspace.
4. Before any mutation, read [Workspaces and transactions](workspaces-and-transactions.md) and check `workspace-status`.

The server starts without a loaded workspace. It can keep multiple workspaces open, but only one loaded workspace may own the active transaction slot.
