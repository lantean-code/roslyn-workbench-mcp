# Workspace Workflow Refactor Design

**Date:** 2026-07-06

**Status:** Proposed and design-reviewed in conversation

## Goal

Refactor the server-owned workspace and transaction implementation so that:

- tool adapters remain in the host project;
- workspace lifecycle, transaction, and plugin execution workflows remain in the workspace project;
- no single replacement for `WorkspaceHostService` emerges;
- internal services own their own parameters, outcomes, and state boundaries;
- `ToolResult` remains tool-layer only;
- existing MCP behaviour and contracts remain unchanged.

## Context

The current refactor improved the server-owned tool surface, but the workspace layer still has a large concentration of responsibility in `WorkspaceHostService`. Although `WorkspaceLifecycleService`, `TransactionService`, and `WorkspaceCoordinator` now exist, they are mostly redirection layers over one large internal class.

This keeps too much behaviour, state mutation, selection logic, external change detection, plugin execution gating, transaction staging, and commit persistence in one place. It also leaves the workspace layer partially shaped around MCP tool concerns because workflow services currently return `ToolResult<T>` and tool-facing DTO-adjacent outcomes.

The next refactor should correct those boundaries while preserving existing runtime behaviour.

## Architectural Direction

Use a lean workflow and state architecture:

- one top-level workflow service per real user-facing responsibility;
- a dedicated execution-context workflow for plugin execution;
- small shared state and infrastructure services for genuinely shared mechanics;
- no facade over all workspace behaviour;
- no generic command bus, mediator layer, or pipeline framework;
- no validator hierarchy unless a validation rule is genuinely shared and benefits from extraction.

This is intentionally more structured than the current design, but avoids unnecessary architectural ceremony.

## Layer Boundaries

### Tool adapter layer

Project: `src/Roslyn.Workbench.Mcp`

Responsibilities:

- MCP tool metadata
- request DTO binding
- response DTO projection
- `ToolResult` construction
- mapping internal workflow outcomes to published MCP result contracts

Rules:

- This layer may depend on workspace workflow interfaces.
- This layer owns `ToolResult` and all MCP-facing result shaping.
- This layer may use shared `RequiredAction` values when projecting failures.

### Workspace workflow layer

Project: `src/Roslyn.Workbench.Mcp.Workspace`

Responsibilities:

- workspace lifecycle workflows
- transaction workflows
- plugin query and mutation execution context workflows

Rules:

- This layer must not return `ToolResult`.
- This layer must not depend on MCP response envelopes.
- This layer must not depend on tool request/response DTOs.
- This layer owns its own operation result family and workflow success types.
- Concrete service implementations in this layer should be `internal`.
- Interfaces in this layer should also default to `internal`.
- Promote an interface to `public` only when it must cross a project boundary for a legitimate consumer, such as plugin execution infrastructure that must create plugin query or mutation contexts.

### Workspace infrastructure and state layer

Project: `src/Roslyn.Workbench.Mcp.Workspace`

Responsibilities:

- mutable session state
- workspace selection
- lifecycle state transitions
- workspace loading
- manifest build and external-change detection
- snapshot validation
- mutation staging
- commit persistence and recovery

Rules:

- This layer must not depend on tool DTOs.
- Only explicit state services may mutate loaded workspace/session state.
- Only explicit commit services may write files or recovery records.
- Concrete infrastructure implementations should be `internal`.
- Infrastructure interfaces should also default to `internal` unless another project must consume them.

## Public Workflow Interfaces

The workspace project should expose only three primary top-level services:

- `IWorkspaceLifecycleService`
- `ITransactionService`
- `IWorkspaceExecutionContextFactory`

These become the only behavioural entry points used externally:

- server-owned tools call lifecycle and transaction services;
- plugin execution infrastructure calls the execution context factory.

`IWorkspaceCoordinator` should be renamed to `IWorkspaceExecutionContextFactory` to make its boundary explicit.

Accessibility rule:

- Keep these interfaces `internal` unless a concrete cross-project consumer requires otherwise.
- The default assumption is that lifecycle and transaction workflow interfaces stay internal because they are consumed through DI inside the host-owned server surface.
- `IWorkspaceExecutionContextFactory` may remain `public` only if the plugin execution path still requires cross-project access from the plugins infrastructure.

## Internal Service Set

### Workflow services

- `IWorkspaceLifecycleService`
  - owns `OpenAsync`, `ListAsync`, `CloseAsync`, `GetStatusAsync`, `ReloadAsync`
- `ITransactionService`
  - owns `StartAsync`, `PreviewAsync`, `MoveHistoryAsync`, `CommitAsync`, `RollbackAsync`
- `IWorkspaceExecutionContextFactory`
  - owns `CreateQueryContextAsync`
  - owns `CreateMutationContextAsync`

### Shared state and infrastructure services

- `IWorkspaceSessionStore`
  - owns mutable host snapshot state
  - owns locking
  - owns session reads and replacements
  - owns workspace registration and removal
  - owns transaction-owner tracking
  - owns workspace ID and epoch allocation

- `IWorkspaceSelector`
  - resolves `workspaceId` / `alias` / `path` into a selected session or structured failure

- `IWorkspaceLoader`
  - normalizes open paths
  - inspects project compatibility
  - loads `MSBuildWorkspace` and `Solution`

- `IWorkspaceChangeDetector`
  - builds input manifests
  - validates manifests
  - determines whether an external change has occurred

- `IWorkspaceStateTransitions`
  - owns legal lifecycle transitions
  - applies transitions such as external-change detection and transaction conflict

- `ISnapshotGuard`
  - validates snapshot preconditions

- `IMutationStagingService`
  - validates `MutationProposal`
  - creates change summaries
  - truncates redo history where required
  - appends staged revisions
  - persists updated transaction/session state

- `ITransactionCommitService`
  - validates final commit applicability
  - owns recovery record handling
  - writes and deletes source files
  - updates post-commit session state

- `IWorkspaceOperationResultFactory`
  - creates consistent workspace-layer failure outcomes
  - centralizes shared error-code and message construction without becoming a behavioural facade

## Result Boundary

The workspace layer must own its own result family.

Recommended types:

- `WorkspaceOperationResult<TOutcome>`
- `WorkspaceOperationStatus`
- `WorkspaceOperationError`
- `WorkspaceOperationContext`

### `WorkspaceOperationStatus`

Recommended statuses:

- `Succeeded`
- `Rejected`
- `Conflict`
- `Faulted`
- `NoChange`

### `WorkspaceOperationError`

Recommended fields:

- `Code`
- `Message`
- `RequiredAction`

`RequiredAction` remains a shared server concept and may be referenced by both the workspace layer and the tool layer. It should not force the workspace layer to depend on `ToolResult`.

### `WorkspaceOperationContext`

Recommended fields:

- `WorkspaceId`
- `WorkspaceEpoch`
- `TransactionRevision`
- optional diagnostics and warnings if the current runtime behaviour requires them

### Workflow success outcomes

Each workflow owns its own success payload:

- `WorkspaceOpenOutcome`
- `WorkspaceListOutcome`
- `WorkspaceCloseOutcome`
- `WorkspaceStatusOutcome`
- `WorkspaceReloadOutcome`
- `TransactionStartOutcome`
- `TransactionPreviewOutcome`
- `TransactionHistoryOutcome`
- `TransactionCommitOutcome`
- `TransactionRollbackOutcome`

The host tool layer then maps those internal outcomes to:

- `ToolResult<WorkspaceOpenData>`
- `ToolResult<WorkspaceListData>`
- `ToolResult<WorkspaceCloseData>`
- `ToolResult<WorkspaceStatusData>`
- `ToolResult<WorkspaceReloadData>`
- `ToolResult<TransactionStartData>`
- `ToolResult<TransactionPreviewData>`
- `ToolResult<TransactionHistoryData>`
- `ToolResult<TransactionCommitData>`
- `ToolResult<TransactionRollbackData>`

## Non-Negotiable Boundary Rules

- Tools may depend on workflows; workflows may not depend on tool DTOs or `ToolResult`.
- Workflows may depend on shared workspace infrastructure; infrastructure may not depend on tool DTOs.
- `IWorkspaceSessionStore` is the only service allowed to mutate loaded session state.
- `IWorkspaceExecutionContextFactory` is the only service allowed to create plugin query and mutation contexts.
- `IMutationStagingService` is the only service allowed to validate and stage `MutationProposal`.
- `ITransactionCommitService` is the only service allowed to write committed source files or recovery records.
- No extracted service may grow into a general-purpose replacement for `WorkspaceHostService`.
- Shared services should be introduced only where they remove real duplication without weakening ownership boundaries.
- Default accessibility for workspace-layer services and interfaces is `internal`, not `public`.
- Any `public` workspace interface or type should be justified by an actual cross-project requirement, not by test convenience or speculative reuse.

## Responsibility Migration From `WorkspaceHostService`

### Move to `WorkspaceExecutionContextFactory`

- plugin query context acquisition
- plugin mutation context acquisition
- operation-gate acquisition
- query and mutation lifecycle gating
- creation of `WorkspaceQueryContext`
- creation of `WorkspaceMutationContext`

### Move to `WorkspaceSelector`

- workspace selection resolution
- selector mismatch and not-found handling
- implicit single-workspace resolution

### Move to `WorkspaceSessionStore`

- `_snapshot`
- `_syncRoot`
- session reads
- session replacement
- workspace registration/removal
- transaction-owner tracking
- workspace ID allocation
- epoch allocation

### Move to `WorkspaceLoader`

- path normalization for open operations
- SDK-style compatibility checks
- `MSBuildWorkspace` load/open logic
- load diagnostics collection

### Move to `WorkspaceChangeDetector`

- input manifest creation
- input manifest validation
- external-change detection checks

### Move to `WorkspaceStateTransitions`

- application of lifecycle transitions
- external-change transition logic
- transaction-conflict transition logic

### Move to `SnapshotGuard`

- snapshot precondition validation

### Move to `MutationStagingService`

- `MutationProposal` validation
- source document allow-list validation
- change summary creation
- revision staging and redo truncation
- transaction/session persistence for staged changes

### Move to `TransactionCommitService`

- final commit validation
- recovery status interaction
- file writes, creates, and deletes
- post-commit state update

## Migration Sequence

Use the following implementation order to reduce behavioural drift:

1. Introduce the workspace-owned result model and a tool-side mapper from `WorkspaceOperationResult<TOutcome>` to `ToolResult<TData>`.
2. Rename `IWorkspaceCoordinator` to `IWorkspaceExecutionContextFactory`.
3. Introduce `IWorkspaceSessionStore` and move snapshot, locking, allocation, and session mutation there.
4. Introduce `IWorkspaceSelector` and move selection logic there.
5. Introduce `IWorkspaceLoader`, `IWorkspaceChangeDetector`, and `IWorkspaceStateTransitions`.
6. Move plugin context creation and mutation session gating into `WorkspaceExecutionContextFactory`.
7. Rebuild `WorkspaceLifecycleService` as a real workflow over the new collaborators.
8. Rebuild `TransactionService` as a real workflow over the new collaborators.
9. Introduce `ISnapshotGuard`, `IMutationStagingService`, and `ITransactionCommitService`.
10. Remove remaining behaviour from `WorkspaceHostService`.
11. Delete `WorkspaceHostService`.

## Constraints

- Preserve all current MCP tool names, metadata, request/response contracts, schemas, annotations, and runtime behaviour.
- Do not introduce MCP-specific abstractions into the workspace layer.
- Keep plugin execution separate from workspace lifecycle and transaction orchestration.
- Prefer explicit constructor injection everywhere.
- Keep classes focused and small enough that their responsibilities are obvious from their names.

## Risks

- state race regressions while moving lock ownership into `IWorkspaceSessionStore`
- duplicated selection or gating logic if workflow boundaries are applied inconsistently
- accidental leakage of tool DTOs or `ToolResult` back into the workspace project
- hidden coupling between plugin execution context creation and transaction staging
- replacement of one god service with several ambiguously named "manager" services

## Success Criteria

The refactor is successful when:

- `WorkspaceHostService` no longer exists;
- workspace lifecycle, transaction, and plugin execution each have explicit top-level ownership;
- the workspace layer no longer returns `ToolResult`;
- the tool layer is responsible for all MCP result projection;
- no single service becomes the new default dumping ground for unrelated behaviour;
- all existing MCP-facing behaviour remains unchanged.
