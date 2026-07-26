# Plugin Authoring Analyser Audit

Date: 2026-07-24

## Purpose

This audit defines the compile-time guidance that should ship with the third-party Plugins package. It expands the initial `Workspace.TryApplyChanges` and live `Workspace.CurrentSolution` concerns into a complete authoring surface and separates:

- mistakes the C# compiler already prevents;
- high-confidence source diagnostics that should be reported in the IDE and at build time;
- runtime validation that must remain authoritative for loaded binaries and package-wide facts;
- Host containment required when trusted plugin code suppresses or bypasses an analyser; and
- broad heuristics that should not become diagnostics.

The analyser is an engineering guardrail for trusted in-process plugins. It is not a security sandbox and cannot prevent deliberate suppression, reflection, dynamically loaded code or direct filesystem access.

## Evidence reviewed

The audit reviewed:

- the public contracts and XML documentation in `Roslyn.Workbench.Mcp.Plugins`;
- the exported-surface lock in `PluginPublicApiContractTests`;
- handler contract, lifetime, composition and warning inspection;
- plugin configuration preparation and materialisation;
- plugin entry-point metadata validation and MEF composition;
- query response collection inspection;
- Workspace snapshot storage, external-change detection, execution-context acquisition, query-cache invalidation and mutation staging;
- the bundled plugin and external plugin fixtures;
- [Third-party plugin authoring](../PluginAuthoring.md);
- [Plugin API surface audit](PluginApiSurfaceAudit-2026-07-18.md);
- [MEF plugin composition and packaging plan](superpowers/plans/2026-07-13-mef-plugin-composition.md); and
- Microsoft's [Roslyn analyser and code-fix guidance](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix); and
- Microsoft's [NuGet analyser conventions](https://learn.microsoft.com/nuget/guides/analyzers-conventions).

Microsoft's current guidance keeps analyser execution compatible with command-line and Visual Studio hosts by targeting .NET Standard 2.0, testing diagnostics with `Microsoft.CodeAnalysis.Testing`, and placing C# analyser assemblies under `analyzers/dotnet/cs` in the API package.

## Current enforcement boundary

### Compiler-enforced facts

The existing generic configuration API already provides the best diagnostic for:

- a handler that is not a class;
- an abstract or open handler supplied to `AddQueryTool<THandler>` or `AddMutationTool<THandler>`;
- a handler without public parameterless construction;
- a query request that does not derive from `WorkspaceBoundRequest`; and
- a type that does not implement the required non-generic family marker.

The authoring analyser should not replace or restate these compiler errors.

### Runtime-enforced facts

Runtime preparation currently rejects:

- missing, multiple or cross-family closed handler contracts;
- non-public external request and response contract types;
- `IDisposable` and `IAsyncDisposable` handlers;
- MEF imports on handlers;
- missing final tool metadata;
- destructive query metadata;
- duplicate names within one plugin;
- invalid package and entry-point metadata;
- unsupported API versions;
- local and global plugin or tool collisions;
- composition, construction and materialisation failures; and
- invalid candidate solutions presented to the mutation staging boundary.

Runtime inspection also warns about instance state, mutable properties or events, mutable static state, legacy registration shapes and unbounded top-level query response collections.

These checks must remain. An analyser may provide earlier and better-located feedback, but a loaded binary might have suppressed the analyser, been built without it or been produced by another language or toolchain.

### Gap in Host containment

The existing external-change guard fingerprints files and directories. `WorkspaceSessionStore` separately retains the immutable effective `Solution` and invalidates query caches when that session snapshot changes, its epoch changes or it becomes out of date.

An in-process plugin can nevertheless call `context.CurrentSolution.Workspace.TryApplyChanges(...)`. That changes the underlying `MSBuildWorkspace.CurrentSolution` in memory without changing disk timestamps, the retained session `CurrentSolution`, the Workspace epoch or the query-cache key. The current external-change detector therefore does not observe it.

The retained session snapshot limits the immediate spread of that mutation, but the associated Roslyn Workspace has diverged and Workspace-backed Roslyn services can observe inconsistent state. The analyser must be paired with runtime containment before third-party authoring is actively promoted:

1. retain the initially loaded Roslyn Workspace solution identity independently of the effective session solution;
2. validate that identity before and after each plugin invocation;
3. on unexpected change, invalidate the Workspace query cache and move the session through the existing out-of-date/conflict path;
4. reject the affected result and require reload or rollback through a stable public error and next action; and
5. cover query and mutation plugins that deliberately call `TryApplyChanges`, including a build where the analyser diagnostic is suppressed.

Normal transaction staging and commit do not call `TryApplyChanges`, so a changed underlying Roslyn Workspace solution is an invalid invariant even when the effective session solution legitimately advances through revisions.

## Recommended diagnostic catalogue

Use the `RWMCP` prefix for public authoring diagnostics. Reserve one ID per remediation concept; do not reuse runtime status IDs because runtime diagnostics have no source location and do not always have identical detection semantics.

### Workspace and configuration safety

| ID | Default | Diagnostic | Detection and scope | Code fix |
| --- | --- | --- | --- | --- |
| `RWMCP001` | Error | Do not mutate the Roslyn Workspace directly | Report direct invocation of `Microsoft.CodeAnalysis.Workspace.TryApplyChanges`. Enable compilation-wide when the assembly declares a source `RoslynPluginAttribute`; always enable inside a handler implementation. Access to `Solution.Workspace` for legitimate query APIs remains allowed. | None. The correct mutation must be returned as a `MutationCandidate`, which cannot be reconstructed safely. |
| `RWMCP002` | Error | Use the invocation solution snapshot | Report reads of `Microsoft.CodeAnalysis.Workspace.CurrentSolution` in the same plugin scope. Continue to permit `context.CurrentSolution` and immutable transformations derived from it. | Defer. Replacing a live solution with a context snapshot is safe only when the correct context symbol and intended snapshot are unambiguous. |
| `RWMCP003` | Error | Plugin configuration must complete synchronously | Report an `async` implementation of `IRoslynPlugin.Configure`. Its `void` contract would otherwise return before awaited registration completes and move failures outside startup validation. | None. The author must move asynchronous preparation outside the unsupported configuration path. |
| `RWMCP004` | Error | Do not retain startup configuration objects | Report assignment of `IPluginConfiguration`, `ToolConfigurationBuilder<TBuilder>` or its concrete builders to fields or properties, and captures that can outlive `Configure`. Local variables and synchronous helper calls remain allowed. | None. Registration should remain visibly scoped to `Configure`. |

The Workspace rules deliberately target the two mutation/snapshot hazards, not every use of `Solution.Workspace`. Some Roslyn query APIs require the associated Workspace.

### Handler contract and lifetime

| ID | Default | Diagnostic | Detection and scope | Code fix |
| --- | --- | --- | --- | --- |
| `RWMCP005` | Error | Implement exactly one handler contract | A concrete handler must expose exactly one closed query or mutation contract and no contract from the other family. This catches direct marker implementations and ambiguous contracts before runtime preparation. | None. Choosing the intended family and contracts is an author decision. |
| `RWMCP006` | Error | Plugin handlers must not own a disposable lifetime | Report handlers assignable to `IDisposable` or `IAsyncDisposable`. | None. Invocation-scoped resources must be acquired and disposed within execution or supplied through supported context services. |
| `RWMCP007` | Error | Plugin handlers must not declare MEF imports | Report `Import`, `ImportMany` and `ImportingConstructor` across the source-visible handler hierarchy. Handler construction is owned by typed registration, not MEF. | None. Host services are not an authoring extension point. |
| `RWMCP008` | Error | External transport contract types must be public | Report a request or response type, element type, generic argument or containing type that is not effectively public. The rule is for external plugins; bundled tools retain their intentional non-public transport policy. | None. Accessibility changes can alter the consumer API and must be deliberate. |
| `RWMCP009` | Warning | Handler instance state requires thread-safety review | Report explicit instance fields, writable instance properties and events on handler types. Report the authored declaration rather than compiler-generated backing fields. Inherited source state should identify the declaring member; metadata-only inherited state can report on the derived handler. | None. Removing or synchronising state is design-specific. |
| `RWMCP010` | Warning | Avoid mutable static handler state | Report non-constant, non-readonly static fields. Runtime validation should remain the fallback for compiled handlers. | None. |
| `RWMCP011` | Warning | Handler field may own a disposable resource | Report handler fields whose effective type implements `IDisposable` or `IAsyncDisposable`, including readonly fields. Ownership cannot be proven from structure, so this remains a warning rather than an error. | None. |
| `RWMCP012` | Error | Query tools cannot declare destructive behaviour | Report `RoslynToolAttribute.Destructive = true` on a query handler. Fluent query builders do not expose destructive configuration. | Remove the named argument when the attributed type is unambiguously a query handler. A code-fix project is not justified for this rule alone. |

`RWMCP009` must not claim to prove that a handler is unsafe. Stateless handlers remain the supported default because one handler instance is retained for the catalogue lifetime and may serve concurrent invocations.

### Invocation and response reliability

| ID | Default | Diagnostic | Detection and scope | Code fix |
| --- | --- | --- | --- | --- |
| `RWMCP013` | Info | Observe the invocation cancellation token | Report a concrete `ExecuteAsync` implementation whose cancellation-token parameter is never meaningfully read or forwarded. Assignment to a discard does not satisfy the rule. A fast synchronous implementation may suppress the advisory; cancellable Roslyn or I/O work should propagate it. | None. Adding a token check or forwarding it requires knowledge of the operation. |
| `RWMCP014` | Warning | Bound agent-facing query collections | Report a raw array or common list, set, dictionary or asynchronous collection as the query response itself, and equivalent raw collection properties directly on the response. Recommend the public `BoundedCollection<TItem>` contract, which publishes already-bounded items, `HasMore` and `TotalCount` when cheaply available. | None. Changing the response contract is an API design change. |

`RWMCP014` deliberately validates the published response shape, not whether a particular `CreatePrebounded` call was supplied with genuinely prebounded work. That distinction matters: valid handlers can receive an already-limited result from another service, while a syntactic `Take(...)` immediately before the factory might merely truncate a fully materialised and expensive result. A call-site diagnostic would therefore produce both false positives and false confidence.

The `BoundedCollection<TItem>` factories enforce internally consistent response metadata, including known-total validation and derived `HasMore`, but cannot prove how much discovery or projection work occurred. The analyser also cannot reliably prove deterministic ordering, sensible curated limits, schema defaults or early termination. Those remain documentation, review, focused performance analysis and runtime-behaviour concerns. Property-name heuristics such as names ending in `Limit` must not become diagnostics.

### Plugin entry-point metadata

| ID | Default | Diagnostic | Detection and scope | Code fix |
| --- | --- | --- | --- | --- |
| `RWMCP015` | Error | Plugin entry-point marker and contract must agree | Report `RoslynPluginAttribute` on a type that cannot be composed as a concrete `IRoslynPlugin`, and report a concrete source `IRoslynPlugin` implementation with no marker. Abstract shared bases are allowed without a marker. | Add the interface or marker only when the existing type already satisfies the corresponding contract; otherwise no automatic fix. |
| `RWMCP016` | Error | A plugin assembly cannot declare multiple marked entry points | At compilation end, report every marked source type when more than one exists. Do not require an entry point in ordinary dependency assemblies that merely reference the Plugins package. | None. |
| `RWMCP017` | Error | Declare the supported plugin API version | Compare the attribute's constant API argument with the `PluginApiVersions.V1` constant from the referenced Plugins assembly. Runtime remains authoritative against the installed Host. | Defer. Replacing the value is simple, but adding a code-fix assembly for one edit is not justified initially. |
| `RWMCP018` | Error | Plugin identity metadata must not be blank | Report a null, empty or whitespace plugin ID or display name in `RoslynPluginAttribute`. | None. Inventing stable identity is unsafe. |
| `RWMCP019` | Error | Tool metadata must decorate a handler | Report `RoslynToolAttribute` on a type that implements no closed query or mutation handler contract. Attribute-free handlers remain valid because fluent configuration can supply all metadata. | None. Choosing the intended handler contract is an author decision. |

Assembly informational SemVer remains a runtime/package check. Implementing a second SemVer parser or loading `NuGet.Versioning` into the analyser host would add dependency and compatibility risk for little benefit.

## Diagnostics deliberately excluded

Do not add diagnostics for:

- all access to `Solution.Workspace`;
- arbitrary `System.IO` use, because plugins may legitimately read configuration, indexes or non-source inputs and reliable source-path provenance would require brittle dataflow;
- HTTP, socket, DNS or external-process APIs, because trusted plugins can have legitimate uses for them while malicious in-process code can bypass source diagnostics through suppression, dependencies, reflection, native code or generated IL;
- reflection or dynamic invocation, which would create noise without establishing a security boundary;
- semantic thread safety, lock correctness or whether a readonly reference points to mutable state;
- handler registration completeness or global name uniqueness, because registration can be conditional and package-global collisions are known only to Host;
- final merged tool metadata, because fluent configuration can override attributes and runtime preparation already evaluates the authoritative result;
- JSON serialisability or complete MCP schema support, because the actual serializer/schema provider must remain authoritative;
- curated collection-limit values, `DefaultValueAttribute` use or deterministic ordering based on naming conventions;
- whether items passed to `BoundedCollection.CreatePrebounded<TItem>` were limited before expensive work, because that requires semantic knowledge across service and Roslyn API boundaries;
- whether a mutation candidate was derived from the invocation snapshot, because Host candidate validation is the enforceable boundary;
- direct marker, construction and request-base errors already emitted by generic constraints at the registration call;
- repository coding style, LINQ choices or source-governance rules unrelated to the public plugin contract; or
- package layout, dependency resolution, informational version parsing and cross-package collisions.

Unregistered-handler analysis can be reconsidered only if real author feedback shows it is a recurring mistake. It is not a first implementation rule because source generators, conditional registration and handler libraries can make absence of a local registration intentional.

Network and external-process diagnostics must not be presented as prompt-injection prevention. An error-level diagnostic or pre-load metadata scan would block only cooperative or straightforward implementations, while code intending to evade the policy retains the full permissions of the Host process. This would restrict legitimate trusted plugins without establishing a security boundary. If untrusted plugin execution becomes a product requirement, address it through a separately designed process and operating-system sandbox boundary rather than expanding this analyser catalogue.

## Runtime validation alignment

Runtime validation should retain every objective safety and package check. The analyser adds source locations and earlier feedback; it does not replace Host preparation.

The Batch 2 diagnostics map to the stable runtime diagnostics as follows:

| Authoring diagnostic | Runtime diagnostic |
| --- | --- |
| `RWMCP005`, `RWMCP008` | `PluginHandlerContract` |
| `RWMCP006` | `PluginHandlerLifetime` |
| `RWMCP007` | `PluginHandlerComposition` |
| `RWMCP009` | `PluginHandlerInstanceState`, `PluginHandlerMutableMembers` |
| `RWMCP010` | `PluginHandlerStaticState` |
| `RWMCP011` | `PluginHandlerDisposableField` |
| `RWMCP012` | `PluginToolBehaviour` |
| `RWMCP013` | No direct runtime diagnostic; cancellation observation is advisory |
| `RWMCP014` | `QueryResponseContract` |
| `RWMCP015`, `RWMCP016` | `PluginDiscovery` and composition validation |
| `RWMCP017`, `RWMCP018` | `PluginMetadata` |
| `RWMCP019` | No direct runtime diagnostic; unregistered attributed types are not activated |

The runtime warning adjustments are:

1. `PluginLegacyRegistration` no longer treats an arbitrary static method named `Register` as legacy registration. The precise `ToolRegistrationMetadata` field check remains.
2. `BoundedCollection<TItem>` now belongs to the public Plugins authoring surface rather than the bundled `Plugins.Core` contracts, so the runtime recommendation is actionable for external authors. Its intent-revealing `CreatePrebounded` factories make the plugin responsible for applying the bound before constructing the response. `QueryResponseContractInspector` should also inspect a raw collection used directly as `TResponse`, not only collection-valued response properties.

Keep a documented mapping between `RWMCP` diagnostics and related runtime IDs. Do not silently rename the stable runtime IDs exposed through `server-status`.

## Project and package design

### Projects

Add:

- `src/Roslyn.Workbench.Mcp.Plugins.Analyzers` targeting `netstandard2.0`; and
- `test/Roslyn.Workbench.Mcp.Plugins.Analyzers.Test` targeting the repository test framework.

The projects and Plugins package wiring are now in place. The analyser assembly is packaged under `analyzers/dotnet/cs`, and architecture coverage locks its .NET Standard 2.0 target and absence of runtime, Workspace, Plugins and MEF assembly dependencies.

Batch 1 is complete. Stable descriptor infrastructure and exact-location source tests cover `RWMCP001`–`RWMCP004`. The Host retains the initially loaded Roslyn solution identity, validates it before and after plugin invocation, invalidates cached queries and moves unexpected changes through the existing workspace-out-of-date or transaction-conflicted recovery path. Focused Workspace, Plugins adapter, Host transport and real query/mutation integration tests cover the containment boundary, including deliberate `RWMCP001` suppression.

Use the American-spelled `Analyzers` suffix for the .NET project/package convention while retaining British English in prose.

The analyser assembly must not reference `Roslyn.Workbench.Mcp.Plugins`, Workspace, Workspaces APIs, MEF or Host runtime assemblies. Resolve target API symbols by fully qualified metadata name during a compilation-start action. This avoids a circular project dependency and prevents analyser-load failures caused by target-package dependencies.

Reference only the compiler-layer Roslyn packages required for C# `IOperation` and symbol analysis, with analyser build dependencies marked private. Add `Microsoft.CodeAnalysis.Analyzers` and enable its extended analyser-development rules. Do not use `Microsoft.CodeAnalysis.Workspaces` unless a future code-fix project creates a measured requirement.

The initial analyser is C#-specific and belongs under the `cs` package path. Runtime validation continues to protect plugins produced by other .NET languages. Compile against the minimum Roslyn compiler API supported by the v1 plugin-author toolchain rather than selecting APIs merely because the Host's Workspaces dependency is newer.

### NuGet inclusion

Ship the analyser inside the `Roslyn.Workbench.Mcp.Plugins` NuGet package at:

```text
analyzers/dotnet/cs/Roslyn.Workbench.Mcp.Plugins.Analyzers.dll
```

Do not create a second package that plugin authors must discover and install. The analyser must remain a build-time asset and must not be copied into plugin package output or the Host runtime.

The release packaging work must verify:

- the analyser DLL exists once at the expected package path;
- compiler dependencies are not exposed as runtime dependencies of the Plugins package;
- installing the Plugins package automatically enables the diagnostics in build and IDE discovery;
- `PrivateAssets` prevents analyser implementation dependencies leaking to consumers; and
- a clean external sample plugin builds without repository project references.

### Repository dogfooding

The shipped analyser targets external authoring rules. `Plugins.Core` deliberately allows internal request/response contracts and should not be made public merely to satisfy `RWMCP008`.

Dogfood the analyser through:

- its dedicated source-level test suite;
- one valid external fixture or clean consumer sample built with all default diagnostics enabled; and
- deliberate negative snippets in analyser tests.

Do not attach it indiscriminately to invalid runtime fixtures whose purpose is to compile unsupported plugin shapes for Host validation.

## Implementation requirements

Every analyser must:

- call `EnableConcurrentExecution`;
- opt out of generated-code analysis;
- use compilation-start symbol caching and `SymbolEqualityComparer.Default`;
- prefer `IOperation` and symbol analysis over text or identifier matching;
- exit without diagnostics when required Plugins or Roslyn symbols are unavailable;
- tolerate incomplete and erroneous source while the user is typing;
- keep whole-compilation state bounded and thread-safe; and
- provide a stable help link to release-facing diagnostic documentation.

The Workspace invocation rules need a deliberate activation boundary. The recommended rule is:

- always analyse direct calls inside a type implementing a plugin handler contract; and
- analyse the rest of a compilation only when that compilation declares a source type marked with `RoslynPluginAttribute`.

This catches helper methods in a normal entry assembly without reporting on every unrelated project that happens to reference the Plugins package. Indirection into a separate dependency assembly can evade the rule and is accepted under the trusted-plugin model.

## Testing strategy

### Descriptor and source tests

Lock:

- every diagnostic ID, category, default severity, enabled-by-default value, title, message and help link;
- positive and negative source for each rule;
- derived, nested, partial and explicit-interface handler forms;
- aliases and fully qualified API use;
- inherited source and metadata handler members;
- missing target symbols and ordinary non-plugin projects;
- generated source and source containing compiler errors;
- concurrent analyser execution; and
- `.editorconfig` suppression or severity changes.

Use `Microsoft.CodeAnalysis.Testing` markup to assert exact source locations. Test snippets may intentionally contain invalid plugin code and should remain isolated from repository source-governance rules.

### Runtime and packaging tests

Add focused production tests for:

- detection before and after a plugin mutates the underlying Roslyn Workspace;
- session transition, cache invalidation and required next action after detection;
- suppressed-analyser query and mutation packages; and
- the corrected runtime collection guidance.

Add package tests that unpack the generated `.nupkg` and build a clean external sample. Acceptance does not need to execute compiler diagnostics: its responsibility remains loading and running the package artifact. Release smoke validation should compile the external sample with the packed Plugins package before Host execution.

## Implementation order

### Batch 1 — Infrastructure, Workspace safety and containment

**Status:** Complete

1. Add the analyser and analyser-test projects with stable descriptor infrastructure.
2. Implement `RWMCP001`–`RWMCP004`.
3. Add Host detection and containment for unexpected underlying Roslyn Workspace changes.
4. Add focused Workspace, Host adapter and malicious-plugin coverage.

This batch is first because the original risk is not adequately handled by an analyser alone.

### Batch 2 — Handler contracts and lifetime

**Status:** Complete

1. Implement `RWMCP005`–`RWMCP012`.
2. Map the diagnostics to the existing runtime preparation checks.
3. Remove the name-only legacy registration warning.
4. Verify the valid external fixture remains clean.

The analyser now reports ambiguous handler contracts, unsupported lifetimes and MEF imports, inaccessible external transport types, state requiring review, disposable-valued fields and destructive query metadata. Runtime preparation retains the corresponding binary checks, including the new `PluginHandlerDisposableField` warning. The name-only `Register` heuristic has been removed, and the valid external query fixture builds with the analyser enabled and no diagnostics.

### Batch 3 — Invocation, response and entry-point guidance

**Status:** Complete

1. Implement `RWMCP013`–`RWMCP019`.
2. Correct and extend runtime raw-collection inspection.
3. Add descriptor, inheritance, incomplete-code and activation-boundary coverage.

The analyser now advises when an invocation token is ignored, reports raw agent-facing query collections, validates the relationship between plugin markers and entry-point contracts, enforces a single marked entry point, checks API and identity metadata, and rejects tool metadata on non-handlers. Runtime query-response inspection now covers a raw collection used directly as `TResponse` as well as collection-valued response properties, including lists, sets, dictionaries and asynchronous collections. Source tests cover inherited response members, incomplete response types, ordinary dependency assemblies and abstract plugin bases.

### Batch 4 — Packaging and author documentation

**Status:** Complete

1. Include the analyser in the Plugins NuGet package.
2. Add `.nupkg` layout and clean-consumer build tests.
3. Publish diagnostic help pages and update plugin authoring examples.
4. Validate IDE/build discovery and the release packaging checklist.

The Plugins package now carries its authoring guide as the NuGet readme, bundles the minimal Abstractions assembly and includes the analyser exactly once at `analyzers/dotnet/cs`. An integration test packs the single Plugins package, rejects compiler-analyser and Workspace implementation leakage, verifies the generated `.nuspec`, proves that an invalid clean consumer fails with `RWMCP015`, and then proves that a valid consumer with no repository project references builds cleanly. The standard NuGet analyser path supplies both command-line and compatible IDE discovery. The analyser has a nested central-package definition so its intentionally conservative compiler API baseline cannot be raised accidentally by Host Roslyn upgrades.

Batch 4 establishes the package shape and clean-consumer validation needed by the later v1 release workflow. The release pipeline must reuse these validated package boundaries rather than rebuilding or reshaping them during publication.

## Completion criteria

The authoring analyser work is complete when:

- all 19 diagnostics have stable descriptors and positive/negative source coverage;
- Workspace mutation suppression is contained by Host runtime detection;
- runtime validation remains authoritative and its misleading heuristics are corrected;
- the analyser runs automatically from the packed Plugins package without runtime dependency leakage;
- a clean external plugin sample builds with no diagnostics;
- deliberate unsafe samples report the expected diagnostics at exact locations;
- plugin authoring documentation explains every rule and suppression boundary; and
- the analyser is explicitly described as guidance for trusted in-process code, not adversarial isolation.
