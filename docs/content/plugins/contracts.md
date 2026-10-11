# Plugin contracts and services

Use this reference after [building your first plugin](../plugin-authoring.md). You do not need caching or custom services for a basic tool.

## Registration and metadata

`RoslynPluginAttribute` is both the discovery metadata and the MEF export. Its ID must be stable and its API version must exactly match the Host API. Set the entry assembly’s `AssemblyInformationalVersionAttribute` to a valid semantic version; Host validates it with NuGet’s SemVer parser. No JSON manifest is used.

Configuration records handler types. It does not accept constructed handler instances. A handler may supply all transport metadata through `RoslynToolAttribute`.

Fluent values override attribute values. Builders and plugin-service registrations freeze when `Configure` returns; later mutation throws `InvalidOperationException`.

## Handler and response contracts

Handler implementation types may be non-public, but they must implement the appropriate non-generic handler marker through a closed generic handler contract. Request and response contracts must be public. Every query response implements `IQueryResponse` from `Roslyn.Workbench.Mcp.Plugins` and must use an object JSON contract; scalar, top-level collection, dictionary and top-level custom-converter responses are rejected before the plugin is advertised. Keep response customisation on ordinary response properties rather than replacing the top-level JSON contract. Host performs this response admission check even when `ToolOutputSchemaMode` is `Omit`; that setting controls publication only, not validation.

Runtime validation reports direct marker implementations, multiple or cross-family handler contracts, disposable handlers, MEF imports, invalid dependency graphs and invalid transport contracts as categorised errors. It accumulates all expected authoring diagnostics and atomically disables the plugin when any error exists; exceptions are reserved for unexpected loading, composition, construction or reflection failures.

Handler-owned instance or static state and handler-owned disposable-valued fields publish categorised warnings because structural inspection cannot prove thread safety or resource ownership. Readonly fields sourced directly from constructor-injected plugin services are excluded because the plugin provider owns those singleton services.

## Request validation

Request presence, nullability and value validity are separate contracts. Use C# `required` for a member that callers must supply, a non-nullable property type when explicit `null` is invalid, and ComponentModel validation attributes such as `[Required]`, `[Range]`, `[StringLength]` or `[AllowedValues]` for constraints on the supplied value. `[Required]` does not replace the C# `required` modifier, and `[NotNull]` is a nullable-analysis annotation rather than a member-presence rule.

The Host retains the JSON Schema `required` array, corrects SDK-generated property nullability, publishes declared property defaults, and evaluates property validation attributes during binding before acquiring an execution context. Validation that cannot be represented by a standard attribute, such as selector consistency or domain-specific path rules, remains the responsibility of the owning resolver or service.

## Bounded query results

Query plugins own the logical size and shape of their results. Collection-returning tools should expose explicit nullable-integer request limits, choose a positive default for each tool or distinct collection, publish that default in the tool's input schema, and apply `[Range(0, int.MaxValue)]` so the Host rejects negative values during request binding. An explicitly requested zero means that no items should be returned; zero must not be used as the declared default. `ResultLimit.GetEffectiveValue` from `Roslyn.Workbench.Mcp.Workspace.Results` resolves an omitted limit against its positive default and throws when called directly with a negative requested limit or a non-positive default.

Return a dedicated response DTO implementing `IQueryResponse`, and expose each deterministic collection through a `BoundedCollection<TItem>` property created with `BoundedCollection.CreatePrebounded<TItem>` from the same namespace. `BoundedCollection<TItem>` is a nested result component, not a top-level query response.

The `CreatePrebounded` name makes the division of responsibility explicit: the plugin must stop or limit the underlying work before constructing the response; the result contract only publishes the already-bounded items together with `HasMore` and an optional cheaply available `TotalCount`. Agents can then request more results or narrow the query when needed.

`IQueryContext.DefaultMaxResults` is available as a compatibility baseline when a plugin has not yet established a more appropriate default; it is not a substitute for a curated, published tool contract. Host does not impose a global serialised response-size ceiling, so plugin authors must also cap or summarise unusually verbose per-item fields rather than relying on byte-led truncation.

## Schema descriptions

Keep each minified UTF-8 input schema at or below 5,000 bytes so property guidance remains portable across MCP clients. Add `[Description]` only when it supplies information that the property name and schema do not already express, such as units, conditional use, sentinel meanings or operational effects. Prefer a clear, complete explanation over a label that merely separates a PascalCase name into words.

Put a `RequiresExactlyOneAttribute` or `RequiresAtLeastOneAttribute` rule in one type-level `[Description]`; the Host publishes that guidance in the root input schema, copies it into the MCP tool description and makes each participating supplied value non-null, while runtime validation remains authoritative for the cross-property rule. Do not repeat the rule on each participating property or manually add it to tool metadata.

Put other shared selector rules on the property that owns the selector so repeated leaf properties do not each restate the same rule. The Host supplies the standard input description for every `SnapshotPrecondition` property; do not repeat that guidance on individual request properties.

The Host logs a non-blocking plugin-authoring warning when a published plugin tool exceeds this budget; the tool remains available because a larger valid schema is an agent-usability concern rather than a transport failure.

## Singleton services and lifetime

Plugins may register their own singleton services through `configuration.Services.AddSingleton<TService, TImplementation>()` or `AddSingleton<TImplementation>()`. The Host builds and validates one isolated provider per plugin, automatically registers the plugin's handlers, resolves constructor-injected handlers during catalogue startup and disposes the provider at Host shutdown.

Plugin services and handlers are singletons and must be thread-safe. Registrations cannot use factory delegates, manually constructed instances, scoped lifetimes or services belonging to another plugin.

Host and Workspace capabilities come only from `IQueryContext` or `IMutationContext`; they are not injected into plugin constructors or copied into plugin providers. Mutation handlers return a candidate solution and summary, and Host stages the proposal through Workspace after the handler returns. Plugins do not receive the Host service provider, MCP objects, file writers, workspace coordinators or Code Action services.

## Execution contexts and selectors

The supported third-party API consists of plugin and tool attributes, configuration builders, handler contracts, execution contexts, execution results, mutation candidates, selector and result contracts, and the read-only analysis services exposed through `IToolExecutionServices`. Host composition, registration, execution leases, staging, result mapping and plugin-catalogue metadata are implementation details and are not public API.

Plugin authors compile against these service contracts and do not construct their implementations. Host supplies the composed `IToolExecutionServices` instance for each execution context. General plugin analysis services are defined by the Plugins assembly. Workspace selectors, result models, resolver contracts and project-system metadata service contracts are supplied by the bundled Abstractions assembly; their Host implementations remain outside the supported plugin API.

`IQueryContext` and `IMutationContext` expose the same immutable Roslyn solution snapshot and read-only resolution and analysis capabilities. `IMutationContext` does not expose a stager. Returning a `MutationCandidate` is the only supported way for a plugin to propose source changes; Host validates and stages that candidate through the active transaction.

`IWorkspaceResolver` remains invocation-scoped on the execution context because it resolves against that context's effective Workspace snapshot. `IToolExecutionContext.Snapshot` exposes the complete authoritative identity for that invocation, and every `ResolvedLocation` contains the complete identity under its nested `Snapshot` property. A Host-produced resolved source location also exposes its span-only `CanonicalLocationSelector` under `Selector`; its JSON is directly usable as `LocationSelector` input without publishing copied-selection text.

`IToolExecutionServices.WorkspaceSelectorFactory` exposes the stateless `IWorkspaceSelectorFactory` service for converting a `ResolvedLocation` into a `CanonicalLocationSelector`, canonical `LocationSelector` or location-backed `SymbolSelector`. Use the resolver to obtain snapshot-bound identities and the factory to project those identities into selectors; preserve published snapshot objects unchanged and do not duplicate selector or snapshot construction in individual tools.

## Analysis services

`IToolExecutionServices.ReferenceDiscoveryService` performs cached Roslyn reference discovery within an explicit document set. It excludes occurrences from unselected project contexts and removes duplicate occurrences within each selected Roslyn document. Supply `IQueryContext.WorkspaceIdentity.WorkspaceId`, `CurrentSolution`, the resolved symbol and the documents selected for the request. The returned `ReferenceOccurrence` values retain Roslyn documents, locations and related definition symbols; each tool remains responsible for ordering, limiting, optional enrichment and projection into its own response contract.

`IToolExecutionServices.TypeHierarchyService` discovers derived classes, derived interfaces and interface implementations within an explicit project set. It returns each unique named type with its shortest inheritance distance from the supplied root, so tools can apply their own depth, ordering and result limits without duplicating hierarchy semantics.

## Optional query-result caching

`IQueryContext.QueryResultCache` provides optional query-result reuse within the exact Workspace snapshot, plugin and registered tool. Use a named sealed immutable record class implementing `IQueryResultCacheKey`, containing every semantic input that changes the result.

`GetOrCreate` and `GetOrCreateAsync` coalesce identical misses; the factory receives a shared computation token, while cancelling one caller stops only that caller's wait.

Treat every returned reference value as immutable. A `null`, `IDisposable` or `IAsyncDisposable` result is returned to the current callers but is not retained. Exceptions and cancellations are never cached. The cache scope is valid only during the originating invocation, and correctness must never depend on a value being admitted or retained.

## Trust and protocol output

Plugins are trusted in-process code, not a security sandbox. Treat `CurrentSolution` as query-only and do not call `CurrentSolution.Workspace.TryApplyChanges`, mutate its associated Roslyn workspace, write source files directly, use reflection to reach Host internals, or otherwise bypass the transaction pipeline. A Roslyn `Solution` exposes its associated `Workspace` transitively, and in-process code can also use ordinary file and reflection APIs, so the Host cannot enforce adversarial isolation while loading plugins into its process. Plugins that are not fully trusted must not be loaded.

The stdio Host reserves raw standard output exclusively for the MCP protocol. Ordinary `Console.Write` and `Console.WriteLine` calls are redirected to standard error so cooperative plugin diagnostics cannot corrupt protocol framing. Plugins must not open or write to the raw standard-output stream directly. This redirection is a protocol-integrity guard for normal managed console output, not a sandbox against trusted code deliberately accessing process handles or native output APIs.

## Compatibility

`PluginApiVersions.V1` is the plugin API family supported throughout Host 1.x. Host package SemVer and plugin API versions are independent: a plugin targets V1 rather than a particular Host patch or minor release. Existing V1 public types, members, signatures, constants and enum meanings remain binary compatible; required members are not added to contracts implemented by plugins, and deprecated V1 members are not removed before Host 2.0. Compatible additions remain possible. See the [Host compatibility policy](../compatibility.md#plugin-api) for the complete boundary and release rules.
