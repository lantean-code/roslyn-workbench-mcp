# Subsystem review: workspace loading, resolution, leases and query caches

## Scope and relationships

This unit covers Workspace loading, lifecycle, session state, operation gates, selectors/resolvers, project/reference services, change detection, instance coordination and both query-cache families. It depends on Abstractions and is consumed by Plugins, CodeActions and the Host composition root.

## Implementation and boundary review

- `WorkspaceLoader`/`WorkspaceLoadWorkflow` create and validate `MSBuildWorkspace` instances, retain supported SDK-style C# projects and return the Workspace as an owned resource. `WorkspaceLifecycleService` establishes sessions, manifests, input monitors and cross-process advisory status.
- `WorkspaceSessionStore` serialises session replacement/removal and lifecycle-observer invalidation. Query leases use shared operation-gate access; mutation/commit paths use exclusive access and revalidate state after acquisition.
- Resolver paths validate workspace epoch plus solution or transaction revision. Location/document/project selectors are resolved against the acquired snapshot, not the live Workspace after acquisition.
- `QueryCacheStateCore` partitions entries by snapshot/generation, coalesces in-flight computations, lets individual waiters cancel without poisoning remaining waiters, cancels the factory when its last waiter departs and rejects stores after invalidation. Plugin cache admission excludes disposable values.
- File-system input monitoring records the first relevant change and transitions sessions to out-of-date before later operations continue. Instance status files are advisory and I/O failures degrade availability rather than authorising unsafe writes.

## Consumers, DI and configuration

`AddWorkspaceServices` registers Workspace services and cache states as singletons. `WorkspaceOptions`, `WorkspaceQueryCacheOptions` and `PluginQueryCacheOptions` are derived from validated startup options. The production Generic Host supplies `IHostApplicationLifetime` used to cancel cache factories at shutdown.

## Tests and findings

Workspace unit tests cover lifecycle branches, resolver outcomes, cache coalescing/invalidation, leases, change detection and coordination. Integration coverage exists for real MSBuild loads and cross-component state, but its shared fixture is currently broken by RWMCP-004. Validated finding RWMCP-001 identifies the ownership window after a successful load and before session registration where cancellation from instance-status publication leaks the loaded Workspace.
