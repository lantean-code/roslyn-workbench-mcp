# Roslyn Workbench Whole-Project Roadmap Design

**Status:** Superseded on 2026-07-18 as an active implementation checklist.

This document remains a historical record of the dependency-led sequence used to build the initial system. It is not authoritative for the current architecture, supported catalogue or release backlog. Current unfinished work is maintained in [Future Tasks](../../FutureTasks.md), and the documentation intended for release starts at the [release documentation index](../../../README.md).

**Goal:** Define a dependency-led, testable implementation roadmap for the entire Roslyn Workbench MCP project, with one eventual release and an early internal deliverable focused on workspace and inspection capabilities.

**Chosen planning shape:** One release, one early deliverable, and multiple internal implementation stages sequenced by dependency, testability, and complexity.

## Closure Audit

The 2026-07-18 audit closed the two remaining stage boxes against the current repository and release scope. Later architecture decisions supersede original details such as the shared Contracts project, single-workspace product shape, fixed catalogue target and fixed response-size model; those historical details must not be used to infer the current public contract.

| Original stage or requirement | Current disposition | Evidence |
| --- | --- | --- |
| Stage 7 custom mutation and read-only catalogue expansion | Complete for the supported catalogue. Bundled inspection and mutation tools have family-owned unit and component coverage. | [Tool implementation matrix](../../RoslynMcpToolImplementationMatrix.md), [Tool test inventory](../../Tool%20Test%20Inventory.md) |
| Stage 7 MEF-backed catalogue expansion | Complete. Every built-in C# refactoring and code-fix family in the audited Roslyn source has a supported or intentionally hidden state; unsupported public-API-dependent families remain conditional capability work rather than an unfinished Stage 7 batch. | [Roslyn Code Action source analysis](../../RoslynCodeActionSourceAnalysis-2026-07-27.md), [Code Action compatibility results](../../IntegrationTestingStage6Results-2026-07-17.md) |
| Stage 7 reusable semantic and mutation infrastructure | Complete. Query tools use the shared Workspace projection/resolution boundaries, while mutations and Code Actions stage through the transaction pipeline. | [Architecture audit](../../ArchitectureAuditChecklist.md), [Test architecture re-audit](../../TestArchitectureReaudit-2026-07-18.md) |
| Stage 8 metadata, schema and contract hardening | Complete for the published surface. Contract tests own names, schemas and JSON shapes, and published-process acceptance samples catalogue publication and representative workflows. | [Tool test inventory](../../Tool%20Test%20Inventory.md), [Integration Testing Stage 8 results](../../IntegrationTestingStage8Results-2026-07-18.md) |
| Stage 8 operational diagnostics, plugin failures and recovery | Complete. Startup fallbacks, package validation and failure isolation, disabled/colliding plugins, persisted recovery and public status projection have owner-aligned and published-process evidence. | [Host architecture validation](../../HostArchitectureValidation.md), [Test architecture re-audit](../../TestArchitectureReaudit-2026-07-18.md) |
| Stage 8 cancellation, concurrency and bounded results | Complete at the functional and integration boundaries used for release confidence. Cancellation paths, isolated mutable state, bounded search and controlled concurrent test execution are covered. A deeper representative tool-performance programme remains P1 work, not an unchecked roadmap stage. | [Tool test inventory](../../Tool%20Test%20Inventory.md), [Integration Testing Stage 7 results](../../IntegrationTestingStage7Results-2026-07-18.md), [Future Tasks](../../FutureTasks.md#establish-and-execute-a-tool-performance-tuning-programme) |
| Stage 8 coverage and end-to-end verification | Complete at the architecture-programme level: 1,952 tests passed across Unit/Contract, component integration, published-Host acceptance and compatibility audit layers. The later partial-branch round added reachable coverage, corrected code-metrics behaviour and explicitly approved defensive Roslyn-only paths. | [Integration Testing Stage 8 results](../../IntegrationTestingStage8Results-2026-07-18.md), [Tool test inventory](../../Tool%20Test%20Inventory.md#partial-branch-reassessment) |
| Stage 8 operational and plugin documentation | Complete for the initial release-documentation baseline. The release index, getting-started, configuration, tool-discovery, workspace/transaction and plugin-authoring guides are separate from the historical engineering records. | [Release documentation index](../../../README.md) |

This closes the original roadmap as a release-decision blocker. It does not declare every desirable hardening or ecosystem improvement complete. The remaining performance, coverage, platform, plugin-authoring and engineering efficiency work is deliberately prioritised in [Future Tasks](../../FutureTasks.md).

## Planning Decisions

- The roadmap optimises for a balanced dependency-led hybrid approach rather than pure infrastructure-first or pure vertical-slice-first delivery.
- Deliverable 1 is a usable `workspace + inspection` server.
- After Deliverable 1, the next priority is the transaction platform before broader read-only analysis expansion.
- MEF-backed refactorings and code-action infrastructure should enter in the middle of the roadmap, after the transaction platform exists but before the full mutation catalogue is implemented.

## Target Product Shape

Roslyn Workbench is a local stdio MCP server with a contract-first tool surface, a single loaded writable workspace, snapshot-safe query semantics, transactional mutation semantics, and a plugin-owned tool model.

The solution architecture is intentionally split across five product projects:

- `src/Roslyn.Workbench.Mcp` The executable host, dependency composition root, MCP server bootstrap, and server-owned lifecycle tools.
- `src/Roslyn.Workbench.Mcp.Contracts` Shared request, response, schema, selector, result-envelope, and error contracts.
- `src/Roslyn.Workbench.Mcp.Workspace` Workspace loading, epoch management, transaction coordination, reload handling, commit, and recovery infrastructure.
- `src/Roslyn.Workbench.Mcp.Plugins` Plugin abstractions, registration, tool metadata, validation, and execution plumbing.
- `src/Roslyn.Workbench.Mcp.Plugins.Core` First-party query and mutation tool implementations that sit on top of the shared plugin and workspace abstractions.

The design docs describe an end-state catalogue of 80 tools. The roadmap should therefore prioritise stable shared seams that let the catalogue expand without revisiting host/workspace/contract fundamentals.

## Stage Model

The project should be planned as one release made up of internal implementation stages. Each stage should validate one architectural dependency chain and leave the repository in a state that is buildable and testable.

### External milestones

- `Deliverable 1` A usable `workspace + inspection` server with working startup, plugin registration, typed tool execution, structured results, workspace lifecycle operations, and a first set of read-only inspection tools.
- `Final release` The complete server architecture capable of supporting the documented catalogue, including transaction-safe staged mutations, MEF-backed action integration, commit/recovery workflows, and broader query/mutation coverage.

### Internal stages

These stages should be tracked as implementation checkpoints. Each box must be ticked only when that stage is fully complete and its defined verification work has passed.

- [x] `Stage 0: Environment and repository baseline`
- [x] `Stage 1: Contracts and shared result model`
- [x] `Stage 2: Plugin model, tool registry, tool executor, and MCP adapter`
- [x] `Stage 3: Workspace lifecycle and server-owned state model`
- [x] `Stage 4: Deliverable 1 inspection capability slice`
- [x] `Stage 5: Transaction platform and staged revision model`
- [x] `Stage 6: MEF composition and action staging infrastructure`
- [x] `Stage 7: Mutation tools and catalogue expansion` — closed by the 2026-07-18 audit
- [x] `Stage 8: Hardening, compatibility, and release readiness` — closed by the 2026-07-18 audit

## Sequencing Rules

- Host-owned lifecycle and persistence responsibilities must exist before plugin tools rely on them.
- Shared contracts, selectors, envelopes, and error codes should stabilise before many tools are implemented.
- Read-only end-to-end capability should validate the host, workspace, adapter, and plugin model before mutation work begins.
- The transaction platform should be completed before broad mutation implementation, because mutation correctness depends on staged revision semantics rather than on individual tool logic.
- MEF-backed integration should arrive early enough to validate provider discovery, composition, and action-token architecture before the server commits to many MEF-backed tools.
- Shared semantic primitives should be implemented once and reused across inspection, analysis, and mutation plugins.
- Test infrastructure should grow with each architectural layer instead of being deferred until the end.

## Stage Details

### Stage 0: Environment and repository baseline

**Purpose**

Establish a healthy development baseline so later stage failures reflect product defects rather than environment drift or incomplete bootstrap.

**Why it comes first**

The repository currently has the correct pinned SDK available through `global.json`, but Roslyn workspace diagnostics have already shown restore and workspace-health failures. That makes baseline validation a prerequisite for trustworthy development and testing.

**Scope**

- Confirm the pinned `.NET 10.0.100` SDK is used consistently by CLI and workspace tooling.
- Repair restore health so the solution loads cleanly and analyzer packages are available.
- Add any repository-wide package-management or shared build configuration needed before source code proliferates.
- Decide whether common package versions belong in root-level shared props/targets files before tool code exists in multiple projects.
- Establish the initial build, restore, and test command path required by repository instructions.

**Primary outputs**

- A repeatable healthy restore/build/test baseline.
- Any shared MSBuild configuration needed to reduce later churn.
- Diagnostic guidance for workspace loading failures and environmental preconditions.

**Primary tests**

- `dotnet restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- `dotnet build --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- `dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- Roslyn workspace diagnostics against the solution after restore health is fixed.

### Stage 1: Contracts and shared result model

**Purpose**

Create the contract surface that every server tool and plugin will rely on.

**Why it comes before adapter and tool work**

The design docs are explicitly contract-first. If requests, selectors, result envelopes, error codes, and schema models are unstable, every later tool implementation inherits churn.

**Scope**

- Define the core `ToolResult<TData>` outcome model.
- Define shared selector types such as document, project, location, symbol, scope, and snapshot precondition selectors.
- Define shared supporting contract types such as diagnostics, warnings, errors, required actions, document references, resolved locations, symbol references, and truncation metadata.
- Define initial server/workspace contract shapes required by Deliverable 1 and Stage 5 transaction work.
- Decide the internal contract organisation strategy inside `Roslyn.Workbench.Mcp.Contracts` so the catalogue can scale without giant files.

**Primary outputs**

- Stable shared DTOs and enums.
- Consistent error and required-action vocabulary.
- A schema-friendly representation of tool inputs and outputs.

**Primary tests**

- Contracts unit tests for discriminated outcomes and JSON serialisation/deserialisation.
- Schema-generation tests for representative tool contracts.
- Validation tests for selector invariants and contract guard rules.

### Stage 2: Plugin model, tool registry, tool executor, and MCP adapter

**Purpose**

Build the machinery that turns a validated plugin contract into a live MCP tool with typed execution and structured output.

**Why it comes before workspace features**

The user already identified plugin structure and tool executor as early dependencies, and the docs confirm that all query and mutation tools are plugins even when shipped in the main server package. Deliverable 1 cannot exist without this layer.

**Scope**

- Define plugin abstractions, registration model, metadata validation, and startup-time discovery rules.
- Implement a tool registry that resolves all enabled tools once at startup.
- Implement the tool executor that binds incoming requests to typed handlers and normalises outcomes.
- Implement the server-owned MCP adapter layer that exposes registered tools with titles, descriptions, schemas, and annotations.
- Define the execution context shape that plugins receive, keeping host-owned concerns out of plugin code.
- Establish how plugin load diagnostics surface through startup and `server-status`.

**Primary outputs**

- A validated plugin registration pipeline.
- A stable tool execution pipeline.
- MCP tool exposure that reflects contract metadata rather than ad-hoc host logic.

**Primary tests**

- Plugin validation tests for duplicate names, invalid metadata, and unsupported combinations.
- Tool executor tests for successful execution, validation rejection, and exception/fault normalisation.
- MCP adapter tests that assert schema publication and behavioural annotations.
- Host integration tests with a minimal test plugin.

### Stage 3: Workspace lifecycle and server-owned state model

**Purpose**

Implement the loaded-workspace state machine that everything else depends on.

**Why it comes before Deliverable 1 plugins**

Inspection tools need a trustworthy workspace abstraction, epoch tracking, document/project resolution, and consistent lifecycle/state errors before they can behave correctly.

**Scope**

- Implement workspace-open, close, status, and reload host operations.
- Model workspace lifecycle states, including unloaded, ready, transaction-active, conflicted, and out-of-date conditions.
- Implement document/project lookup and path normalisation rules.
- Add workspace epoch tracking and snapshot assertions required by location-based queries.
- Establish exclusive-operation and query-concurrency gating semantics described in the design docs.
- Build the host-owned representation of load diagnostics and effective workspace identity.

**Primary outputs**

- A workspace coordinator abstraction.
- A stable state/error model for loaded-solution operations.
- The server-owned lifecycle tool implementations for workspace status and management.

**Primary tests**

- Workspace project tests for open/close/reload/status state transitions.
- Path normalisation and selector resolution tests.
- Concurrency gate tests for query versus exclusive operation rules.
- Host integration tests for the lifecycle tool surface.

### Stage 4: Deliverable 1 inspection capability slice

**Purpose**

Ship the first usable server by proving that the host, adapter, workspace model, and plugin model can support real read-only Roslyn capability.

**Why this is Deliverable 1**

You chose `workspace + inspection` as the first usable milestone. This is the smallest capability slice that proves the server is genuinely useful without taking on transaction complexity too early.

**Scope**

- Implement `server-status`.
- Implement the workspace context inspection tools needed for a usable server, likely starting with `get-solution-structure`, `get-project-details`, and `get-document-options`.
- Implement a first semantic inspection set that maximises reuse and proves symbol/location infrastructure. The likely first group is:
  - `get-document-outline`
  - `search-symbols`
  - `resolve-symbol`
  - `get-symbol-info`
  - `go-to-definition`
  - `find-references`
  - `find-callers`
  - `find-implementations`
  - `get-diagnostics`
- Build the shared projections needed for document references, resolved locations, symbol references, and bounded collection results.

**Tool selection rule**

Within Deliverable 1, tools should be ordered by dependency and reuse rather than by catalogue order. Symbol resolution and projection utilities should land before tools that layer on top of them.

**Primary outputs**

- A usable server with a coherent read-only tool set.
- Reusable projection and lookup primitives for later analysis and mutation work.

**Primary tests**

- Plugin-core tests for each inspection tool.
- Integration tests over representative sample solutions.
- Boundary tests for empty workspaces, ambiguous selectors, snapshot mismatches, and result truncation.
- End-to-end host tests that invoke tools through the MCP layer rather than only through internal handlers.

### Stage 5: Transaction platform and staged revision model

**Purpose**

Implement the transaction infrastructure that mutation correctness depends on.

**Why it comes before mutation breadth**

The docs make the transaction platform central: mutations never write directly, query tools must read from staged solutions during a transaction, and commit/recovery semantics are host-owned. Implementing many mutation tools before this platform stabilises would create rework.

**Scope**

- Implement transaction start, preview, history, rollback, and commit host tools.
- Model immutable baseline capture and bounded staged revision history.
- Implement transaction revision tracking and snapshot-precondition handling across queries and mutations.
- Implement change-summary and diff-preview infrastructure.
- Implement conflict detection and external-change handling rules.
- Implement the durable commit/recovery protocol and recovery-status reporting.
- Make query execution aware of staged versus baseline solutions.

**Primary outputs**

- A transaction coordinator and revision journal.
- Stable change-preview and affected-symbol infrastructure.
- Commit/recovery semantics owned by the host/workspace layer.

**Primary tests**

- Workspace tests for transaction lifecycle transitions and revision history.
- Diff/preview tests on representative source edits.
- Commit/recovery tests, including interrupted or conflicting apply scenarios.
- Query-against-staged-solution tests.

### Stage 6: MEF composition and action staging infrastructure

**Purpose**

Validate the MEF-dependent architecture while the codebase is still small enough to change safely.

**Why it sits here**

You chose MEF as a mid-stage concern. That allows the transaction platform to exist first while still validating provider composition and code-action staging before too many tools depend on assumptions that might be wrong.

**Scope**

- Compose Roslyn refactoring/code-fix providers and validate their availability.
- Implement code-action discovery and action-token generation rules.
- Implement action-token revalidation against workspace epoch and transaction revision.
- Implement the first end-to-end path for `list-code-actions`, `stage-code-action`, and `stage-code-fix`.
- Establish provider-load diagnostics and the rule that missing MEF dependencies disable tools cleanly rather than silently changing behaviour.

**Primary outputs**

- A proven MEF composition pipeline.
- Action-token infrastructure integrated with transaction semantics.
- Clear enablement/disablement behaviour for MEF-dependent tools.

**Primary tests**

- Composition tests for provider discovery and failure reporting.
- Token validation tests for expiry, snapshot mismatch, and tampering.
- Integration tests that stage a real refactoring or code fix into a transaction.

### Stage 7: Mutation tools and catalogue expansion

**Closure:** Complete for the supported catalogue. The final classification and evidence are recorded in the [closure audit](#closure-audit); this stage does not require implementation of families blocked by unsupported public Roslyn APIs or host-only interaction models.

**Purpose**

Expand from the transaction platform into the broader mutation and analysis catalogue using the shared seams already validated.

**Scope**

- Implement custom mutation tools that rely on transaction infrastructure but not on MEF.
- Implement MEF-backed refactoring and generation tools once the action-staging path is stable.
- Expand read-only analysis and architecture tools that reuse Deliverable 1 symbol, location, and projection infrastructure.
- Group work by shared primitives to minimise churn. For example:
  - symbol/member/attribute/hierarchy family
  - operation/control-flow/data-flow family
  - dependency/impact/test-impact family
  - formatting/import-management family
  - custom mutation family
  - MEF mutation family

**Ordering rule**

Within this stage, tools should be batched by shared semantic building blocks and implementation source from the implementation matrix (`Core`, `Custom`, `MEF`) rather than treated as eighty unrelated tasks.

**Primary outputs**

- Incremental expansion toward the documented catalogue.
- Reusable semantic-analysis and mutation support infrastructure.

**Primary tests**

- Plugin-core tests grouped by tool family.
- Shared regression suites for selectors, projections, previews, and staged-solution semantics.
- Contract tests that assert tool metadata stays aligned with the contract catalogue.

### Stage 8: Hardening, compatibility, and release readiness

**Closure:** Complete as an implementation and release-readiness stage. The [closure audit](#closure-audit) records the later evidence and separates genuine follow-up work from this historical stage.

**Purpose**

Prepare the server for the final release by closing the gap between “implemented” and “reliable.”

**Scope**

- Audit all tool metadata, descriptions, schemas, and behavioural annotations against the contract docs.
- Harden diagnostics, warnings, and required-action flows for operational clarity.
- Validate concurrency, cancellation, and response-size rules under load.
- Verify plugin load diagnostics, disabled-tool behaviour, and recovery scenarios.
- Close test coverage gaps across host, workspace, plugins, and contracts.
- Finalise docs that explain operational limits and plugin behaviour.

**Primary outputs**

- A release-ready server with verified contract compliance.
- Confidence that the tool catalogue behaves consistently under realistic usage.

**Primary tests**

- Full restore/build/test validation.
- Cross-project end-to-end integration suites.
- Stress and fault-injection scenarios where practical.
- Contract-compliance regression suites.

## Cross-Cutting Architecture Priorities

These areas should be explicitly accounted for during planning because they cut across multiple stages and are easy to under-scope.

### Shared semantic primitives

The roadmap should reserve time for infrastructure that is not itself a public tool but is reused by many tools:

- document and project resolution
- symbol binding and stable symbol projection
- source span and location mapping
- bounded collection-result shaping
- change preview and diff summarisation
- snapshot validation
- provider diagnostics and capability gating

### Error model and operational semantics

The project depends on a consistent rejected/conflict/faulted model. This should not emerge piecemeal inside individual tools.

### Contract-to-implementation traceability

The design docs are unusually detailed. The roadmap should therefore preserve traceability between:

- tool catalogue entries
- contract DTOs and schemas
- plugin registrations
- tests that assert tool metadata and behaviour

### Sample-workspace strategy

Representative test solutions are a dependency for meaningful workspace, symbol, transaction, and MEF tests. The plan should account for them early enough that later stages are not forced to invent test data ad hoc.

## Testing Strategy By Layer

The implementation roadmap should keep tests aligned to the architectural layer being added.

- `Contracts` Serialisation, schema generation, validation invariants, and result-shape tests.
- `Plugins` Registration, metadata validation, execution pipeline, and adapter tests.
- `Workspace` Lifecycle, selector resolution, epoch/revision handling, transaction state, commit, and recovery tests.
- `Plugin Core` Tool-family behaviour tests over representative sample solutions.
- `Host integration` End-to-end MCP tool registration and invocation tests across workspace states.

The roadmap should avoid a test strategy where only the final stage exercises the full stack. Each stage should add tests that prove the newly introduced seam works in isolation and as part of the stack.

## Key Risks and Planning Implications

### Risk: contract churn

If the contract surface is implemented lazily while tools are added, the same selectors and envelope types will be repeatedly reworked. The roadmap mitigates this by front-loading Stage 1.

### Risk: host/plugin boundary erosion

It will be tempting to place convenience logic in the executable host. The roadmap mitigates this by defining plugin execution context and host-owned responsibilities early in Stage 2 and Stage 3.

### Risk: mutation-before-transaction shortcuts

Implementing refactorings before the transaction platform is stable would produce brittle direct-write or pseudo-transaction behaviour. The roadmap explicitly forbids this ordering.

### Risk: MEF assumptions proving wrong late

If provider composition and code-action staging are left until the end, the project may discover architectural gaps too late. The roadmap mitigates this with Stage 6.

### Risk: inadequate test fixtures

Many tools depend on realistic code patterns such as partial types, inheritance, overloads, diagnostics, and refactoring candidates. The roadmap should budget sample solutions and test support early instead of treating them as incidental.

### Risk: restore and workspace fragility

The current repository already shows restore/workspace issues. The roadmap therefore includes environment and restore health as a first-class stage rather than assuming the scaffold is production-ready.

## Recommended Planning Outcome

The implementation plan should be written as a staged roadmap that:

- uses the stage model above
- treats Deliverable 1 as `workspace + inspection`
- prioritises the transaction platform immediately after Deliverable 1
- introduces MEF composition mid-roadmap
- expands the remaining catalogue by shared primitive and implementation source
- keeps testing aligned to each layer and stage

This design should be sufficient to drive a detailed task-by-task implementation plan without first deciding the exact order of all 80 tools individually.
