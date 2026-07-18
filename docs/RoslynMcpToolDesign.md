# Roslyn Workbench - Complete Tool Catalogue

## Purpose

This is the complete planned capability catalogue for a local, stdio-based
Roslyn MCP server. It describes the intended end state, not the first release.
A smaller initial release is acceptable, but its architecture must support this
catalogue without changing the core workspace, plugin, transaction or result
contracts later.

## Current Execution Surface Note (2026-07-02)

The current build now ships the following point 2 and point 3 items from this
catalogue:

- `get-code-context`
- `find-callees`
- `find-overrides`
- `get-symbol-dependencies`
- `get-symbol-dependents`
- `get-change-impact`
- `get-api-surface`
- `convert-expression-body`
- `add-null-checks`
- `get-control-flow-graph`, including populated `regions` data

The following point 2 catalogue entries remain explicitly deferred from the
current execution surface and must not be treated as registered tools in the
current build:

- `get-code-metrics`
- `find-unused-symbols`
- `find-duplicate-code`
- `get-dependency-graph`
- `find-dependency-cycles`
- `get-test-impact`
- `analyze-nullability`
- `analyze-async`
- `analyze-disposables`
- `move-type-to-file`
- `move-type-to-namespace`
- `convert-to-async`
- `convert-property`
- `convert-to-pattern-matching`
- `generate-constructor`
- `generate-tostring`

The following mutation families are not planned for implementation in this
server while they depend on non-public Roslyn services or internal IDE-only
generation paths. They remain part of the aspirational catalogue only and
should be treated as unavailable unless Roslyn exposes a new supported public
API path in the future:

- `move-type-to-namespace`
- `convert-to-async`
- `convert-to-pattern-matching`
- `generate-constructor`
- `generate-tostring`
- `extract-interface`
- `extract-base-class`
- `change-signature`
- `generate-equals-hashcode`
- `generate-overrides`
- `implement-interface`

Some of the deferred or not-planned mutation families above already have
planned request and result shapes in this document because they remain part of
the aspirational end-state catalogue. They are omitted from the registered tool
surface in the current build.

## Project Identity

The product name is **Roslyn Workbench**.

The canonical repository name is `roslyn-workbench-mcp`.

The NuGet package name is `Roslyn.Workbench.Mcp`.

The executable name is `roslyn-workbench-mcp`.

## Count and Scope

The target surface contains **82 tools**:

| Group | Tool count |
|---|---:|
| Server and workspace context | 9 |
| Semantic inspection and navigation | 19 |
| Analysis and architecture | 16 |
| Specific refactorings, generation and formatting | 28 |
| Roslyn code actions and transaction control | 10 |
| **Total** | **82** |

The existing `JoshuaRamirez/RoslynMcpServer` registry has 41 tools. The target
retains 40 of those operations, replaces `diagnose` with `workspace-status`,
and adds 41 tools.

The planned implementation source and dependency for each tool is recorded in
[RoslynMcpToolImplementationMatrix.md](RoslynMcpToolImplementationMatrix.md).

`Retained` below means the conceptual operation is retained. The new server may
reuse isolated Roslyn operation code where it is sound, but it will not retain
the existing host, direct-write, or incomplete rollback design.

## Public Surface Rules

- The server runs locally over stdio and starts with no loaded solution.
- It supports multiple loaded workspaces at once. Exactly one loaded workspace
  may own an active transaction for staged mutations.
- A writable workspace requires every loaded C# project to use the SDK-style
  project format. The server never writes project, props or targets files.
- The enabled plugin set is discovered once at server startup. Every enabled
  tool is returned by `tools/list`; the list does not change for the lifetime
  of that server process.
- Adding, removing or upgrading a manually installed plugin requires a server
  restart. There is no v1 hot loading, tool-on-demand mechanism, or dynamic
  tool-list notification.
- Tool availability does not vary with workspace or transaction state. A tool
  that cannot run returns a structured state error such as
  `NoActiveTransaction`, `TransactionConflicted`, or `WorkspaceOutOfDate`.
- Every workspace-executed request may include `workspace` selection by server
  `workspaceId`, caller-friendly `alias`, or canonical loaded `path`. The
  selector may be omitted only when exactly one workspace is loaded.
- Query tools do not write files. When a transaction is active, they query its
  staged working solution; otherwise they query the loaded workspace solution.
- Query tools may run concurrently, up to the configured query limit. A query
  cannot start while an exclusive workspace operation is active.
- Mutating tools require an active transaction. A successful mutation stages a
  new revision, returns a bounded preview, and never writes to disk directly.
- Mutations and workspace or transaction lifecycle operations require exclusive
  access. They do not wait for active queries or another exclusive operation;
  an unavailable operation gate returns `WorkspaceBusy` with `Retry`.
- `transaction-commit` is the only public operation that writes staged source
  changes to disk.
- A mutation, or a query that reuses a prior source location or symbol, includes
  the response's workspace epoch and transaction revision as a snapshot
  precondition. A stale span is rejected rather than reinterpreted at the same
  offset in a newer solution. Symbol names and metadata names are search inputs,
  not source identity.
- Collection and graph queries accept explicit named per-collection limits.
  When omitted, the server uses its startup-configured `DefaultMaxResults`,
  which defaults to `100`. `0` means “return none from this collection”.
  Results have a documented deterministic order. Top-level published
  collections should use `BoundedCollection<TItem>`, which carries `items` and
  `hasMore`. A larger limit recomputes from the start. There
  are no cursors or generic paging.
- Large code and diff results use explicit document and range selectors, or a
  focused follow-up call, rather than pagination.

## Existing Server Inventory

The existing registry contains the following 41 tools.

| Existing tool | Current purpose | Target disposition |
|---|---|---|
| `diagnose` | Check Roslyn/MSBuild/SDK and workspace health | Replace with `server-status` and `workspace-status` |
| `move-type-to-file` | Move a type into its own document | Retain and reimplement |
| `move-type-to-namespace` | Move a type to a namespace | Retain and reimplement |
| `rename-symbol` | Rename a resolved symbol and references | Retain and reimplement with `Renamer` |
| `extract-method` | Extract a selection to a method | Retain |
| `introduce-variable` | Stage one Roslyn introduce-variable leaf action | Retain |
| `extract-interface` | Extract an interface from a type | Retain |
| `extract-base-class` | Extract selected members to a base class | Retain |
| `introduce-parameter` | Convert an expression/local to a parameter | Retain |
| `inline-variable` | Replace local uses with its initializer | Retain |
| `change-signature` | Add, remove or reorder method parameters | Retain |
| `encapsulate-field` | Replace a field with a property | Retain |
| `convert-to-async` | Convert sync code to async | Retain |
| `convert-expression-body` | Toggle block/expression-bodied syntax | Retain |
| `convert-property` | Convert auto and full properties | Retain |
| `convert-foreach-linq` | Convert supported `foreach`/LINQ patterns | Retain |
| `convert-to-interpolated-string` | Convert concatenation/formatting | Retain |
| `convert-to-pattern-matching` | Modernise supported type-test/switch patterns | Retain |
| `generate-constructor` | Generate constructor from members | Retain |
| `generate-equals-hashcode` | Generate equality members | Retain |
| `generate-overrides` | Generate base-member overrides | Retain |
| `generate-tostring` | Generate `ToString` | Retain |
| `implement-interface` | Generate interface members | Retain |
| `add-null-checks` | Generate parameter guards | Retain |
| `add-missing-usings` | Resolve and add imports | Retain |
| `remove-unused-usings` | Remove unused imports | Retain |
| `sort-usings` | Sort imports | Retain |
| `format-document` | Format a document | Retain |
| `find-references` | Resolve references across the solution | Retain |
| `find-callers` | Find callers of a method | Retain |
| `find-implementations` | Find interface/abstract implementations | Retain |
| `go-to-definition` | Find source definitions | Retain |
| `search-symbols` | Search symbol names | Retain |
| `get-diagnostics` | Return compiler diagnostics | Retain |
| `get-code-metrics` | Calculate selected code metrics | Retain |
| `analyze-control-flow` | Analyse a selected region's control flow | Retain |
| `analyze-data-flow` | Analyse a selected region's data flow | Retain |
| `get-document-outline` | Return namespace/type/member hierarchy | Retain |
| `get-symbol-info` | Describe the symbol at a position | Retain |
| `get-type-hierarchy` | Find base or derived types | Retain |

## Complete Planned Tool Set

### Server and Workspace Context (9)

| Tool | Status | Purpose |
|---|---|---|
| `server-status` | New | Report server and MCP protocol versions, Roslyn/MSBuild availability, effective non-sensitive startup configuration, loaded tool count, plugin load diagnostics, and unfinished commit recovery state. It works without a loaded workspace. |
| `workspace-open` | New | Load an additional `.sln`, `.slnx` or `.csproj`, reject non-SDK-style C# projects, report load failures, and keep the resulting workspace available for selected queries or transactions. It does not start a transaction. If cross-instance status reports that the workspace is or may be in use elsewhere, agents treat it as query-only, use it only when necessary, expect results to become stale, and coordinate mutation ownership before starting a transaction. |
| `workspace-list` | New | Enumerate the currently loaded workspaces and identify which one, if any, owns the global transaction slot. |
| `workspace-close` | New | Dispose one selected loaded workspace after its active transaction has been committed or rolled back. |
| `workspace-status` | Replaces `diagnose` | Report one selected workspace's lifecycle state, project-load status, external-change state, active transaction state, revision capacity, reload requirement and advisory cross-instance state. A workspace that is or may be in use elsewhere is query-only unless mutation ownership has been coordinated. |
| `workspace-reload` | New | Reload one selected workspace after external changes. It is unavailable while that workspace owns an active transaction. |
| `get-solution-structure` | New | Return solution folders, projects, target frameworks and direct project relationships. |
| `get-project-details` | New | Return project properties, documents, direct references, analyzers and compilation options. |
| `get-document-options` | New | Return parse options, nullable context, language version, analyzers and editor-config-derived options for one document. |

`workspace-list` exists because multi-workspace loading requires an explicit
enumeration surface. MCP `ping` still provides standard connection liveness,
while `server-status` provides server diagnostics. There is also no separate
`get-project-dependencies`; direct relationships are available from the two
inspection tools above, while scoped transitive analysis belongs to
`get-dependency-graph`.

The descriptions for structural tools - `move-type-to-file`,
`extract-interface`, `extract-base-class`, file-relocating
`move-type-to-namespace`, and document-adding code actions or fixes - state
that they require the target project to include the resulting source files by
its own conventions. The server does not inspect or alter compile-item globs.
This requirement is visible through standard MCP tool descriptions, rather
than a client-specific metadata extension.

To keep `tools/list` usable for agents, the default server configuration omits
published `outputSchema` metadata and relies on the runtime structured result,
the contract catalogue, and concise description hints where needed. The server
does not publish a smaller or lossy “summary schema”: it either publishes the
real output schema in `Full` mode or omits `outputSchema` entirely.

### Semantic Inspection and Navigation (19)

| Tool | Status | Purpose |
|---|---|---|
| `get-document-outline` | Existing | Return the semantic hierarchy of a document. |
| `get-code-context` | New | Return a bounded code window with enclosing symbol, diagnostics and semantic context. |
| `search-symbols` | Existing | Search declarations by name, kind, accessibility, namespace or project. |
| `resolve-symbol` | New | Resolve a symbol at a position and return a stable selector and documentation ID. |
| `get-symbol-info` | Existing | Return complete metadata for a resolved symbol. |
| `get-symbol-members` | New | List members, including inherited and explicit-interface members when requested. |
| `get-symbol-attributes` | New | Return declared and inherited attributes with constructor and named arguments. |
| `go-to-definition` | Existing | Return source or metadata definition locations. |
| `find-references` | Existing | Find reads, writes, definition references and aliases across the solution. |
| `find-callers` | Existing | Return direct call sites and containing symbols. |
| `find-callees` | New | Return directly invoked symbols from a method or selected body. |
| `find-implementations` | Existing | Find interface and abstract-member implementations. |
| `find-overrides` | New | Find overrides of a virtual or abstract member. |
| `find-derived-types` | New | Find types derived from a specified type, with depth and project filters. |
| `get-type-hierarchy` | Existing | Return base, derived and implemented-interface relationships. |
| `find-overloads` | New | Return overloads and their parameter signatures. |
| `get-partial-declarations` | New | Return every declaration of a partial type or method. |
| `get-symbol-dependencies` | New | Return types, members and assemblies directly used by a symbol. |
| `get-symbol-dependents` | New | Return symbols that directly depend on a specified symbol. |

### Analysis and Architecture (16)

| Tool | Status | Purpose |
|---|---|---|
| `get-diagnostics` | Existing | Return compiler and configured analyzer diagnostics, filtered by scope, severity and ID. |
| `get-code-metrics` | Existing | Return projected logical lines, cyclomatic complexity, nesting depth, type coupling and a derived maintainability score for a symbol or scope. |
| `analyze-control-flow` | Existing | Return reachability, exit paths and return behaviour for a selected executable region. |
| `analyze-data-flow` | Existing | Return variables read/written, data in/out and captured variables. |
| `get-operation-tree` | New | Return a compact, typed `IOperation` tree for a selected expression, statement or member. |
| `get-control-flow-graph` | New | Return basic blocks, branches and regions for a method, lambda or local function. |
| `find-unused-symbols` | New | Identify candidate unused locals and members from compiler-unused diagnostics, with confidence reasons. |
| `find-duplicate-code` | New | Group identical executable blocks by normalized statement sequence. This is advisory, not an automatic refactoring. |
| `get-dependency-graph` | New | Build a bounded project, namespace, type or symbol dependency graph for the selected scope. |
| `find-dependency-cycles` | New | Detect cycles at project, namespace or type granularity within the selected scope. |
| `get-change-impact` | New | Combine references, callers, overrides, implementations and public surface to estimate blast radius. |
| `get-api-surface` | New | Describe exported API symbols for the selected solution, project, namespace or type. |
| `get-test-impact` | New | Identify likely impacted tests using built-in test-like type and method naming conventions. |
| `analyze-nullability` | New | Identify nullable-flow issues and unsafe dereferences from compiler nullability diagnostics. |
| `analyze-async` | New | Identify supported async antipatterns such as async methods without `await` and unawaited task-returning invocations. |
| `analyze-disposables` | New | Identify candidate undisposed local `IDisposable` or `IAsyncDisposable` values. This is advisory only. |

### Specific Refactorings, Generation and Formatting (28)

Each successful operation stages one new transaction revision and returns a
bounded preview. The operation does not write to disk.

| Tool | Status | Purpose |
|---|---|---|
| `move-type-to-file` | Existing | Stage Roslyn's move-type refactoring to move a selected type into its own Roslyn-chosen file within the same project. Arbitrary caller-selected target paths are not supported. |
| `move-type-to-namespace` | Existing | Not planned for this server while the current Roslyn move-to-namespace path still depends on internal service and options seams. |
| `rename-symbol` | Existing | Stage a solution-wide rename with configurable rename options. |
| `extract-method` | Existing | Stage extraction of a valid statement or expression selection. |
| `introduce-variable` | Existing | Stage one supported Roslyn introduce-variable leaf action. |
| `extract-interface` | Existing | Not planned for this server while the Roslyn implementation still depends on options-service interaction. This action family remains hidden from descriptor-based discovery unless a supported public API path becomes available. |
| `extract-base-class` | Existing | Not planned for this server while the Roslyn implementation still depends on options-service interaction. This action family remains hidden from descriptor-based discovery unless a supported public API path becomes available. |
| `introduce-parameter` | Existing | Stage promotion of an expression or local to a parameter and update call sites. |
| `inline-variable` | Existing | Stage inlining of a local variable. |
| `change-signature` | Existing | Not planned for this server while the required Roslyn feature service remains internal-only. This action family remains hidden from descriptor-based discovery unless a supported public API path becomes available. |
| `encapsulate-field` | Existing | Stage field encapsulation and reference updates. |
| `convert-to-async` | Existing | Not planned for this server while the public Roslyn surface only exposes narrow code-fix cases instead of the documented end-state async-conversion workflow. |
| `convert-expression-body` | Existing | Stage the supported Roslyn block-body or expression-body conversion offered at the selected declaration location. |
| `convert-property` | Existing | Stage Roslyn-backed conversion of a selected property between supported auto-property and full-property forms. |
| `convert-foreach-linq` | Existing | Stage one supported Roslyn foreach or LINQ conversion. |
| `convert-to-interpolated-string` | Existing | Stage interpolation conversion. |
| `convert-to-pattern-matching` | Existing | Not planned for this server while the relevant Roslyn fixes still depend on diagnostics not surfaced through the server's current public compilation and analyzer diagnostics path. |
| `generate-constructor` | Existing | Not planned for this server while the current Roslyn path remains a dialog-backed member-pick flow. |
| `generate-equals-hashcode` | Existing | Not planned for this server while the required Roslyn feature service remains internal-only. This action family remains hidden from descriptor-based discovery unless a supported public API path becomes available. |
| `generate-overrides` | Existing | Not planned for this server while the Roslyn implementation still depends on internal generation APIs. This action family remains hidden from descriptor-based discovery unless a supported public API path becomes available. |
| `generate-tostring` | Existing | Not planned for this server while no supported public Roslyn generation seam has been identified for this workflow in the current build. |
| `implement-interface` | Existing | Not planned for this server while the required Roslyn feature service remains internal-only. This action family remains hidden from descriptor-based discovery unless a supported public API path becomes available. |
| `add-null-checks` | Existing | Stage the supported Roslyn parameter null-check refactoring at the selected parameter location. |
| `add-missing-usings` | Existing | Stage import additions. |
| `remove-unused-usings` | Existing | Stage import removal. |
| `sort-usings` | Existing | Stage import ordering. |
| `format-document` | Existing | Stage document formatting using the loaded workspace options. |

### Roslyn Code Actions and Transaction Control (10)

| Tool | Status | Purpose |
|---|---|---|
| `list-code-actions` | New | List applicable installed Roslyn refactorings and code fixes at a position or range, but only for the built-in families that this server build has explicitly audited. Each returned action carries execution metadata describing whether it is replayable, parameterised or unsupported. |
| `describe-code-action` | New | Revalidate one discovered action and return its descriptor plus any preflight context needed before a dedicated executor tool can run. |
| `stage-code-action` | New | Revalidate and stage a selected replayable refactoring action into the active transaction. Parameterised actions are rejected and must use a dedicated executor when one lands. |
| `stage-code-fix` | New | Revalidate a diagnostic and stage a selected code fix into the active transaction. |
| `stage-fix-all` | New | Stage a selected fix across document, project or solution scope, subject to configured caps. |
| `transaction-start` | New | Start a transaction on one selected workspace, capture its immutable base solution, and create an empty staged revision history. It is rejected if another loaded workspace already owns the global transaction slot. Agents check `workspace-status` first and do not mutate a workspace that is or may be in use by another instance unless mutation ownership has been coordinated. |
| `transaction-preview` | Replaces `get-change-set` | Return changed-document and affected-symbol summaries for the current staged revision; return a detailed diff only for an explicitly selected document. |
| `transaction-history` | New | Move the current revision backward or forward using an `undo` or `redo` direction. It exposes the bounded revision count and remaining capacity. |
| `transaction-commit` | Replaces `apply-change-set` | Recheck the final derived file manifest against the transaction baseline, then run the durable apply-and-recover protocol without compiling the solution. |
| `transaction-rollback` | Replaces `discard-change-set` | Discard all staged revisions and return the workspace to its loaded baseline without writing files. |

`transaction-history` deliberately contains the symmetric undo and redo
operations because they share the same bounded revision journal, selector and
result contract. It is one self-contained transaction plugin tool, not a
generic command router.

Action IDs are opaque, snapshot-bound tokens with a bounded lifetime. They
contain the provider identity, diagnostic identity, equivalence key and nested
action path needed to reproduce the action. The server never retains a
provider-created `CodeAction` instance between requests; action-cache capacity
is therefore zero. When staging, it
re-runs the named provider and accepts exactly one matching action; expiry,
snapshot mismatch, no match, or multiple matches return `ActionExpired` or
`ActionAmbiguous` rather than guessing.

## Transaction Behaviour Required by the Catalogue

- A transaction has one immutable base solution and one staged working
  solution. It is associated with one loaded workspace session, not exposed as
  a public transaction ID.
- The host enforces a single global mutation owner. At most one loaded
  workspace may be in `TransactionActive` or `TransactionConflicted` at a
  time.
- The baseline does not consume revision capacity. Each successful mutating
  operation adds one revision. The server exposes the configured maximum,
  current revision count and remaining count in `workspace-status` and all
  mutation/history responses. `MaxTransactionRevisions` defaults to `20`.
- When the configured revision capacity is reached, the next mutation is
  rejected. The server never silently discards history. A new mutation after
  undo drops the redo branch and frees its capacity.
- Each revision is a Roslyn `Solution` snapshot, which shares unchanged
  structures with adjacent revisions. The configured revision cap controls
  history length but is not an absolute memory guarantee; the server may also
  reject an exceptional operation that would exceed an explicit resource cap.
- Every staged mutation supplies a host-derived change summary together with a
  compact `MutationPreview` summary. `transaction-preview` returns the
  changed-document summaries for the active revision and can additionally
  include a bounded single-document diff.
- The server builds a `WorkspaceInputManifest` at load/reload. It includes
  source documents; solution, project and imported build files; editorconfig,
  additional and analyzer-config documents; resolved metadata/analyzer paths;
  and package assets that affect the loaded workspace. Watchers set a cheap
  dirty hint, but are not authoritative. Before semantic work, the coordinator
  validates the relevant manifest entries using metadata and content
  fingerprints where metadata changed. A document semantic operation validates
  its project and compilation dependency closure; a solution-wide operation
  validates the full manifest. A mismatch transitions `Ready` to
  `WorkspaceOutOfDate` or `TransactionActive` to `TransactionConflicted`.
- After a mutation plugin produces a candidate solution, the coordinator first
  validates a strict solution delta allow-list. It permits only regular C# source
  document text edits and validated create/delete operations. It rejects project
  membership, references, parse/compilation options, analyzers, analyzer-config
  and additional documents, document metadata, and project-file changes. It then
  derives the complete affected-file manifest and compares every created,
  replaced or deleted source-document path with the immutable transaction
  baseline. This includes all reference documents affected by a rename. A
  mismatch discards the candidate and transitions to `TransactionConflicted`; no
  revision is staged.
- Every created, deleted or relocated source document must have a canonical path
  beneath its owning project's directory. Absolute paths, traversal and symlink
  escapes are rejected. The transaction manifest contains only these approved
  source-document changes; project, props and targets configuration remains
  outside the server's scope.
- The baseline comparison always uses the original transaction solution, not
  the current staged revision. A document changed by an earlier staged mutation
  still has its original text on disk.
- Commit repeats the same comparison for the final derived manifest, protecting
  against changes made after a revision was staged. A conflicted transaction
  must be rolled back, the workspace reloaded, and the work repeated.
- Git history, comparisons with arbitrary past versions, and public recovery
  mechanisms are deliberately outside this server. Any journal, temporary
  file, backup and crash-recovery logic needed to make commit durable is an
  internal implementation concern.

### Workspace lifecycle and transaction capabilities

The in-memory `Stateless` machine represents only durable workspace lifecycle
states. It does not represent in-progress operations, which are owned by the
workspace operation gate.

| State | Meaning |
|---|---|
| `Unloaded` | No workspace is loaded. |
| `Ready` | A workspace is loaded with no active transaction. |
| `TransactionActive` | A transaction owns a baseline and staged working solution. |
| `TransactionConflicted` | Workspace-input manifest validation found an external change that invalidated the transaction. It can be previewed or rolled back but not mutated or committed. |
| `WorkspaceOutOfDate` | Workspace-input manifest validation found an external change with no active transaction. Reload is required before semantic work. |

For one workspace session, `workspace-open` creates it in `Ready`;
`transaction-start` transitions `Ready` to `TransactionActive`; a detected
baseline mismatch transitions `Ready` to `WorkspaceOutOfDate` and
`TransactionActive` to `TransactionConflicted`. A normal rollback returns
`TransactionActive` to `Ready`; rollback from `TransactionConflicted`
transitions to `WorkspaceOutOfDate`. A successful commit transitions
`TransactionActive` to `Ready`. There is no `Committing`, `Querying` or
`Mutating` lifecycle state.

The transaction maintains a current `TransactionCapabilities` snapshot rather
than encoding every revision combination as a state. It contains revision
count, current revision, remaining capacity, `CanMutate`, `CanUndo`, `CanRedo`,
`CanCommit` and `CanRollback`. It is updated when revisions are staged, undone,
redone, rolled back or externally conflicted. A mutation request reads this
maintained data before execution; request-specific semantic checks, such as
selector validity and whether Roslyn can perform the requested refactoring,
remain the responsibility of the coordinator and plugin.

### Commit durability and recovery

`transaction-commit` applies staged source changes directly to the workspace
files. It does not provide multi-file atomic visibility: observers can see an
intermediate set of files while a commit is being applied.

Stage 5 currently persists recovery status records under the configured
`StateDirectory`, in `recovery/<commit-id>.json`. Commit writes `Prepared`
before it starts file application, updates the record to `Applying`, applies
document create/replace/delete operations, and removes the record on success.
If an I/O or access failure occurs while applying files, the server replaces
the record with `RecoveryIncomplete` and returns `ResolveRecovery`.

On startup, `server-status` reports any remaining recovery records. While a
solution has an unresolved record for any non-terminal recovery state,
`workspace-open` refuses to load it. Stage 5 does not yet implement durable
per-file backup manifests, automatic restore, or the `.vs/.../commits`
staging-area protocol described for later stages.

## Architectural Implications

### Startup configuration

Version one requires no configuration file. The stdio host receives settings
through command-line arguments and environment variables, with command-line
arguments taking precedence over environment values, and environment values
taking precedence over built-in defaults. Configuration is loaded and validated
once during startup; changing it requires a server restart.

The initial settings are repeatable `plugin-directory`, `default-max-results`,
`code-action-token-lifetime`, `max-transaction-revisions`,
`max-concurrent-queries` and `state-directory`. Each has an
environment-variable equivalent. `server-status` reports the effective
non-sensitive configuration and plugin load results so an agent can reason
about the server's current limits and tool set. The built-in defaults are
`DefaultMaxResults = 100`, `CodeActionTokenLifetime = 5 minutes`,
`MaxTransactionRevisions = 20` and `MaxConcurrentQueries = 2`.
`StateDirectory` defaults to the host temporary directory under
`roslyn-workbench-mcp-state`.

### MEF plugin composition

Ordinary query and mutation tools are plugins; internal Code Action tools are
not. Plugins.Core remains a normal Host reference but its `BundledCorePlugin`
entry point is exported and configured through the same MEF and materialisation
pipeline as third-party plugins. External `plugin-directory` values are search
roots. Each immediate child is one package containing exactly one assembly with
exactly one `RoslynPluginAttribute`; other DLLs are dependencies. Discovery is
not recursive and no sidecar manifest is used.

Host reads managed PE metadata before loading plugin code. The marker supplies
the stable ID, display name and exact supported API version, while
`AssemblyInformationalVersionAttribute` supplies SemVer. Malformed metadata,
incompatibility, invalid contracts, duplicate identities, deterministic name
collisions, composition failures or load failures disable the whole plugin.
The server continues to start, and `server-status` preserves its existing JSON
shape while reporting enabled warnings and disabled-plugin diagnostics. Code
Actions remain absent from plugin status and the tool list does not change
after startup.

Plugins may register only `Query` and `Mutation` tools. Workspace and
transaction lifecycle tools are server-owned and cannot be replaced or
extended by a plugin. A plugin receives an immutable Roslyn `Solution` and
approved query services, but never the `MSBuildWorkspace`, transaction
coordinator, state machine, file writer or commit journal.

Each plugin entry point exposes only
`Configure(IPluginConfiguration)`. `AddQueryTool<THandler>()` and
`AddMutationTool<THandler>()` constrain handlers to the corresponding marker
interface and public parameterless construction, capture a typed handler factory,
and return concrete fluent metadata builders. `RoslynToolAttribute` provides handler metadata; fluent
values override attribute values. Configuration and its builders freeze when
`Configure` returns. Host validates every handler and all collisions before it
constructs one handler per enabled tool. Handlers must be thread-safe and must
not own disposable resources.
Expected plugin-authoring validation failures accumulate as structured diagnostics
with stable IDs. A plugin with any error is atomically disabled, while warnings are
retained on an enabled plugin. Exceptions are reserved for unexpected loading,
composition, construction and reflection failures rather than validation flow.
Invocation-scoped services are available through the execution context. The
registry uses reflection only at startup to discover the closed generic handler
contract and materialise its corresponding closed registration; handler construction
uses the captured typed factory. A `RegisteredTool` is the sole internal
source of truth for a tool's plugin identity, name, description, behaviour,
annotations, request and response CLR types, generated JSON schemas, and
invocation delegate. Tool names are globally unique; invalid handler contracts,
incompatible API versions and name collisions fail plugin loading.

Configured handlers declare their tool name and description through attributes or fluent metadata,
while their implemented generic interface determines whether they are a query
or mutation. The server does not automatically expose every attributed type in
an assembly. This prevents helpers or unfinished handlers becoming public tools
by accident.

The server uses the official .NET MCP SDK and generic hosting with stderr
logging. It does not use a custom JSON-RPC loop. The plugin API is independent
of the SDK's attribute-scanning registration model: every query and mutation
tool is exposed through a server-owned `PluginMcpServerTool`, built from its
`RegisteredTool` at startup. This is a custom `McpServerTool` adapter, not a
custom JSON-RPC handler.

`PluginMcpServerTool` publishes the `ProtocolTool` name, description,
annotations, input schema, and optional output schema from `RegisteredTool`.
When called, it deserializes the MCP argument object using the server's
configured JSON serializer options and passes the typed request through the
closed generic Host adapter. `PluginExecutionContextFactory` then acquires the
required query or exclusive lease, performs lifecycle, transaction,
external-change and selector checks, constructs the plugin query or mutation
context, invokes the retained typed handler, and
converts the internal normalized tool result into the published structured
`CallToolResult` content.
The plugin handler is never an MCP endpoint and receives neither raw protocol
arguments nor transport objects.

`McpServerTool.Create(MethodInfo, target, options)` is still useful for simple
server-owned tools, provided their target is a thin server adapter that enters
the same command pipeline. It is not the plugin execution adapter: it would
bind arguments and directly invoke its target before the host could construct
the plugin context. A custom `McpRequestHandler` is unnecessary in v1 because
it would take responsibility for broader JSON-RPC routing and error handling
without adding value over the per-tool adapter.

### Workspace and transaction coordination

The server owns a single `MSBuildWorkspace`, on-demand external-change
detection and a transaction coordinator. The coordinator wraps a lightweight
`Stateless` state machine and a non-waiting workspace operation gate. Plugins
do not know about the state machine, locking, disk writes or rollback machinery.

The operation gate permits concurrent query leases, up to a configured maximum,
or one exclusive lease. `MaxConcurrentQueries` defaults to `2`. Queries
acquire a shared lease and run against the current complete effective
`Solution`, including the staged working solution
when a transaction is active. Mutation, transaction lifecycle, reload, close
and commit operations acquire the exclusive lease. No request waits in a
server-side queue: an attempted shared or exclusive lease that cannot be
acquired immediately returns `WorkspaceBusy` and `Retry`. An exclusive lease
is held for the whole operation, ensuring every mutation begins from a stable
revision with no query in flight.

Cross-instance status remains advisory rather than an operation gate. When
`workspace-open` or `workspace-status` reports another live instance,
unreadable live-instance data or unavailable instance status, an agent treats
the workspace as query-only, uses it only when necessary and expects results to
become stale as the other instance changes the workspace. The agent coordinates
mutation ownership before starting a transaction; the Host does not infer that
coordination or reject `transaction-start` solely from advisory instance state.

The tool executor resolves the plugin and delegates queries or candidate source
changes to the coordinator. Long-running Roslyn work occurs outside state entry
and exit callbacks, but while its operation lease is held; the coordinator
advances the state only after the work succeeds or fails. Query plugins return
structured data. Mutation plugins return a `MutationCandidate`: a candidate
changed `Solution`, a concise summary, warnings and optional intended
changed-symbol selectors. The coordinator verifies that the candidate belongs to
the current workspace, accepts only its allow-listed source-document delta,
derives the authoritative diff, stages the revision and enforces transaction
limits. Cancellation is honoured while a query or candidate is being computed;
once a state transition begins, the host completes it and reports the resulting
revision through normal status/result contracts.

### Shared contracts

Use common, strongly typed contracts for:

- `IToolExecutionContext`: immutable current `Solution`, snapshot identity,
  selector-resolution helpers, result limits and invocation-scoped logging.
- `IQueryContext` and `IMutationContext`: extend `IToolExecutionContext`.
  They remain intentionally similar; the tool's query or mutation contract,
  rather than ambient services, determines what it is permitted to return.
- `DocumentSelector`: a workspace-local Roslyn `DocumentId`, with an optional
  normalised workspace-relative path for JSON and human readability.
- `TextSpanSelector`: a document selector plus zero-based UTF-16 `start` and
  `length`, directly matching Roslyn's `TextSpan` model.
- `TextSelectionSelector`: an agent-friendly input locator containing a
  document selector, copied selected text, and optional short text before and
  after the selection. The host resolves it against the current effective
  source text before invoking Roslyn.
- `SnapshotPrecondition`: the workspace epoch and optional transaction revision
  from a prior result. It is required for mutations and prior-location queries.
- `SymbolSelector`: a Roslyn-derived source location and text span. A
  documentation comment ID is permitted for a query only when it resolves to a
  single source symbol; it cannot name locals or one declaration of a partial
  symbol. Symbol names and metadata names are search inputs, not identity.
- `ScopeSelector`: solution, Roslyn `ProjectId`, document, or selected project
  set.
- `ProjectRelativePath`: a target path relative to the owning project's
  canonical directory. It rejects absolute paths, traversal and symlink escape.
- `ResolvedLocation`: a response model containing document identity, text span,
  display line/column information and the effective solution snapshot identity.
  It is valid only for that snapshot; a staged change or reload requires the
  caller to supply its snapshot precondition or resolve it again.

`TextSelectionSelector` is a convenience only. A unique match resolves to a
canonical `TextSpanSelector`; no match returns `SelectionNotFound`; multiple
matches return `AmbiguousSelection` and candidate locations. The server never
guesses which repeated text the caller intended. `resolve-symbol` accepts this
selector so an agent can turn copied source text into a canonical location and
symbol without calculating a character offset itself.
- Published tool response: a family-specific structured MCP payload with a
  shared machine-readable failure base and compact success projection.
- `MutationCandidate`: a plugin-produced candidate changed `Solution`, summary,
  warnings and optional intended changed-symbol selectors. It does not contain
  diffs, content hashes, transaction state or disk-write instructions.
- `MutationResult`: operation summary, revision information and preview.

The host owns all MCP response construction. Query plugins return their typed
data; mutation plugins return `MutationCandidate`. The candidate is an internal
pre-staging value and is never published as the MCP response; successful
mutation tools publish `MutationData`. Plugins never create
canonical diffs, staged revisions, validation records or disk-write plans.

### Tool response and error contract

Every workspace, transaction and plugin-tool invocation now publishes a compact
family-specific payload within one universal success envelope. A plugin
must still return structured data; it must not substitute prose for structured
results or invent an incompatible success or error shape.

All tools share the same minimal failure and continuation base:

```json
{
  "ok": false,
  "error": {
    "code": "SnapshotMismatch",
    "message": "The request snapshot does not match the current workspace snapshot."
  },
  "next": "resolveTargetAgain"
}
```

Every successful result publishes its family-specific payload under the same `data` property:

- direct lifecycle and status tools publish `{ ok: true, data: { ...response dto... } }`
- query tools publish `{ ok: true, data: { ...response dto... } }`
- staged mutations publish `{ ok: true, data: { staged, summary?, transaction? } }`

This gives clients a uniform outer parsing rule without restoring the verbose internal `ToolResult<TData>` envelope. The payload within `data` remains compact and specific to the tool family.

This keeps the default path compact:

- bounded collections carry their own truncation metadata
- query DTOs keep heavy branches behind explicit request flags and named limits
- status tools default to smaller projections and expose expanded detail on
  request
- mutation success confirms staged state by default rather than returning a
  universal change envelope

`next`, when present, is a machine-readable continuation hint such as
`OpenWorkspace`, `StartTransaction`, `ReloadWorkspace`, `ResolveTargetAgain`,
`CommitOrRollback`, `ReduceTransactionHistory` or `Retry`. It guides an agent
without replacing the error code as the authoritative contract.

Roslyn diagnostics use a common representation aligned with Roslyn's own
model: diagnostic ID, severity, message, document identity and text span.
Diagnostics are retrieved only through an explicit query such as
`get-diagnostics`; mutation, preview and commit operations do not implicitly
compile the solution.

At the MCP boundary, the host serializes the real published response family for
each tool and sets `isError` to `false` for successful results and to `true`
for rejected, conflict and faulted results. JSON-RPC or MCP protocol errors,
including malformed protocol messages, remain protocol-level errors rather than
fabricated tool results. This preserves a recoverable, machine-readable result
for valid tool calls while keeping transport failures separate.

## Deliberate Public Exclusions

- Build, test, debug, terminal and Git operations. They belong to the host
  client, CI tooling or dedicated MCP servers.
- Arbitrary text replacement and direct file writes. They bypass semantic
  analysis, staging and transaction safeguards.
- Public support for comparing arbitrary historical versions, candidate
  refactoring alternatives or Git-style recovery. Git is the appropriate tool
  for those workflows.
- Cloud/HTTP hosting, remote checkout, multi-user tenancy and repository
  sharding.
- Plugin downloads, NuGet acquisition, hot loading and on-demand tool
  discovery.
- Visual Studio automation, which would make the server Windows and IDE
  dependent.

## Sources

- Existing tool registry and README: https://github.com/JoshuaRamirez/RoslynMcpServer
- Roslyn workspace model: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-workspace
- Roslyn semantic APIs: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/semantic-analysis
- Symbol search and references: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder
- Rename API: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.rename.renamer.renamesymbolasync
- Control-flow graph API: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.flowanalysis.controlflowgraph
- MCP C# SDK tools: https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html
- MCP C# SDK `McpServerTool`: https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerTool.html
