# Roslyn Workbench MCP

![Roslyn Workbench MCP wordmark](assets/roslyn-workbench-mcp-wordmark.svg)

> [!WARNING]
> Roslyn Workbench is in alpha. Tool contracts, configuration and persisted state may change between releases. Review the release notes before upgrading.

Roslyn Workbench is a local stdio MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.

It provides persistent multi-workspace sessions, semantic queries for symbols and code structure, bounded agent-facing results, and source changes staged through transactions with preview, history, rollback, conflict detection and crash recovery. Code Actions use the same transaction boundary. Nothing writes source files until an explicit `transaction-commit` call.

## Install

Roslyn Workbench is distributed as the `Roslyn.Workbench.Mcp` .NET tool and requires a supported .NET 10 SDK. Alpha and beta packages normally use [GitHub Packages](https://github.com/lantean-code/roslyn-workbench-mcp/packages). Release-candidate and production packages use [NuGet.org](https://www.nuget.org/packages/Roslyn.Workbench.Mcp), and an explicitly selected beta may also be published there.

For a package published to NuGet.org:

```bash
dotnet tool install --global Roslyn.Workbench.Mcp --prerelease
```

See [Getting started](https://lantean-code.github.io/roslyn-workbench-mcp/getting-started.html) for installation and MCP client configuration.

Configure any MCP client capable of launching a local stdio process to run:

```json
{
  "command": "roslyn-workbench-mcp",
  "args": ["--state-directory", "/absolute/path/to/roslyn-workbench-state"]
}
```

## Supported environments

Alpha support covers Windows x64, Linux x64 and WSL2 x64. macOS x64 and ARM64 are best effort until hosted validation exists. Windows ARM64 and Linux ARM64 are not currently supported release targets. The server can be used by any MCP client that supports local stdio servers; optional capabilities such as elicitation depend on the client and its policy.

## Trust and transaction safety

Open only workspaces you trust. Loading a workspace evaluates MSBuild project logic and later operations can load project analyzers with the Host process's operating-system permissions. Third-party plugins also execute as trusted in-process code and are not sandboxed.

A typical safe workflow is:

1. Inspect `server-status` and open a trusted solution, solution filter or project with `workspace-open`.
2. Use query tools to inspect the loaded workspace.
3. Check `workspace-status`, then begin a transaction with `transaction-start`.
4. Stage a mutation or Code Action and inspect it with `transaction-preview`.
5. Commit the reviewed transaction or discard it with `transaction-rollback`.

The live MCP `tools/list` response is authoritative for the tools and schemas exposed by a running instance. The [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) provides the richer versioned tool reference and agent guide.

## Alpha limitations

The existing plugin runtime remains available to source consumers, but the Plugins NuGet package, supported plugin-authoring documentation and curated plugin repository will not be published until v1 preparation. External pull requests are also not accepted before v1; Issues, Discussions, private security reports and other feedback remain welcome.

## Support and project links

- [Support and feedback routes](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SUPPORT.md)
- [Security reporting](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SECURITY.md)
- [Contribution policy](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CONTRIBUTING.md)
- [Code of Conduct](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CODE_OF_CONDUCT.md)
- [Documentation](https://lantean-code.github.io/roslyn-workbench-mcp/)
- [Releases](https://github.com/lantean-code/roslyn-workbench-mcp/releases)
- [GitHub Packages](https://github.com/lantean-code/roslyn-workbench-mcp/packages)
- [NuGet package](https://www.nuget.org/packages/Roslyn.Workbench.Mcp)

Roslyn Workbench MCP is licensed under the [MIT License](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/LICENSE).
