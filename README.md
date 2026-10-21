# Roslyn Workbench MCP

<!-- mcp-name: io.github.lantean-code/roslyn-workbench-mcp -->

![Roslyn Workbench MCP wordmark](assets/roslyn-workbench-mcp-wordmark.svg)

## Your Visual Studio C# workflow, now available to your agent

The C# fixes and refactorings you use in Visual Studio 2026—now available to your AI coding agent.

Roslyn Workbench MCP gives your agent access to Roslyn Code Actions and compiler-aware C# analysis through a local MCP server. Your agent can discover applicable fixes and refactorings, apply the compiler-generated changes, and show you the result before writing source files. It uses the same .NET Compiler Platform that powers Visual Studio's C# tooling.

## Put Roslyn Code Actions to work

Over 230 eligible built-in Roslyn Code Fix and refactoring providers sit behind one discovery and staging workflow. Ask your agent to:

- Extract a method, introduce a parameter, inline a temporary variable or encapsulate a field.
- Convert a loop to LINQ or modernise code with an applicable Roslyn refactoring.
- Discover fixes for compiler and analyzer diagnostics in the current document.
- Prepare supported Fix All operations across a document, project or solution.
- Stage the selected changes, inspect the transaction diff, then commit or roll back.

These are Roslyn-generated changes, selected for your actual code and project configuration—not instructions for an agent to imitate a refactoring with text replacements. See the [Code Action guide](https://lantean-code.github.io/roslyn-workbench-mcp/latest/code-actions.html) for discovery, Fix All and staging.

The provider count is not a count of individual actions: a provider can offer multiple choices, and applicability depends on the code, diagnostics and project configuration. Actions requiring interactive option dialogs, package installation or unsupported project-system changes are excluded; this is not complete Visual Studio feature parity.

## Understand the code before changing it

- Navigate to definitions, find references and implementations, and trace callers and callees using compiler semantics.
- Explore type hierarchies, project dependencies and the public API surface across persistent multi-workspace sessions.
- Inspect diagnostics, async usage, nullability and control flow, and investigate change and test impact.
- Keep agent responses bounded instead of loading an entire solution into the conversation.

The built-in reference catalogue contains 57 MCP tool definitions covering navigation, analysis, Code Actions and workspace management. The live tool set depends on operational mode, configuration and enabled plugins; the three Code Action tools expose many applicable fixes and refactorings without adding one MCP tool per provider.

## Compiler-powered changes, reviewable transactions

Source changes are staged through transactions with preview, history, rollback, conflict detection and crash recovery. Code Actions and symbol renames use the same transaction boundary: nothing writes source files until an explicit transaction commit. In approval-required mode, review is bound to an exact-change receipt.

The server runs locally over stdio, with workspace analysis and refactoring performed on your machine. Inspection-only is the default; you choose when to enable source changes. Builds, tests and Git commits remain part of your normal development workflow.

## Install

Roslyn Workbench is distributed through [NuGet.org](https://www.nuget.org/packages/Lantean.Roslyn.Workbench.Mcp) as the `Lantean.Roslyn.Workbench.Mcp` .NET tool and requires a supported .NET 10 SDK. Pin the exact release version you intend to run. The [compatibility policy](https://lantean-code.github.io/roslyn-workbench-mcp/latest/compatibility.html) defines the stable 1.x guarantees for tools, errors and continuations, recovery formats and plugin APIs.

Run an exact package version without a permanent installation:

```bash
dnx Lantean.Roslyn.Workbench.Mcp@VERSION --yes -- --version
```

Alternatively, install the same package as a global tool with `dotnet tool install --global Lantean.Roslyn.Workbench.Mcp --version VERSION`.

See the [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) for installation and MCP client configuration.

Configure any MCP client capable of launching a local stdio process to run:

```json
{
  "command": "roslyn-workbench-mcp",
  "args": [
    "--operational-mode", "inspection-only",
    "--state-directory", "/absolute/path/to/roslyn-workbench-state"
  ]
}
```

`inspection-only` is the safe default and exposes no source-mutation or transaction tools. Select `transactional` for client-mediated commit confirmation, `approval-required` for confirmation bound to an exact transaction-review receipt, or deliberately select `autonomous-trusted` for transactions without Host confirmation. See the configuration guide for client elicitation requirements.

## Supported environments

Windows x64, Linux x64 and WSL2 x64 are supported. macOS x64 and ARM64 are best effort until hosted validation exists. Windows ARM64 and Linux ARM64 are not currently supported release targets. The server can be used by any MCP client that supports local stdio servers; operations requiring elicitation depend on the client advertising and permitting it.

## Roslyn Workbench skill

The optional skill guides agents through workspace selection, semantic inspection, Code Actions, transactions and recovery. Clients with MCP resource support can read `skill://roslyn-workbench/SKILL.md` from the installed server, with 11 task-specific references loaded only when needed.

For separate installation, [download the development skill bundle](https://lantean-code.github.io/roslyn-workbench-mcp/dev/assets/generated/roslyn-workbench-skill.zip) and follow the [installation guidance](https://lantean-code.github.io/roslyn-workbench-mcp/dev/agent/). Select documentation matching your installed server to obtain a release-specific bundle once available. Extract the complete `roslyn-workbench` directory, including its references, client metadata and logo, into your client's supported skill location. Discovery and activation depend on the client; the skill does not change permissions or Host policy.

## Improve agent tool selection

Connecting Roslyn Workbench makes its tools available, but an MCP client may not select them automatically when ordinary file and text-search tools can also attempt the task. Short repository instructions can tell an agent when compiler-aware tooling is the better choice without forcing it into every C# workflow.

Add this basic instruction to the user-level guidance used by your agent, or to repository guidance when it should apply only to a particular codebase:

```markdown
## Roslyn Workbench

When Roslyn Workbench MCP is available, consider using it for C# work that benefits from compiler semantics, including precise symbol navigation, references, diagnostics, code structure, Code Actions, change-impact analysis and transactional source changes. Its semantic tools are particularly useful when symbol identity or compiler interpretation matters, while the repository's normal tools and commands remain suitable for builds, tests, package management, documentation and non-semantic file operations. Transaction previews and structured next actions can help keep source changes safe and easy to review.
```

The [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) explains user-level and repository-level setup for Codex, GitHub Copilot, Claude Code and other MCP clients. User-level guidance avoids repeating the instruction when Roslyn Workbench is available across repositories, while repository guidance can provide a more specific layer for an individual codebase.

## Trust and transaction safety

Open only workspaces you trust. Loading a workspace evaluates MSBuild project logic and later operations can load project analyzers with the Host process's operating-system permissions. External plugin loading is disabled by default; supplying `--enable-plugins` explicitly opts into loading configured third-party plugins as trusted in-process code, which is not sandboxed.

A typical safe workflow when `transactional` or `autonomous-trusted` mode is configured is:

1. Inspect `server-status` and open a trusted solution, solution filter or project with `workspace-open`.
2. Use query tools to inspect the loaded workspace.
3. Check `workspace-status`, then begin a transaction with `transaction-start`.
4. Stage a mutation or Code Action and inspect it with `transaction-preview`.
5. Commit the reviewed transaction or discard it with `transaction-rollback`.

The live MCP `tools/list` response is authoritative for the tools and schemas exposed by a running instance. The [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/) provides the richer versioned tool reference and agent guide.

## Plugins and contributions

Plugin authors reference the separate `Lantean.Roslyn.Workbench.Mcp.Plugins` package. See the [plugin-authoring guide](https://lantean-code.github.io/roslyn-workbench-mcp/latest/plugin-authoring.html) for the supported API and deployment layout. Users install trusted plugins manually; there is no curated plugin repository.

External pull requests are accepted. Discuss the scope first and sign every contributed commit with a cryptographic signature that GitHub can verify. See the [contribution policy](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CONTRIBUTING.md) for the process.

## Support and project links

- [Support and feedback routes](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SUPPORT.md)
- [Security reporting](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SECURITY.md)
- [Contribution policy](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CONTRIBUTING.md)
- [Code of Conduct](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/CODE_OF_CONDUCT.md)
- [Documentation](https://lantean-code.github.io/roslyn-workbench-mcp/)
- [Releases](https://github.com/lantean-code/roslyn-workbench-mcp/releases)
- [NuGet package](https://www.nuget.org/packages/Lantean.Roslyn.Workbench.Mcp)

## Thanks

[![Roslyn](assets/third-party/roslyn-readme.svg)](https://github.com/dotnet/roslyn)

Roslyn Workbench MCP is made possible by the [.NET Compiler Platform](https://github.com/dotnet/roslyn). Thank you to the Roslyn team for building and maintaining the compiler tooling that powers its semantic analysis and refactoring capabilities.

[![Sentry](assets/third-party/sentry-readme.svg)](https://sentry.io/)

Thank you to [Sentry](https://sentry.io/) for supporting the project with an open-source licence for its opt-in error-reporting service.

Roslyn Workbench MCP is licensed under the [MIT License](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/LICENSE).
