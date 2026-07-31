# Roslyn Workbench documentation

Roslyn Workbench is a local stdio MCP server that uses Roslyn to inspect C# workspaces and stage source changes through explicit transactions.

## Use the server

1. [Get started and connect an MCP client](GettingStarted.md).
2. Review the available [startup configuration](Configuration.md).
3. Let the client discover the live [tool surface and result contracts](ToolDiscovery.md).
4. Follow the [workspace and transaction safety model](WorkspacesAndTransactions.md) before making changes.
5. Review the boundary for [local diagnostics and user-approved external error reporting](ErrorReporting.md).
6. Use the final [Code Action discovery, Fix All and staging workflow](CodeActions.md).

## Extend the server

[Third-party plugin authoring](PluginAuthoring.md) describes the trusted in-process plugin API, package layout, validation and deployment rules. [Plugin authoring diagnostics](PluginAuthoringDiagnostics.md) documents the build-time guidance supplied by the Plugins package.

The author-facing Plugins package includes its minimal Abstractions assembly; plugin authors do not install or deploy the Workspace implementation.

## Documentation authority

The documents directly under `docs` are intended to accompany a release and describe supported user and agent behaviour. MCP `tools/list` is the authoritative tool inventory for a running server because the enabled plugin set and the three Host-owned Code Action tools are composed once during startup.

Plans, specifications, audits, implementation matrices, inventories, backlog items and dated evidence are kept under [`development`](development/). They are engineering records and may describe intended, intermediate, historical or aspirational states rather than the release contract.
