# Getting started

## Prerequisites

- A supported Roslyn Workbench executable or a source checkout built with the
  .NET 10 SDK selected by `global.json`.
- An MCP client that can start a local stdio server.
- The .NET SDKs and build tooling required by the solutions or projects you
  intend to load.

## Build from source

From the repository root:

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --configuration Release \
  --output artifacts/publish/Roslyn.Workbench.Mcp/release
```

The published executable is placed beneath
`artifacts/publish/Roslyn.Workbench.Mcp/release`.

## Connect a client

Configure the MCP client to launch the absolute path to the published
`Roslyn.Workbench.Mcp` executable. Client configuration formats differ, but the
equivalent process configuration is:

```json
{
  "command": "/absolute/path/to/Roslyn.Workbench.Mcp",
  "args": [
    "--state-directory",
    "/absolute/path/to/roslyn-workbench-state"
  ]
}
```

The server communicates over standard input and standard output. Protocol data
uses stdout; operational logging uses stderr.

## First workflow

1. Call `server-status` with `detail` set to `Full` and review component status,
   startup fallbacks, recovery state and the published tool count.
2. Call `workspace-open` with the absolute path to a `.sln`, `.slnx` or
   `.csproj`. All loaded C# projects must use the SDK-style project format.
3. Use query tools against the loaded workspace.
4. Before any mutation, read [Workspaces and transactions](WorkspacesAndTransactions.md)
   and check `workspace-status`.

The server starts without a loaded workspace. It can keep multiple workspaces
open, but only one loaded workspace may own the active transaction slot.
