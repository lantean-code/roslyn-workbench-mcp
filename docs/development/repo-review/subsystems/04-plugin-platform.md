# Subsystem review: public plugin platform and analyser

## Scope and relationships

This unit covers `Roslyn.Workbench.Mcp.Plugins`, `Roslyn.Workbench.Mcp.Plugins.Analyzers` and the Host-side preparation/materialisation bridge. Plugins depends on Abstractions and Workspace; it must not depend on CodeActions or Host MCP types. Host discovers and registers prepared plugin tools.

## Implementation and boundary review

- Plugin configuration is startup-only. Query and mutation handlers have closed generic contracts, metadata and behaviour hints; Roslyn analyser rules catch unsupported visibility, handler shapes and response collections at compile time where possible.
- Host runtime preparation independently inspects handler accessibility/constructability, resolves query/mutation contracts, materialises typed registrations and emits diagnostics instead of activating invalid external plugins.
- Execution contexts expose snapshot-scoped resolver/services and invocation-scoped cache facades. Query and mutation adapters acquire the appropriate Workspace lease, map structured plugin results and detect direct `TryApplyChanges` mutation of the live Workspace.
- External packages are trusted in-process extensions. Discovery confines entry assemblies and resolved managed/native dependencies to each package directory, uses a dedicated non-collectible load context, shares Roslyn/Abstractions/Plugins identities and fails closed on duplicate plugin IDs or global tool-name collisions.
- Query cache namespaces include snapshot, plugin ID and tool name. Disposable values are not admitted and background use after an invocation is outside the public contract.

## Consumers, DI and configuration

Host registers request resolution, compiler diagnostics, inspection/dependency services and plugin execution context factories as singletons. Repeatable plugin directories come from CLI/environment configuration, are path-deduplicated and scanned only at startup.

## Tests and findings

Unit tests cover registration, validation, typed visitor dispatch, adapters, result mapping and cache facade behaviour. Host integration tests exercise package discovery, private dependency loading and collisions, but RWMCP-005 leaves one catalogue expectation stale and RWMCP-004 disables component-workspace plugin containment tests. No production plugin-boundary finding survived validation.
