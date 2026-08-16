# Unit 5 — Code Actions

Date: 2026-08-16

**Report status:** Completed.

## Scope and evidence

This review covered the complete current `Roslyn.Workbench.Mcp.CodeActions` implementation, its Workspace dependencies, Host catalogue visitors and adapters, dependency-injection registrations, controlled-provider support, unit/integration/audit tests, published-Host acceptance coverage and Code Action scenarios. It traced discovery, nested projection, opaque-reference creation and resolution, replay, Fix All preparation, operation evaluation, mutation-candidate production, staging, invalidation and reference consumption. The review used only current repository source and current normative documentation; it did not inspect Git history or earlier review material.

No test command was run for this unit. Existing tests were inspected as claimed evidence, not treated as fresh execution evidence.

## Composition, ownership and lifetimes

Code Actions remain an internal Host capability rather than part of the third-party plugin contract. Startup constructs the fixed three-tool catalogue, while the CodeActions project owns MEF provider composition, policy, discovery, replay and evaluation. Host adapters acquire neutral Workspace query or mutation contexts and map typed Code Action outcomes to the common MCP envelopes.

The major services and reference store are process singletons. References are opaque, bounded and expiring, and are indexed by Workspace identity, epoch, transaction and internal snapshot identity for lifecycle invalidation. Invocation contexts and Workspace leases remain per call. No incorrect dependency direction or lifetime mismatch was substantiated.

## Discovery and projection

`list-code-actions` validates the public snapshot precondition before resolving the document and UTF-16 span, collects applicable refactorings and diagnostic-backed fixes, applies provider policy, flattens nested leaf actions, creates bounded response items and stores replay recipes. Deterministic projection and result limiting are covered well for normal controlled providers and representative built-in providers.

The review found two discovery defects. First, sibling Code Fix root actions are all flattened with root path `[0]`, unlike refactoring roots, so distinct sibling roots can receive an indistinguishable replay recipe. Second, exceptions from an individual provider are not isolated at the provider boundary, allowing one faulty provider to abort discovery from every otherwise usable provider. They were allocated `RWMCP3-012` and `RWMCP3-013` and later validated independently.

## Replay, Fix All and staging

Opaque references retain internal Workspace snapshot identity in addition to the public snapshot tuple. Resolution replays the provider action, verifies identity and handles missing, changed or ambiguous output. Fix All preparation replays the originating Code Fix, obtains Roslyn's Fix All operation for the selected scope, evaluates and normalises its changed solution, bounds the affected documents, computes a candidate identity and stores a prepared reference. `stage-code-action` evaluates either a normal or prepared reference and returns a Workspace mutation candidate; the Host mutation adapter performs final staging and consumes the reference after success or a detected prepared-candidate change.

This path independently corroborates `RWMCP3-004`: the initial `list-code-actions` request is guarded only by the public Workspace/epoch/revision tuple before it interprets the requested document and span, whereas later replay benefits from the internal snapshot identity held by the opaque reference. It also strongly corroborates `RWMCP3-005`: operation evaluation accepts document additions and removals, while the final Workspace planner persists only source files and current add/delete coverage uses default SDK compile globs.

No separate defect was substantiated in Fix All scope selection, prepared-candidate identity, unsupported-operation rejection, reference expiry/invalidation or successful reference consumption.

## Contracts, configuration and Host consumers

The catalogue contains exactly `list-code-actions`, `prepare-fix-all` and `stage-code-action`; these remain separate from plugin discovery and plugin status. Host visitors materialise strongly typed query/mutation registrations, and the common binder/schema layer owns JSON admission. Reference lifetime and capacity, diagnostic/action/change limits and provider policy are declared and consumed consistently in the inspected paths.

Project selectors use the common Workspace resolver, so `RWMCP3-002` can select the wrong target-specific project before Code Action discovery. All staged Code Action source writes converge on the common transaction writer, so the containment race in `RWMCP3-007` also applies without creating a CodeActions-specific duplicate.

## Tests and evidence gaps

Current unit and integration tests cover controlled refactorings and fixes, nested actions, built-in composition, diagnostics, replay outcomes, expiry and invalidation, Fix All scopes, changing/failing prepared output, unsupported operations, encoding-sensitive changes and staging. The audit suite checks version-sensitive built-in provider compatibility, and acceptance/scenario coverage exercises the published workflow.

The material gaps are targeted: no controlled Code Fix fixture creates two sibling root actions with otherwise matching recipe identity, and no test proves that a throwing Code Fix or refactoring provider is isolated while another provider still contributes actions. Existing provider-failure tests do not establish both behaviours.

## Provisional candidates

### RWMCP3-012 — Sibling Code Fix roots can become permanently ambiguous during replay

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs:212-249`; `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/CodeActionResolver.cs:186-214`

The Code Fix discovery loop calls the flattening routine with root path `[0]` for every root action. If a provider registers two sibling root actions that otherwise share title, equivalence key and diagnostic identity but produce different operations, both published references carry the same structural recipe. Replay then finds multiple matches and returns ambiguity, so an action that was just offered cannot be staged. Use the actual root registration index and add a controlled provider end-to-end test covering sibling roots and replay.

### RWMCP3-013 — One throwing Code Action provider aborts discovery for all providers

**Severity:** P2  
**Confidence:** Medium-high  
**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:120-130,192-213`; `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs:145-160,173-224,349-364`

Exceptions from `ComputeRefactoringsAsync`, retrieving fixable diagnostic identifiers or `RegisterCodeFixesAsync` escape the per-provider loop. One third-party or built-in provider failure therefore turns the entire `list-code-actions` request into a generic Host failure and hides actions from unaffected providers. Add a cancellation-preserving provider fault boundary that discards the failed provider's partial results and continues with unaffected providers, with controlled throwing-provider coverage and bounded diagnostic reporting.

## Exported conclusions and reopenings

- `RWMCP3-002` affects Code Action project selection through the shared Workspace resolver.
- `RWMCP3-004` is corroborated at the initial request boundary; opaque replay references themselves retain stronger internal identity.
- `RWMCP3-005` is corroborated by accepted Code Action add/delete candidates.
- `RWMCP3-007` applies through the shared commit writer.
- Unit 6 must independently assess how provider faults are mapped and reported at the Host boundary.
- Unit 8 should verify the controlled-provider and published-workflow evidence claims and the two identified gaps.

