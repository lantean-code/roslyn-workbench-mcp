# CodeActions Architecture Validation

Date: 2026-07-16

Status: Complete

## Purpose

This document records the post-refactor architecture validation of `Roslyn.Workbench.Mcp.CodeActions`. It covers the five remaining validation areas and the two lower-risk checks identified after removing the query and mutation workflow facades.

This is an architecture checklist, not the CodeActions unit-test inventory. Production work in this document should be completed before the next unit-test phase so tests are written against the intended boundaries.

## Current Boundary Position

The principal project boundary is correct:

```text
Host -> CodeActions -> Workspace
```

Validated evidence:

- CodeActions has one production project reference: Workspace.
- CodeActions has no production reference to Plugins, Plugins.Core, the MCP SDK or the former Contracts project.
- MCP argument binding, schemas, publication and transport adapters remain in Host.
- Code Action handlers, requests, results, discovery, replay and provider composition remain in CodeActions.
- Query and mutation registrations retain closed generic handler, request and response types through typed visitors.
- Query and mutation execution contexts contain invocation-specific Workspace state only.
- Stable Code Action services are constructor-injected into handlers.
- The query and mutation workflow facades have been removed.
- There is no runtime type discrimination in the Code Action tool invocation path.

The boundary does not need redesign. The remaining work is internal responsibility tightening, reflection isolation and automated regression evidence.

## Validation Summary

| Area | Finding | Decision | Complexity |
| --- | --- | --- | --- |
| Resolution | Complete: token validation, Workspace matching, provider rediscovery and unique selection are named stages, and fix-all reuses the boundary. | Retain the focused resolution boundary. | Complete |
| Scoped and fix-all execution | Complete: shared selector resolution has one stateful boundary, while each service retains its distinct application semantics. | Retain the service split and explicit per-scope application. | Complete |
| Diagnostic and operation boundaries | Complete: optional analyzer activation and solution change counting have focused boundaries, while diagnostic selection and Roslyn operation application remain cohesive. | Retain the two extracted seams without further method-level splitting. | Complete |
| MEF provider composition | Complete: the catalogue owns composition while one compatibility adapter contains the non-public Roslyn export shape. Assembly deduplication now uses assembly identity. | Retain this startup-only compatibility boundary and its real-MEF evidence. | Complete |
| Boundary regression | Complete: production references, CodeActions transport isolation, server status and friend assemblies now have explicit regression evidence. | Retain the automated boundary suite alongside behavioural Host coverage. | Complete |
| Shared helpers | Complete: result mapping now has one owner and the remaining selector operations have a focused name. | Retain the pure selector and result helpers. | Complete |
| Built-in registration and ledger | Complete: one registrar owns every closed generic registration and its metadata, while the catalogue applies ledger visibility in one place. | Retain deterministic static composition. | Complete |

## Code Action Resolution

### Current responsibilities

`CodeActionResolutionService.ResolveActionAsync` currently:

1. validates the expected snapshot;
2. decodes the signed action token;
3. validates action kind, expiry and Workspace identity;
4. resolves the originating document and span;
5. rediscovers the provider action;
6. matches the replay identity uniquely;
7. classifies visibility; and
8. returns the resolved action, descriptor, document and span.

These steps form one high-level responsibility: resolving an opaque replay token against the current Workspace. The service itself is therefore justified. The problem is orchestration size and duplicated consumers, not the existence of the service.

### Findings

- `CodeActionFixAllService` repeats token decoding, kind, expiry, Workspace identity, document resolution, diagnostic discovery and unique-action matching.
- The duplicated implementations can drift in expiry, matching and error semantics.
- Snapshot validation, token rejection and selection rejection are also constructed in more than one helper class.
- Expiry now uses `TimeProvider`, which is the correct boundary.
- Returning a uniform `ActionExpired` for invalid token details avoids exposing which validation failed and should be preserved.
- Centralising fix-all resolution must preserve the existing public error codes, particularly the distinction between an expired action and an unavailable fix-all provider.

### Resolution

Resolution now uses named snapshot, token-context, rediscovery and unique-selection stages. Token context is represented explicitly without null-forgiving operators, expiry parsing uses invariant round-trip semantics, and provider unavailability is carried as an internal resolution reason while preserving the existing published rejection. Fix-all consumes the resolved action, document and span and maps provider unavailability back to its existing `FixAllUnavailable` result.

### Working checklist

- [x] Break `ResolveActionAsync` into named private stages for request/snapshot validation, token-context validation, action rediscovery and unique selection.
- [x] Use small internal result records where a stage can return either a value or a rejection; do not introduce parameter objects merely to shorten signatures.
- [x] Make `CodeActionFixAllService` consume the resolution boundary instead of decoding and replaying the token independently.
- [x] Preserve the current wire-level error codes and required actions while centralising the flow.
- [x] Use invariant round-trip parsing for the `O`-formatted expiry value.
- [x] Keep token signing and serialisation in `CodeActionTokenService`; do not move cryptography into the resolver.

## Scoped and Fix-All Execution

### Current position

The earlier facade split is sound:

- replay selection belongs to `CodeActionReplayService`;
- token-backed fix-all belongs to `CodeActionFixAllService`;
- diagnostic-driven scoped fixes belong to `CodeActionScopedFixService`; and
- location-driven code fixes belong to `CodeActionLocationFixService`.

Recombining these behind a workflow or services aggregate would restore the dependency and capability problems already removed.

### Findings

- `CodeActionFixAllService.StageFixAllAsync` is a single long orchestration method and duplicates resolution logic.
- `CodeActionScopedFixService` is large, but its size comes from explicit scope strategies rather than unrelated responsibilities.
- Both fix-all paths resolve document, project, projects and solution scopes, but their target application differs.
- Scope selector resolution is stateful because it uses `IWorkspaceResolver`; it should not be presented as a pure static helper.
- Candidate discovery, candidate application and change-limit enforcement are distinct stages and can be named without creating a class for every branch.

### Resolution

`CodeActionScopeResolver` now owns the duplicated document, project, project-set and solution target resolution. It is injected because it delegates to the invocation's `IWorkspaceResolver`; it is not presented as a pure helper. The result carries only resolved Roslyn targets or the existing execution rejection.

`CodeActionFixAllService` now has named resolution, scope-application, change-limit and result-construction stages. A small internal operation record is justified here because the resolved action, origin, provider and fix-all provider travel together through every scope strategy. `CodeActionScopedFixService` consumes the same scope result while retaining its explicit solution, document, project and multi-project application methods.

Project sets are canonicalised by Roslyn `ProjectId` before application. Distinct projects are still applied sequentially against the evolving candidate solution, while duplicate selectors no longer cause the same project to be mutated twice. Change limits remain enforced only after Roslyn has produced the complete candidate and before the Workspace mutation candidate is returned.

### Working checklist

- [x] Refactor `StageFixAllAsync` into private validation, scope application, change-limit and result-construction stages.
- [x] Remove its direct token, clock and diagnostic replay dependencies by using the resolution boundary where wire compatibility permits.
- [x] Introduce one focused scope-target resolver only if it removes real duplicate selector-resolution behaviour from both services; otherwise retain private methods.
- [x] If introduced, make the scope resolver an injected service because it calls `IWorkspaceResolver`; do not label it a pure helper.
- [x] Retain the explicit per-scope application methods in `CodeActionScopedFixService` because they make Roslyn `FixAllScope` differences visible.
- [x] Keep multi-project application sequential against the evolving candidate solution.
- [x] Keep `MaxChanges` enforcement after application and before returning the Workspace mutation candidate.
- [x] Do not add a new aggregate workflow or services object.

## Diagnostic and Operation Boundaries

### Diagnostic service

`CodeActionDiagnosticService` owns the diagnostics needed to discover or replay Code Actions. Compiler diagnostics, configured analyzer diagnostics, scoped fallback analyzers and synthetic diagnostics are all part of that outcome.

Runtime analyzer activation is different: it searches loaded assemblies by a ledger-supplied type name, activates a potentially non-public analyzer and suppresses reflection failures. That is a brittle external compatibility boundary with its own reason to change.

### Operation service

`CodeActionOperationService` currently:

- converts a Roslyn `CodeAction` into a neutral Workspace mutation candidate;
- applies document/project fix-all operations;
- validates the supported `CodeActionOperation` shape; and
- counts changed source documents.

Applying and validating Roslyn operations are cohesive. Counting changed documents is a deterministic solution comparison used for policy enforcement rather than action application.

### Resolution

`CodeActionAnalyzerActivator` is now the sole runtime analyzer reflection boundary. It distinguishes missing, incompatible, inspection-failed and construction-failed analyzers without using exceptions for ordinary absence. Its exception filters cover only expected assembly inspection and activation failures, with comments documenting why those external-runtime failures safely mean the optional analyzer is unavailable. `CodeActionDiagnosticService` consumes the explicit activation result and retains compiler, configured-analyzer, scoped and synthetic diagnostic selection.

Compiler and configured-analyzer collection now has one internal implementation, while document and project filtering remain separate and visible. This removes duplication without splitting the cohesive diagnostic outcome across more services.

`CodeActionSolutionChangeCounter` now owns the deterministic source-text comparison used by both fix-all services. It is independently injectable so orchestration tests can control change-limit outcomes, but its implementation has no dependencies or policy decisions. `CodeActionOperationService` now contains only Code Action operation materialisation, supported-operation validation and fix-all application. The Roslyn wrapping bookkeeping operation remains a named and documented compatibility exception.

### Working checklist

- [x] Extract analyzer lookup and activation behind a focused CodeActions-owned collaborator.
- [x] Keep reflection confined to that collaborator and return an explicit availability result rather than using exceptions for ordinary not-found flow.
- [x] Catch only reflection/activation failures that can arise at this external boundary; do not retain an unqualified catch without recording why it is safe.
- [x] Keep compiler, analyzer, scoped and synthetic diagnostic selection in `CodeActionDiagnosticService`.
- [x] Consolidate duplicated compiler/analyzer collection internally where it improves readability without changing document versus project filtering.
- [x] Retain supported-operation validation with Code Action application.
- [x] Move changed-source-document counting to a small pure internal solution-change helper, or rename the operation service only if keeping it there is clearer. Prefer the pure helper because it has no injected dependencies and has multiple consumers.
- [x] Preserve the special ignorable Roslyn wrapping operation as an explicitly documented compatibility exception.

## MEF Provider Composition

### Current position

`MefCodeActionProviderCatalog` correctly owns internal Roslyn provider composition. It is distinct from third-party plugin MEF composition and must remain so.

Official Roslyn APIs expose `MefHostServices.Create` and `DefaultAssemblies`, but not a public provider-export enumeration API. The current reflection reaches Roslyn's explicit internal export-provider implementation. This is startup compatibility code, not runtime tool dispatch.

### Findings

- Reflection is currently local to one method and runs only during singleton catalogue construction.
- The broad composition catch correctly converts an external composition failure into unavailable Code Actions, but its scope makes the precise failure stage opaque.
- Export enumeration reflects method and `Value` property shapes directly, so Roslyn upgrades can break it.
- Assembly deduplication combines assembly identities and filesystem locations in one case-insensitive string set. Filesystem path comparison should not assume Windows semantics on Linux.
- Real MEF behaviour already has integration coverage and should remain integration-tested rather than replaced with mocked composition.

### Resolution

`MefHostExportProviderCompatibilityAdapter` now owns every assumption about Roslyn's explicit internal `IMefHostExportProvider.GetExports<T>` implementation and the exported lazy value shape. It validates reflection cardinality and value shape explicitly, converts expected reflection or activation failures into a typed result, and reports the precise compatibility stage and exception type without publishing a stack trace or uncontrolled exception text. No registration, handler construction or invocation path uses this reflection.

`MefCodeActionProviderCatalog` remains the composition owner. Assembly resolution, MEF host creation, export reading and provider metadata filtering are now separately reported stages. External provider and Roslyn composition failures still disable Code Actions without preventing server startup.

Assembly deduplication now uses `CodeActionAssemblyIdentityComparer`. It compares assembly full identities and never mixes assembly names with filesystem locations, so Linux paths no longer inherit a case-insensitive comparison. A focused real-MEF integration test exercises the compatibility adapter directly, while the existing catalogue, built-in provider and audit scenarios continue to validate the complete composition path.

### Working checklist

- [x] Retain `MefCodeActionProviderCatalog` as the catalogue/composition owner.
- [x] Move the non-public export enumeration into one small internal compatibility adapter so reflection assumptions have a single test and failure message.
- [x] Do not introduce reflection into registration, handler construction or invocation.
- [x] Replace the mixed string-key assembly deduplication with an assembly-aware approach that does not impose case-insensitive filesystem semantics on Linux.
- [x] Report composition stage and actionable context without exposing an uncontrolled exception dump.
- [x] Keep catalogue construction non-throwing for ordinary provider unavailability.
- [x] Retain real MEF composition and Roslyn-upgrade compatibility as integration/audit coverage.

## Boundary Regression and Host Composition

### Existing evidence

- Typed registration and visitor dispatch have focused unit tests.
- Host DI composition resolves the concrete provider catalogue and Code Action execution-context factory.
- Reserved Code Action name collisions disable external plugins deterministically.
- `server-status` publishes Code Action component availability separately from plugin statuses.
- Host constructs Code Action MCP adapters; CodeActions contains no MCP references.

### Gaps

- Forbidden project-reference directions are documented but remain manually inspected.
- The server-status tests demonstrate separate collections, but there is no explicit assertion named around CodeActions never appearing as a plugin.
- CodeActions grants friend access to `Plugins.Core.Test` and `Plugins.Core.IntegrationTest`, although neither project directly consumes CodeActions internals. Those friendships weaken the stated boundary and appear stale.

### Resolution

`CodeActionsArchitectureTests` now reads the production project files and enforces the complete approved reference graph. The graph permits the intentional direct Workspace reference from Plugins.Core because its bundled public inspection contracts use Workspace-owned selectors and results. It requires Workspace to remain dependency-neutral, CodeActions and Plugins to depend only on Workspace, and Host to remain the sole composition root. A focused CodeActions assertion separately rejects Plugins, Plugins.Core, Host and `ModelContextProtocol` dependencies.

Host composition integration coverage now requests full server status with the real Code Action and plugin catalogues. It proves Code Actions have a populated internal catalogue and component status, that the status plugin collection is exactly the plugin catalogue collection, and that the former `roslyn.workbench.codeactions` plugin identity is absent.

The stale CodeActions friendships for TestSupport, Plugins.Core.Test and Plugins.Core.IntegrationTest have been removed. The retained friends are direct production, integration-support, CodeActions test/audit, Host test/integration or dynamic-proxy consumers. Their exact set is now locked by the architecture test so future access must be intentional.

### Working checklist

- [x] Add an architecture-regression test that reads production project references and enforces the approved dependency graph.
- [x] Assert explicitly that CodeActions has no Plugins, Plugins.Core, MCP SDK or Host project/package reference.
- [x] Add an explicit server-status test proving Code Actions are represented only by `CodeActions` component status and never by a `PluginStatus` entry.
- [x] Retain the existing reserved-name collision integration test.
- [x] Retain typed visitor and Host DI construction tests.
- [x] Remove stale `InternalsVisibleTo` entries for `Roslyn.Workbench.Mcp.Plugins.Core.Test` and `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` after confirming a clean build.
- [x] Review the remaining friend assemblies and retain only those that directly require internal CodeActions access.

## Lower-Risk Check: Shared Helpers

### Original finding

Static helpers are acceptable here only for deterministic operations with no hidden state or external resource ownership. `ToolExecutionHelpers` meets that shape for selector projection and result construction, but it duplicates snapshot, selector-status and rejection mapping already present in `CodeActionExecutionResultFactory`.

### Resolution

The duplicated result methods have been removed. `CodeActionExecutionResultFactory` is the single result-mapping owner, while the remaining symbol resolution and location projection live in the more accurately named `CodeActionSelectorHelpers`.

### Working checklist

- [x] Consolidate snapshot validation, selector-status mapping and rejection construction into `CodeActionExecutionResultFactory`.
- [x] Keep symbol resolution and location-selector projection in a separate helper because they are tool-facing projection logic.
- [x] Rename the remaining helper to `CodeActionSelectorHelpers` so its responsibility is explicit.
- [x] Do not turn pure result or selector projection into injected services.

## Lower-Risk Check: Built-In Registration and Ledger

### Original finding

`BundledCodeActionToolRegistrar` and `BundledCodeActionCatalog` are deterministic startup composition. Static implementation is appropriate. The original implementation left two invariants weaker than necessary:

- tool-owned static registration methods spread catalogue composition and MCP metadata across every handler; and
- `ConvertPropertyTool.Register` bypassed `BuiltInCodeActionLedger.IsDedicatedToolVisible` even though the ledger contains `convert-property`.

### Resolution

The registrar now owns every declared tool's closed generic registration and MCP metadata. Handler classes own execution only and expose no static registration entry point. `BundledCodeActionCatalog` applies ledger visibility to the resulting registrations, so dedicated-tool identity has one composition path and `convert-property` follows the same rule as every other dedicated tool. Infrastructure tools remain outside the dedicated ledger.

### Working checklist

- [x] Ensure every dedicated built-in tool, including `convert-property`, is gated by the ledger.
- [x] Remove tool-owned static registration methods and centralise closed generic registrations and metadata in the registrar.
- [x] Keep list, describe and staging infrastructure tools outside dedicated-provider visibility gating.
- [x] Retain duplicate-name failure in `CodeActionToolRegistry`.
- [x] Add catalogue evidence that every published tool contract is exact, every visible ledger tool is registered exactly once and any named hidden ledger tool is absent.
- [x] Do not replace deterministic internal registration with plugin discovery or runtime assembly scanning.

## Recommended Working Order

1. [x] Consolidate the result helpers and harden built-in ledger registration.
2. [x] Refactor resolution and make fix-all reuse it.
3. [x] Refactor fix-all and scoped-fix orchestration without restoring a facade.
4. [x] Isolate analyzer activation and solution-change counting.
5. [x] Isolate and harden the MEF compatibility adapter.
6. [x] Add automated project-boundary, status and friend-assembly regression evidence.
7. [x] Re-run the architecture audit and mark each checklist item complete.

This order starts with small invariant clean-ups, then changes the shared resolution boundary before its consumers, and leaves external MEF compatibility and regression automation until the internal shape is stable.

## Final Re-Audit

The completed implementation was re-audited against every checklist item on 2026-07-16. No remaining architecture issue was found:

- the production project-reference graph matches the approved boundary and CodeActions references only Workspace;
- no plugin, MCP transport, services aggregate, query workflow or mutation workflow dependency has returned;
- query and mutation registrations retain their closed generic types and dispatch through the typed visitor;
- stable Code Action services are constructor-injected while invocation contexts contain only Workspace execution state;
- mutation staging remains on the execution lease rather than the handler context;
- result mapping, scope resolution, analyzer activation, solution-change counting and MEF export compatibility each have one owner;
- reflection remains confined to optional analyzer activation and the documented Roslyn MEF compatibility adapter;
- internal registration remains deterministic and ledger-gated; and
- automated project-reference, friend-assembly, collision, Host-composition and server-status evidence remains green.

The architecture phase is therefore complete. The measured unit-test inventory is maintained in `CodeActionsUnitTestInventory.md`.

## Next Phase: CodeActions Unit Testing

Unit testing is deliberately the next phase, after this architecture checklist is complete. Do not build broad tests around implementation that this checklist intends to reshape.

The next unit-test inventory should begin with:

1. `CodeActionResolutionService` and any extracted validation results;
2. `CodeActionScopeResolver`, followed by isolated consumer tests that mock that boundary;
3. `CodeActionDiagnosticService` and the analyzer-activation boundary;
4. `CodeActionOperationService` and the solution-change helper;
5. `CodeActionTokenService` malformed input, signature, payload and round-trip behaviour; and
6. unit-testable MEF compatibility logic, while retaining real composition as integration coverage.

For materially changed, unit-testable implementation, require 100% line and branch coverage. Continue to use integration and audit tests for real Roslyn MEF composition, built-in provider compatibility and controlled-provider replay behaviour.

## Completion Criteria

The CodeActions architecture round is complete when:

- every working checkbox above is either completed or explicitly rejected with rationale;
- no query or mutation workflow facade has been reintroduced;
- CodeActions still references only Workspace among production projects;
- reflection remains confined to documented Roslyn compatibility boundaries;
- Host remains the sole MCP transport owner;
- Code Actions remain internal and absent from plugin discovery and plugin status;
- the full build, focused Code Action tests, affected integration/audit projects and full suite are green; and
- the CodeActions unit-test inventory is started as the next tracked phase.
