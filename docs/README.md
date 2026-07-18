# Roslyn Workbench documentation

Roslyn Workbench is a local stdio MCP server that uses Roslyn to inspect C#
workspaces and stage source changes through explicit transactions.

## Use the server

1. [Get started and connect an MCP client](GettingStarted.md).
2. Review the available [startup configuration](Configuration.md).
3. Let the client discover the live [tool surface and result contracts](ToolDiscovery.md).
4. Follow the [workspace and transaction safety model](WorkspacesAndTransactions.md)
   before making changes.

## Extend the server

[Third-party plugin authoring](PluginAuthoring.md) describes the trusted
in-process plugin API, package layout, validation and deployment rules.

## Documentation authority

The documents directly under `docs` are intended to accompany a release and
describe supported user and agent behaviour. MCP `tools/list` is the
authoritative tool inventory for a running server because the enabled plugin
set and Code Action catalogue are fixed during startup.

Plans, specifications, audits, implementation matrices, inventories, backlog
items and dated evidence are kept under [`development`](development/). They are
engineering records and may describe intended, intermediate, historical or
aspirational states rather than the release contract.
