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
dotnet tool install --global Lantean.Roslyn.Workbench.Mcp
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
  "args": [
    "--operational-mode", "inspection-only",
    "--state-directory", "/absolute/path/to/roslyn-workbench-state"
  ]
}
```

`inspection-only` is also the default when the option is omitted. It publishes semantic query and Workspace lifecycle tools without publishing or authorising source mutation. Select `transactional` when the client supports MCP elicitation and commit confirmation should cover one commit or the Host session. Select `approval-required` when every approval must be bound to the exact change returned by `transaction-review`, or deliberately select `autonomous-trusted` to use the preview workflow without Host confirmation. See [Configuration](configuration.md#operational-modes) before enabling mutation.

External plugin loading is independently disabled by default. To load trusted third-party packages, add the valueless `--enable-plugins` switch and one or more `--plugin-directory` values. Inspection-only then publishes plugin query tools but continues to omit their mutation tools. Plugins execute in-process with the Host user's permissions, so enable only reviewed packages.

To restrict Workspace admission to specific source trees, configure one or more allowed roots. This example also rejects a Workspace when its evaluated documents extend outside its effective root:

```json
{
  "command": "roslyn-workbench-mcp",
  "args": [
    "--operational-mode", "inspection-only",
    "--state-directory", "/absolute/path/to/roslyn-workbench-state",
    "--allowed-workspace-root", "/absolute/path/to/engineering-source",
    "--allowed-workspace-root", "/absolute/path/to/team-source",
    "--external-document-policy", "reject-workspace"
  ]
}
```

Use `allow-read-only` instead of `reject-workspace` when trusted builds legitimately evaluate linked or generated documents outside the source boundary. Command-line roots replace, rather than extend, any `ROSLYN_WORKBENCH_MCP_ALLOWED_WORKSPACE_ROOTS` environment value.

The server communicates over standard input and standard output. Protocol data uses stdout; operational logging uses stderr.

For a source build, use the absolute path to the published `Roslyn.Workbench.Mcp` executable instead of the installed command.

## Trust the workspace before opening it

Open only a fully trusted workspace. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. Allowed Workspace roots reduce accidental agent scope; they do not sandbox this code or constrain build-tool inputs. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox before opening it.

## First workflow

1. Call `server-status` with `detail` set to `Full` and review component status, startup fallbacks, recovery state and the published tool count.
2. After establishing that the workspace and its build inputs are fully trusted, call `workspace-open` with the absolute path to a `.sln`, `.slnx` or `.csproj`. If the project requires caller-specific MSBuild configuration, include the optional allowlisted `msBuildProperties`; use `artifactsPath` only when the build itself requires a non-default artifacts location. Standard SDK, NuGet and Visual Studio locations are discovered through normal MSBuild evaluation. Evaluated documents outside the workspace root are queryable but read-only. A solution may contain unsupported languages or non-SDK-style projects; they are skipped with load diagnostics. At least one supported SDK-style C# project must remain.
3. Use query tools against the loaded workspace.
4. If the configured operational mode supports mutation, read [Workspaces and transactions](workspaces-and-transactions.md) and check `workspace-status` before starting a transaction.

The server starts without a loaded workspace. It can keep multiple workspaces open, but only one loaded workspace may own the active transaction slot.
