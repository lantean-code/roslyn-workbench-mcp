# Mutation plugins

A mutation handler proposes a changed Roslyn solution. The Host validates and stages that proposal; the plugin never writes source files directly.

## Mutation flow

```text
Current invocation snapshot
          |
Resolve target and validate expectedSnapshot
          |
Create changed immutable Solution
          |
Return MutationCandidate
          |
Host validates and stages a transaction revision
          |
Preview or receipt-based review -> commit or rollback
```

## Request and handler

Derive the public request from `WorkspaceMutationRequest` and implement `IMutationToolHandler<TRequest>`. Register the handler during synchronous plugin configuration:

```csharp
configuration.AddMutationTool<ExampleMutationTool>()
    .WithName("example-mutation")
    .WithTitle("Example Mutation")
    .WithDescription("Stages an example source mutation.")
    .IsDestructive();
```

This registration fragment assumes an implementation of `ExampleMutationTool`; it is not a complete plugin. Start with the [query tutorial](../plugin-authoring.md) for the full project and entry point.

## Validate before interpreting source locations

Query request contracts derive from `WorkspaceBoundRequest`. Mutation request contracts derive from `WorkspaceMutationRequest`, which inherits the Workspace selector and adds the required `ExpectedSnapshot` precondition. The Host publishes that inherited precondition as a required, non-null input, so mutation plugins should not redeclare or weaken it.

The precondition is the complete opaque snapshot identity: workspace ID, workspace epoch, immutable-solution snapshot ID and nullable transaction revision. Plugin handlers consume the value supplied by the caller and must not synthesise snapshot IDs or rebuild a precondition from selected fields.

Before interpreting snapshot-sensitive selectors, handlers must use the request resolver's snapshot-aware methods or call `ValidateSnapshot` explicitly; Host then stages successful candidates through the transaction boundary.

Use the invocation's resolver to identify the intended document or symbol. Do not search by filename suffix or interpret a source span before validating its complete snapshot identity.

## Return the candidate

After constructing a changed immutable `Solution` from the invocation snapshot, return a candidate using this handler-body pattern:

```csharp
var candidate = new MutationCandidate
{
    CandidateSolution = changedSolution,
    Summary = "Describe the proposed source change",
};

return PluginExecutionResult.Success(candidate);
```

Here `changedSolution` is the solution produced by your transformation. An asynchronous handler returns the execution result directly; a synchronous `ValueTask` handler wraps it with `ValueTask.FromResult`.

`IQueryContext` and `IMutationContext` expose the same immutable Roslyn solution snapshot and read-only resolution and analysis capabilities. `IMutationContext` does not expose a stager. Returning a `MutationCandidate` is the only supported way for a plugin to propose source changes; Host validates and stages that candidate through the active transaction.

Host and Workspace capabilities come only from `IQueryContext` or `IMutationContext`; they are not injected into plugin constructors or copied into plugin providers. Mutation handlers return a candidate solution and summary, and Host stages the proposal through Workspace after the handler returns. Plugins do not receive the Host service provider, MCP objects, file writers, workspace coordinators or Code Action services.

## Source-change boundaries

Never call `CurrentSolution.Workspace.TryApplyChanges`, write source files directly or use reflection to bypass the Host. Plugins are trusted in-process code, not a sandbox.

An active transaction and a mutation-enabled operational mode are required. The Host applies the same snapshot, source-containment, generated-source and transaction checks used by bundled tools. A successful handler result is a proposal, not a persisted change.

Source creation, deletion and same-directory rename do not update project membership. Explicit project includes or excludes need a separate project-file update outside the plugin transaction.

The agent follows the [transaction workflow](../workspaces-and-transactions.md), including mode-specific approval and optional compiler-impact validation. Plugins must not implement their own commit, receipt or elicitation mechanism.
