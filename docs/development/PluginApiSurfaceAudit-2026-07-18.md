# Plugin API Surface Audit

Date: 2026-07-18

## Purpose

This audit verifies that `Roslyn.Workbench.Mcp.Plugins` exposes only the contracts required by trusted third-party plugins and does not publish Host-owned composition, registration, execution-lease or mutation-staging controls.

## Supported public surface

The supported surface is grouped as follows:

| Category | Public contracts |
| --- | --- |
| Entry and metadata | `IRoslynPlugin`, `RoslynPluginAttribute`, `RoslynToolAttribute`, `PluginApiVersions` |
| Configuration | `IPluginConfiguration`, `ToolConfigurationBuilder<TBuilder>`, `QueryToolConfigurationBuilder`, `MutationToolConfigurationBuilder` |
| Handlers and contexts | query and mutation handler interfaces, `IToolExecutionContext`, `IQueryContext`, `IMutationContext` |
| Execution results | `PluginExecutionResult<TResponse>`, `PluginExecutionOutcome`, `PluginExecutionError`, `MutationCandidate` |
| Query helpers | `IToolExecutionServices`, request resolution, compiler diagnostics, inspection, project structure and dependency analysis contracts and their result models |
| Transitive Workspace surface | selector, result and resolution contracts only |

The exact exported type set is locked by `PluginPublicApiContractTests`. Adding another exported type requires an intentional contract-test update and review of its transitive member signatures.

## Removed accidental exports

The audit internalised these Host-only types:

- `PluginMetadata`, which is derived from trusted assembly metadata and belongs to Host catalogue preparation;
- `ToolExecutionFailureResult`, which carries Host-generated short-circuit and publication state;
- `ToolKind`, which belongs to internal registration and transport materialisation; and
- `ToolBehaviorHints`, which belongs to internal registration metadata.

The execution context factory, query and mutation leases, registration models, configuration preparation, result mapping and Workspace staging adapters were already internalised during the preceding CA1062 correction.

## Capability boundary

The public API contract tests enforce that:

- only the approved exported type set remains public;
- public signatures may reference only Workspace contract and resolution namespaces;
- `IMutationContext` adds no capability beyond the common execution context;
- execution-context properties are get-only; and
- the internal Workspace mutation stager is not assignable from the public mutation context.

The supported mutation path is therefore:

1. Host supplies a read-only execution context.
2. The plugin derives an immutable candidate `Solution`.
3. The plugin returns a `MutationCandidate`.
4. Host validates the candidate against the current Workspace and active transaction.
5. Host stages the accepted candidate through its internal Workspace stager.

No supported plugin API exposes Host DI, MCP transport objects, workspace lifecycle services, transaction services, file writers, commit infrastructure or mutation staging.

## Trusted in-process limitation

This boundary prevents accidental API coupling; it is not a security boundary. Plugins are trusted in-process assemblies. The intentionally exposed Roslyn `Solution` has a transitive `Workspace` property, and arbitrary in-process code can use file I/O or reflection regardless of .NET accessibility.

Plugin authors must treat `CurrentSolution` as query-only and must not call `CurrentSolution.Workspace.TryApplyChanges`, write source directly or use reflection to bypass Host controls. Enforcing that rule against untrusted or adversarial plugins would require a different architecture, such as out-of-process execution, rather than additional accessibility modifiers.

## Outcome

The Plugins assembly now exposes no Host-owned staging or lifecycle capability through its supported public API. A later authoring-surface extraction moved every approved Workspace-namespaced public dependency into the minimal Abstractions assembly and locked both assemblies with exact exported-type contract tests. The trusted in-process limitation remains explicit in release-facing plugin-authoring guidance.

The built-in tool assemblies now expose only intentional entry points. Code Actions export no types because their catalogue and MCP publication are Host-owned. `Plugins.Core` exports only `BundledCorePlugin`; its request, response and supporting DTOs are internal. Bundled preparation explicitly allows non-public transport contracts, while external plugin preparation continues to reject any request or response graph containing a non-public type. Contract tests lock both exported type sets and the two validation policies.
