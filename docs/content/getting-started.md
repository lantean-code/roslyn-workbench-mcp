# Getting started with Roslyn Workbench

Connect your AI coding agent to Roslyn's C# fixes, refactorings and semantic navigation. Once connected, ask it to discover applicable Code Actions, extract a method, encapsulate a field or investigate a diagnostic using the compiler platform behind Visual Studio's C# tooling.

Start with inspection-only to explore a trusted solution without enabling source changes. When you are ready to apply fixes or refactorings, choose a [mutation mode](configuration.md) and use the [Code Action workflow](code-actions.md) to stage and review changes before committing them. See the Code Action guide for supported operations and exclusions; not every Visual Studio action is available.

## Prerequisites

- A supported .NET 10 SDK.
- An MCP client that can start a local stdio server.
- The .NET SDKs and build tooling required by the solutions or projects you intend to load.

## Platform support

Windows x64, Linux x64 and WSL2 x64 are supported. macOS x64 and ARM64 are available on a best-effort basis until hosted validation is in place. Windows ARM64 and Linux ARM64 are not currently supported release targets.

## Run with dnx

`dnx` acquires and runs the selected .NET tool package without a permanent global installation. Pin an exact release in MCP client configuration so package acquisition and upgrades remain deliberate:

```bash
dnx Lantean.Roslyn.Workbench.Mcp@VERSION --yes -- --version
```

The `--yes` option permits non-interactive acquisition. Arguments after `--` belong to Roslyn Workbench rather than `dnx`. A typical client configuration is:

```json
{
  "command": "dnx",
  "args": [
    "Lantean.Roslyn.Workbench.Mcp@VERSION",
    "--yes",
    "--",
    "--operational-mode", "inspection-only",
    "--state-directory", "/absolute/path/to/roslyn-workbench-state"
  ]
}
```

## Install the global .NET tool

For a persistent command, install the same package globally and verify it:

```bash
dotnet tool install --global Lantean.Roslyn.Workbench.Mcp --version VERSION
roslyn-workbench-mcp --version
```

## Connect a client

When using the global tool, configure the MCP client to launch the installed `roslyn-workbench-mcp` command. Client configuration formats differ, but the equivalent process configuration is:

```json
{
  "command": "roslyn-workbench-mcp",
  "args": [
    "--operational-mode", "inspection-only",
    "--state-directory", "/absolute/path/to/roslyn-workbench-state"
  ]
}
```

The server communicates over stdin/stdout; operational logs use stderr. Inspection-only is the default. Use the correct absolute state-directory path for your platform; see [State storage](configuration.md#state-storage-and-recovery).

## Trust the workspace before opening it

Open only a fully trusted workspace. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. Allowed Workspace roots reduce accidental agent scope; they do not sandbox this code or constrain build-tool inputs. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox before opening it.

## First workflow

1. Call `server-status` with `detail: Full` and review startup warnings, recovery state and component availability.
2. Open a fully trusted `.sln`, `.slnx` or `.csproj` with `workspace-open`.
3. Ask your agent to inspect diagnostics, find references or discover Code Actions.
4. When you want source changes, choose a [mutation mode](configuration.md#operational-modes) and follow the [transaction workflow](workspaces-and-transactions.md).

For example, ask: “Use Roslyn Workbench to open this trusted solution, inspect its compiler diagnostics, and list applicable Code Fixes without changing any files.”

The server starts without a loaded workspace. It can keep multiple workspaces open, but only one loaded workspace may own the active transaction slot.


## Next steps

- [Agent setup](agent-setup.md): encourage your client to choose compiler-aware tools.
- [Workspace loading](workspaces.md): unsupported projects, build properties, external documents and reload.
- [Configuration](configuration.md): operational modes, allowed roots and optional plugin loading.
- [Code Actions](code-actions.md): discover, stage and review fixes and refactorings.

## Update or remove the global tool

Finish or roll back active transactions, stop the Host and update to an exact version:

```bash
dotnet tool update --global Lantean.Roslyn.Workbench.Mcp --version VERSION
roslyn-workbench-mcp --version
```

For `dnx`, change the exact `@VERSION` in the client's configuration and restart the Host. Review the selected release notes before upgrading, and retain unresolved recovery evidence; see [Compatibility](compatibility.md#durable-recovery-state).

To remove a global installation, remove the client's server entry, stop the Host and run `dotnet tool uninstall --global Lantean.Roslyn.Workbench.Mcp`. Follow [Troubleshooting and removal](troubleshooting.md) before deleting any state directory.

## Build from source

See [Building from source](building-from-source.md) for the publish command and source-build client configuration.
