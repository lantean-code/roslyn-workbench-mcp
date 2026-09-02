# Architecture

Roslyn Workbench is a local stdio MCP server. The Host binds protocol requests; Workspace owns loaded solutions, execution leases and transactional writes; Plugins and CodeActions adapt their separate tool systems to those neutral services.

## Project boundaries

| Project | Responsibility |
| --- | --- |
| Abstractions | Minimal shared selectors, snapshot preconditions, results, validation and resolver/service contracts. It does not depend on implementation projects. |
| Workspace | Loading, addressable documents, selectors, snapshots, query services, caches, change detection, transaction history, durable commit and recovery. It does not depend on MCP, Plugins or CodeActions. |
| Plugins | Typed extension contracts, startup registration, execution adaptation, analysis and query-cache access. It does not depend on MCP or CodeActions. |
| Plugins.Core | Bundled inspection and ordinary mutation tools using the plugin path. |
| CodeActions | Internal catalogue, Roslyn provider composition, diagnostics, discovery, replay references, Fix All and candidate production. It does not participate in plugin discovery. |
| Plugins.Analyzers | Compile-time authoring rules; separate from the runtime dependency graph. |
| Host | Composition, startup, MCP schemas and binding, server-owned tools, four transport adapters, plugin loading and consented error reporting. |

The current project files and architecture tests enforce the dependency graph. Runtime projects target .NET 10; the authoring analyser targets `netstandard2.0`. The .NET SDK is pinned in `global.json`. The Host is distributed as a .NET tool; plugin authoring uses the separate contracts and analyser package rather than the Host's implementation assemblies.

## Composition and tool execution

The Host validates configuration, registers services, checks MSBuild and recovery prerequisites, loads the bundled and configured plugins, then publishes a fixed tool catalogue. Adding a plugin requires a restart. Plugin discovery validates metadata and collisions before handler materialisation. Server-owned and Code Action names remain reserved. Trusted plugins execute in-process; load contexts are dependency isolation, not a security sandbox.

Four closed generic transport adapters keep plugin query, plugin mutation, Code Action query and Code Action mutation paths distinct. Typed registration visitors preserve request and response types. Reflection needed for discovery is concentrated at startup. The Host alone owns MCP request binding, validation and result envelopes. Query handlers cannot stage mutations; mutation handlers return candidates, and the Host uses the acquired mutation lease to stage accepted results.

Input schemas retain SDK-generated member metadata while the Host applies its contract rules and shared guidance. Enum values on the MCP boundary are strings. Internal serializers need not use the transport settings. Published descriptions add useful guidance rather than repeating property names; request-level guidance is also projected into tool metadata for clients that omit root-schema descriptions. Snapshot guidance comes from the schema pipeline. Runtime validation remains authoritative even when a client simplifies JSON Schema.

## Workspace identity and lifetime

The process can retain multiple Workspace sessions, but only one owns the active transaction slot. A lease captures the effective immutable solution, Workspace identity, epoch and transaction revision. Outside a transaction, queries use the loaded baseline; inside one, they use the selected staged revision. Stale locations and symbols are rejected rather than reinterpreted against changed source.

The resolver is the public path to addressable project and document state. Generated and otherwise non-addressable source is filtered centrally; plugins do not receive the internal document-filter service. Evaluated documents outside the Workspace root remain queryable but read-only. Project-graph changes, linked files and supported languages remain explicit constraints rather than silently inferred mutation permissions.

External-change monitoring and input certification detect ordinary user edits. Reload replaces the relevant snapshot identity; transaction history is bounded and undo/redo must not let a reused ordinal validate an unrelated snapshot. Cross-process status is advisory for cooperating instances, and commit takes a Workspace-root lock. See the [operating model](operating-model.md) for the boundary around structural filesystem changes during application and recovery.

## Transactions and recovery

Staging changes immutable Roslyn solutions, not source files. Candidate validation checks snapshot, document and project graphs, linked-document consistency and writable containment. Preview shows the candidate; rollback discards it. Commit validates the original input state, prepares a bounded durable plan and recovery records, rechecks disk state, then applies create/replace/delete operations. Representation such as text encoding and line endings is preserved where applicable.

The final application and recovery-terminal phases are deliberately non-cancellable. Cancellation before application leaves the staged transaction recoverable; cancellation must not interrupt a partially applied durable operation. Recovery evidence is retained until the terminal state is safe. Startup recovery precedes MCP initialisation, and unresolved recovery is visible in status and blocks affected Workspace operations.

## Code Actions

`list-code-actions`, `prepare-fix-all` and `stage-code-action` share one workflow. Discovery flattens eligible leaves, applies provider and change-safety policy and stores bounded, expiring recipes. References are process-local, snapshot-bound handles, not portable serialized Roslyn objects. Fix All prepares a candidate and records its identity; staging replays against the originating snapshot and rejects stale or changed output. Final staging and commit use the same Workspace boundary as ordinary mutations.

Provider composition, supported action families and replay compatibility are checked by the separate Code Action audit. A Roslyn upgrade warrants that audit; routine pushes do not. Unknown or unsupported operations are not permission to write around transactions.

## Caches and diagnostics

Workspace query results, plugin query results and Code Action recipes are separate cache families with different ownership and admission units. Plugin cache registration owns keys and value admission; invocation-bound scopes cannot outlive their lease. Concurrent misses may share an in-flight computation, but exceptions and cancellations are not retained as results. Recursive same-key factory use is rejected. Snapshot and lifetime boundaries invalidate stale data rather than relying only on expiry.

Unexpected tool failures produce generic correlated responses and bounded local diagnostic records. Explicit report preparation projects an allowlisted immutable report and provider preview. Submission applies consent and optional exception-message removal at dispatch; it cannot replace the stored destination or add new payload fields. No report is sent just because an exception occurred. The Sentry provider is build configuration, not a runtime endpoint supplied by the caller; builds without it use the explicit stderr dispatcher. User guidance is in [Error reporting](../content/error-reporting.md).
