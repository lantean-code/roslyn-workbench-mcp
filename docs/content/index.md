# Roslyn Workbench MCP { .rw-visually-hidden }

![Roslyn Workbench MCP wordmark](assets/generated/roslyn-workbench-mcp-wordmark.svg)

## Your Visual Studio C# workflow, now available to your agent

The C# fixes and refactorings you use in Visual Studio 2026—now available to your AI coding agent.

Roslyn Workbench MCP brings Roslyn Code Actions, semantic navigation and compiler-aware C# analysis to your agent through a local MCP server. Discover the fixes and refactorings that apply to your code, stage the compiler-generated changes, and review the diff before writing source files. It uses the same .NET Compiler Platform that powers Visual Studio's C# tooling.

## Put Roslyn Code Actions to work

Over 230 eligible built-in Roslyn Code Fix and refactoring providers give your agent more than another way to edit text. Use [Code Actions](code-actions.md) to extract methods, introduce parameters, encapsulate fields, convert loops to LINQ and apply fixes for compiler and analyzer diagnostics. Supported Fix All operations can prepare changes across a document, project or solution.

An agent can discover applicable actions, select the right fix or refactoring, stage it into a transaction, and present the resulting changes for review. Provider count is not an individual-action count: available choices depend on the code, diagnostics and project configuration. Actions requiring interactive option dialogs, package installation or unsupported project-system changes are excluded; this is not complete Visual Studio feature parity.

## Understand the change, then make it

- Find definitions, references, implementations, callers and callees using compiler semantics.
- Explore type hierarchies, dependencies and public APIs across persistent multi-workspace sessions.
- Inspect diagnostics, async usage, nullability and control flow; investigate change and test impact before editing.
- Stage Code Actions and symbol renames through [reviewable transactions](workspaces-and-transactions.md), with rollback, conflict detection and crash recovery.

The built-in reference catalogue contains 57 MCP tool definitions. The running server exposes only those permitted by its operational mode and configuration, plus enabled plugin tools. The three Code Action tools unlock many applicable fixes and refactorings without publishing a separate MCP tool for every provider.

## Local execution, deliberate source changes

Workspace analysis and refactoring run on your machine over stdio. [Inspection-only is the default](configuration.md); enable a mutation mode when you want your agent to stage changes. Nothing writes source files until an explicit transaction commit. Builds, tests and Git commits stay in your normal development workflow.

[Connect your coding agent](getting-started.md), then add [agent guidance](agent-setup.md) so it knows when to choose Roslyn's semantic tools and Code Actions.

## Use the server

1. [Get started and connect an MCP client](getting-started.md).
2. Review the available [startup configuration](configuration.md).
3. Let the client discover the live [tool surface and result contracts](tool-discovery.md).
4. Follow the [workspace and transaction safety model](workspaces-and-transactions.md) before making changes.
5. Review the boundary for [local diagnostics and user-approved external error reporting](error-reporting.md).
6. Use the [Code Action discovery, Fix All and staging workflow](code-actions.md).

Agents that need more detail than the MCP initialisation instructions provide should use the version-specific [agent guide](agent/index.md).

Questions, ideas, defects, documentation problems and private reports each have a defined route in the repository [support and feedback guide](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/SUPPORT.md).

## Documentation authority

This site describes supported user and agent behaviour for the displayed release. MCP `tools/list` remains authoritative for a running server because its enabled plugin set is composed at startup.
