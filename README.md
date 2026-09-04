# Roslyn Workbench MCP

![Roslyn Workbench MCP wordmark](assets/roslyn-workbench-mcp-wordmark.svg)

> [!WARNING]
> Roslyn Workbench is in beta. Tool contracts, configuration and persisted state may change between releases. Review the release notes before upgrading.

Roslyn Workbench is a local stdio MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.

It provides persistent multi-workspace sessions, semantic queries for symbols and code structure, bounded agent-facing results, and source changes staged through transactions with preview, history, rollback, conflict detection and crash recovery. Code Actions use the same transaction boundary. Nothing writes source files until an explicit `transaction-commit` call.

## Install

Roslyn Workbench is distributed as the `Lantean.Roslyn.Workbench.Mcp` .NET tool and requires a supported .NET 10 SDK. The 0.1.0 beta is distributed through [NuGet.org](https://www.nuget.org/packages/Lantean.Roslyn.Workbench.Mcp). Other engineering prereleases may use [GitHub Packages](https://github.com/lantean-code/roslyn-workbench-mcp/packages); consult the notes for the selected release.

For a package published to NuGet.org:

```bash
dotnet tool install --global Lantean.Roslyn.Workbench.Mcp --prerelease
```

See the [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) for installation and MCP client configuration.

Configure any MCP client capable of launching a local stdio process to run:

```json
{
  "command": "roslyn-workbench-mcp",
  "args": ["--state-directory", "/absolute/path/to/roslyn-workbench-state"]
}
```

## Supported environments

Beta support covers Windows x64, Linux x64 and WSL2 x64. macOS x64 and ARM64 are best effort until hosted validation exists. Windows ARM64 and Linux ARM64 are not currently supported release targets. The server can be used by any MCP client that supports local stdio servers; optional capabilities such as elicitation depend on the client and its policy.

## Improve agent tool selection

Connecting Roslyn Workbench makes its tools available, but an MCP client may not select them automatically when ordinary file and text-search tools can also attempt the task. Short repository instructions can tell an agent when compiler-aware tooling is the better choice without forcing it into every C# workflow.

Add this basic instruction to the user-level guidance used by your agent, or to repository guidance when it should apply only to a particular codebase:

```markdown
## Roslyn Workbench

When Roslyn Workbench MCP is available, consider using it for C# work that benefits from compiler semantics, including precise symbol navigation, references, diagnostics, code structure, Code Actions, change-impact analysis and transactional source changes. Its semantic tools are particularly useful when symbol identity or compiler interpretation matters, while the repository's normal tools and commands remain suitable for builds, tests, package management, documentation and non-semantic file operations. Transaction previews and structured next actions can help keep source changes safe and easy to review.
```

The [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) explains user-level and repository-level setup for Codex, GitHub Copilot, Claude Code and other MCP clients. User-level guidance avoids repeating the instruction when Roslyn Workbench is available across repositories, while repository guidance can provide a more specific layer for an individual codebase.

## Trust and transaction safety

Open only workspaces you trust. Loading a workspace evaluates MSBuild project logic and later operations can load project analyzers with the Host process's operating-system permissions. Third-party plugins also execute as trusted in-process code and are not sandboxed.

A typical safe workflow is:

1. Inspect `server-status` and open a trusted solution, solution filter or project with `workspace-open`.
2. Use query tools to inspect the loaded workspace.
3. Check `workspace-status`, then begin a transaction with `transaction-start`.
4. Stage a mutation or Code Action and inspect it with `transaction-preview`.
5. Commit the reviewed transaction or discard it with `transaction-rollback`.

The live MCP `tools/list` response is authoritative for the tools and schemas exposed by a running instance. The [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) provides the richer versioned tool reference and agent guide.

## Beta limitations

The existing plugin runtime remains available to source consumers, but the Plugins NuGet package, supported plugin-authoring documentation and curated plugin repository will not be published until v1 preparation. External pull requests are also not accepted before v1; Issues, Discussions, private security reports and other feedback remain welcome.

## Support and project links

- [Support and feedback routes](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SUPPORT.md)
- [Security reporting](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SECURITY.md)
- [Contribution policy](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CONTRIBUTING.md)
- [Code of Conduct](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CODE_OF_CONDUCT.md)
- [Documentation](https://lantean-code.github.io/roslyn-workbench-mcp/)
- [Releases](https://github.com/lantean-code/roslyn-workbench-mcp/releases)
- [GitHub Packages](https://github.com/lantean-code/roslyn-workbench-mcp/packages)
- [NuGet package](https://www.nuget.org/packages/Lantean.Roslyn.Workbench.Mcp)

## Thanks

[![Roslyn](assets/third-party/roslyn-readme.svg)](https://github.com/dotnet/roslyn)

Roslyn Workbench MCP is made possible by the [.NET Compiler Platform](https://github.com/dotnet/roslyn). Thank you to the Roslyn team for building and maintaining the compiler tooling that powers its semantic analysis and refactoring capabilities.

[![Sentry](assets/third-party/sentry-readme.svg)](https://sentry.io/)

Thank you to [Sentry](https://sentry.io/) for supporting the project with an open-source licence for its opt-in error-reporting service.

Roslyn Workbench MCP is licensed under the [MIT License](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/LICENSE).
