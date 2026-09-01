# Roslyn Workbench MCP { .rw-visually-hidden }

![Roslyn Workbench MCP wordmark](assets/generated/roslyn-workbench-mcp-wordmark.svg)

Roslyn Workbench is a local stdio MCP server that uses Roslyn to inspect C# workspaces and stage source changes through explicit transactions.

## Use the server

1. [Get started and connect an MCP client](getting-started.md).
2. Review the available [startup configuration](configuration.md).
3. Let the client discover the live [tool surface and result contracts](tool-discovery.md).
4. Follow the [workspace and transaction safety model](workspaces-and-transactions.md) before making changes.
5. Review the boundary for [local diagnostics and user-approved external error reporting](error-reporting.md).
6. Use the final [Code Action discovery, Fix All and staging workflow](code-actions.md).

Agents that need more detail than the MCP initialisation instructions provide should use the version-specific [agent guide](agent/index.md).

## Documentation authority

This site describes supported user and agent behaviour for the displayed release. MCP `tools/list` remains authoritative for a running server because its enabled plugin set is composed at startup.
