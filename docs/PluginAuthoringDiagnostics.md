# Plugin authoring diagnostics

The `Roslyn.Workbench.Mcp.Plugins` package includes build-time diagnostics for trusted, in-process plugin authors. These diagnostics provide early guidance; Host runtime validation and containment remain authoritative when a diagnostic is suppressed or a plugin is built without the analyser.

<a id="RWMCP001"></a>

## RWMCP001

### Do not mutate the Roslyn Workspace directly

Do not call `Microsoft.CodeAnalysis.Workspace.TryApplyChanges`. Query tools are read-only, and mutation tools must return a `MutationCandidate` so the Host can validate and stage the proposed solution through the active transaction.

The Host detects an unexpected change to the underlying Roslyn Workspace after plugin execution, rejects the result and requires the affected workspace to be reloaded or its transaction to be rolled back.

<a id="RWMCP002"></a>

## RWMCP002

### Use the invocation solution snapshot

Do not read `Microsoft.CodeAnalysis.Workspace.CurrentSolution`. Use the `CurrentSolution` supplied by the invocation context so the query or mutation observes the selected workspace snapshot and transaction revision.

Access to `Solution.Workspace` remains permitted because some legitimate Roslyn query APIs require the associated Workspace.

<a id="RWMCP003"></a>

## RWMCP003

### Plugin configuration must complete synchronously

Implement `IRoslynPlugin.Configure` synchronously. An `async void` implementation returns before awaited registration completes and moves failures outside Host startup validation.

Complete asynchronous preparation before plugin configuration or redesign it as invocation-scoped work.

<a id="RWMCP004"></a>

## RWMCP004

### Do not retain startup configuration objects

Do not assign `IPluginConfiguration` or a tool configuration builder to a field or property, and do not capture those objects in a delegate that can escape configuration.

Local variables and synchronous helper calls are supported. Keep registration visibly scoped to `IRoslynPlugin.Configure`.

<a id="RWMCP005"></a>

## RWMCP005

### Implement exactly one handler contract

A concrete handler must implement exactly one closed `IQueryToolHandler<TRequest, TResponse>` or `IMutationToolHandler<TRequest>` contract. Do not implement a marker interface directly, implement multiple contracts from one family or combine query and mutation contracts.

Split distinct tools into separate handler types so registration and transport metadata remain unambiguous.

<a id="RWMCP006"></a>

## RWMCP006

### Plugin handlers must not own a disposable lifetime

Handler instances are retained for the lifetime of the plugin catalogue. They must not implement `IDisposable` or `IAsyncDisposable`.

Acquire and dispose invocation-scoped resources during `ExecuteAsync`, or use supported services supplied by the invocation context.

<a id="RWMCP007"></a>

## RWMCP007

### Plugin handlers must not declare MEF imports

Do not use `Import`, `ImportMany` or `ImportingConstructor` on a handler or its source-visible base types. Typed plugin registration owns handler construction; Host MEF services are not a plugin-authoring extension point.

Use the services exposed through the query or mutation context.

<a id="RWMCP008"></a>

## RWMCP008

### External transport contract types must be public

Requests, responses and their containing, element and generic argument types must be effectively public. The Host serialises these types across the MCP boundary and external consumers must be able to describe their contracts.

This rule applies to external plugin assemblies. Bundled Host tools may use internal transport contracts because their activation and serialisation boundary is controlled by the Host.

<a id="RWMCP009"></a>

## RWMCP009

### Handler instance state requires thread-safety review

An explicit instance field, writable instance property or instance event introduces state on a handler that may serve concurrent invocations. Stateless handlers are the supported default.

Remove the state when possible. If it is intentional, ensure that access is thread-safe and suppress the warning with a specific justification.

<a id="RWMCP010"></a>

## RWMCP010

### Avoid mutable static handler state

Non-constant, non-readonly static fields are shared across every handler invocation and workspace. Remove the mutable static state or protect it with an appropriate concurrency and lifetime design.

<a id="RWMCP011"></a>

## RWMCP011

### Handler field may own a disposable resource

A handler field has a type that implements `IDisposable` or `IAsyncDisposable`. This is a warning because field ownership cannot be inferred from its type, but catalogue-lifetime handlers must not retain resources that require an unsupported disposal lifecycle.

Prefer invocation-scoped ownership. Suppress the warning only when the field is demonstrably non-owning and safe for concurrent use.

<a id="RWMCP012"></a>

## RWMCP012

### Query tools cannot declare destructive behaviour

A query handler cannot set `RoslynToolAttribute.Destructive` to `true`. Query tools must be read-only; source changes belong in a mutation handler and must be returned as a `MutationCandidate`.

<a id="RWMCP013"></a>

## RWMCP013

### Observe the invocation cancellation token

A handler `ExecuteAsync` implementation does not meaningfully read or forward its cancellation token. Assigning the token to a discard does not count as observation.

Forward the token to cancellable Roslyn, I/O and asynchronous APIs, or call `ThrowIfCancellationRequested` at appropriate boundaries. A genuinely fast synchronous implementation may suppress this informational diagnostic with a specific justification.

<a id="RWMCP014"></a>

## RWMCP014

### Bound agent-facing query collections

A query publishes a raw array, list, set, dictionary, enumerable or asynchronous collection either as its response or through a public response property.

Expose a nullable integer limit with a positive declared default and apply `[Range(0, int.MaxValue)]` so negative values are rejected during request binding. An explicitly requested zero means that no items should be returned and must not be the declared default. Resolve omitted values with `ResultLimit.GetEffectiveValue`, apply the effective limit before constructing the response, and publish the result through `BoundedCollection.CreatePrebounded<TItem>` from `Roslyn.Workbench.Mcp.Workspace.Results`. Include `TotalCount` only when the complete count is already available cheaply.

This diagnostic validates the published contract shape. It cannot prove that collection discovery stopped at the limit or that the chosen limit and ordering are appropriate.

<a id="RWMCP015"></a>

## RWMCP015

### Plugin entry-point marker and contract must agree

A type decorated with `RoslynPluginAttribute` must be a concrete, non-generic `IRoslynPlugin` implementation. Conversely, every concrete `IRoslynPlugin` implementation must declare the attribute.

Abstract plugin base classes may omit the attribute because they are not composable entry points.

<a id="RWMCP016"></a>

## RWMCP016

### A plugin assembly cannot declare multiple marked entry points

An assembly must expose exactly one marked plugin entry point when it is a plugin package. Split independent plugins into separate assemblies and packages.

Assemblies that merely reference the Plugins package do not need to declare an entry point.

<a id="RWMCP017"></a>

## RWMCP017

### Declare the supported plugin API version

The API version supplied to `RoslynPluginAttribute` must equal the `PluginApiVersions.V1` constant from the referenced Plugins package.

Runtime loading remains authoritative because the installed Host determines which API version it supports.

<a id="RWMCP018"></a>

## RWMCP018

### Plugin identity metadata must not be blank

The plugin ID and display name supplied to `RoslynPluginAttribute` must contain non-whitespace text. Choose a stable, globally distinctive ID and a meaningful user-facing display name.

<a id="RWMCP019"></a>

## RWMCP019

### Tool metadata must decorate a handler

`RoslynToolAttribute` may only decorate a type that implements a closed `IQueryToolHandler<TRequest, TResponse>` or `IMutationToolHandler<TRequest>` contract.

Attribute-free handlers remain supported because fluent configuration can supply their complete metadata.

<a id="RWMCP020"></a>

## RWMCP020

### Use a dedicated immutable query-cache key

Every `IQueryResultCache` call must use a named, sealed, immutable reference type with stable value equality, normally a sealed record class implementing `IQueryResultCacheKey`. Put strings and other scalar semantic inputs inside that dedicated key rather than using them directly. Arrays, mutable collections, Roslyn snapshot objects, writable members and unsafe nested member shapes are rejected.

The analyser cannot determine whether every semantic input is present. Include every input that can change the computed value within the registered tool.

<a id="RWMCP021"></a>

## RWMCP021

### Cached value may be unsafe to retain

An `IQueryResultCache` value is clearly mutable, disposable, recursively contains an unsafe retained shape, or is a plugin result envelope whose transient failure state could become sticky. Cache only values that callers treat as immutable and that do not own resources.

This warning is intentionally suppressible when static analysis cannot see a valid immutability or ownership invariant. Give every suppression a specific justification. The Host still refuses an actual `IDisposable` or `IAsyncDisposable` value at runtime.
