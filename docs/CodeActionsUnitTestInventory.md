# CodeActions Unit-Test Inventory

Date: 2026-07-16

Status: Planned

## Purpose

This document inventories unit-test coverage for `Roslyn.Workbench.Mcp.CodeActions` after completion of the architecture refactor. It separates reachable unit behaviour from real Roslyn composition and compatibility boundaries, records the measured baseline, and defines the order in which the remaining tests should be built.

The inventory follows `TestingStrategy.md` and `test/AGENTS.md`:

- use xUnit, Moq and AwesomeAssertions;
- keep production collaborators as visible mocks;
- use real in-memory Roslyn objects only as syntax, semantic, compilation or solution data;
- do not use reflection to invoke implementation code or create test-only production seams;
- keep real MEF composition, built-in provider compatibility and controlled-provider execution in integration or audit projects; and
- require 100% line and branch coverage for selected unit-testable implementations unless an exact defensive or external-runtime branch is approved and recorded here.

## Architecture Handoff

The final architecture re-audit found no remaining boundary or responsibility issue. Unit tests can now be written against stable responsibilities:

- token validation and action rediscovery belong to `CodeActionResolutionService`;
- scope selection belongs to `CodeActionScopeResolver`;
- diagnostic collection and optional analyzer activation are separate;
- Roslyn operation materialisation and solution change counting are separate;
- query and mutation contexts contain only invocation-specific Workspace state;
- mutation staging remains on the mutation execution lease; and
- Roslyn MEF reflection remains isolated from registration and invocation.

No production change is authorised by this inventory. Any seam or defensive-branch cleanup identified below must be called out for approval before implementation.

## Measured Baseline

The baseline was captured from the CodeActions unit project with Coverlet after the architecture suite completed.

| Measure | Current value |
| --- | ---: |
| Production C# files | 181 |
| Files with executable sequence points | 135 |
| Interfaces, enums and other files without sequence points | 46 |
| Discovered unit tests | 377 |
| Line coverage | 83.25% |
| Branch coverage | 70.49% |
| Executable files at 100% line and branch | 107 |
| Executable files with partial coverage | 12 |
| Executable files with zero unit coverage | 16 |

Coverlet emits compiler-generated async and closure classes separately. The figures and tables in this document aggregate their sequence points into the owning source file.

The assembly percentage understates the strength of the existing handler coverage but correctly exposes missing service coverage. All 43 built-in refactoring handlers, all five internal Code Action tools, the catalogue and registrar, the discovery service, the replay and location services, the scope resolver, and the scoped-fix service currently measure 100% line and branch coverage.

## Existing Test Quality

The current CodeActions unit project follows the active test pattern after the consistency cleanup:

- no `Mock.Of<T>`, `null!`, `Task.Delay`, test comments or reflection-based implementation invocation is present;
- shared mocks are constructed explicitly in test-class constructors and local mocks use `new Mock<T>()`;
- collaborator mocks and scenario-specific setup remain visible in test classes;
- fix-all and scoped-fix consumers mock `ICodeActionScopeResolver`, while `CodeActionScopeResolverTests` own selector-resolution behaviour directly;
- reusable helpers create Roslyn data only and do not construct hidden production service graphs;
- test namespaces mirror their production namespaces; and
- the architecture test uses assembly metadata only to locate the repository before inspecting project files, rather than reflecting over runtime implementation shape.

The nested query and mutation handlers in registry tests are typed registration fixtures, not substitutes for production collaborators.

## Completed Coverage Families

The following production families need no broad replacement tests. Future work should preserve their current behavioural coverage while avoiding duplicate assertions.

| Family | Current evidence | Disposition |
| --- | --- | --- |
| Built-in refactoring handlers | 43 test classes; every handler at 100% line and branch; registration assertions are centralised in the catalogue contract test | Complete |
| Internal list, describe and staging tools | Five test classes; every tool at 100% line and branch; registration assertions are centralised in the catalogue contract test | Complete |
| Replay, location and scoped-fix orchestration | `CodeActionReplayService`, `CodeActionLocationFixService` and `CodeActionScopedFixService` at 100% line and branch | Complete |
| Scope resolution | Dedicated `CodeActionScopeResolverTests` at 100% line and branch; consumers mock the boundary | Complete |
| Action resolution | `CodeActionResolutionServiceTests` cover token context, provider rediscovery, replay identity, visibility and success at 100% line and branch | Complete |
| Discovery | `CodeActionDiscoveryService` at 100% line and branch | Complete |
| Result construction and identity | Execution result factory, information factory and candidate identity at 100% line and branch | Complete |
| Internal catalogue and ledger | Catalogue, registrar and ledger at 100% line and branch; one contract snapshot locks every published registration and metadata value; architecture and audit evidence retained | Complete |
| Context capability shape | Query and mutation context projection at 100% line and branch | Complete |

Data-only contracts that already execute through these tests do not need one test per property. Their values remain assertions of the handlers and services that consume or publish them.

## Zero-Coverage Logic Inventory

| Target | Line | Branch | Complexity | Disposition and required behaviour |
| --- | ---: | ---: | --- | --- |
| `CodeActionDiagnosticService` | 0% | 0% | High | Add. Use in-memory Roslyn documents with a mocked analyzer activator. Cover compiler, configured analyzer, scoped analyzer and synthetic diagnostics; document, project, ID and span filtering; missing compilation; unavailable analyzers and cancellation. |
| `CodeActionOperationService` | 0% | 0% | High | Add. Cover action operation materialisation, no/multiple apply operations, unsupported auxiliary operations, the documented wrapping operation, null fix-all actions, document/project fix-all contexts and successful candidates. |
| `CodeActionTokenService` | 0% | 0% | Medium | Add round-trip, independent-instance rejection, tampering, malformed separators/Base64, payload preservation and URL-safe encoding. Review the signed-null payload guard separately because the current public flow cannot produce a valid signature for malformed JSON. |
| `CodeActionAnalyzerActivator` | 0% | 0% | Medium, mixed boundary | Add normal type-not-found, incompatible, available and construction-failed behaviour using controlled analyzer types. Treat unreadable loaded-assembly inspection as an external-runtime defensive branch unless a normal supported test path is found. |
| `CodeActionSolutionChangeCounter` | 0% | 0% | Low | Add using in-memory multi-document solutions. Cover unchanged, changed, missing and multiple documents plus cancellation. |
| `WorkspaceFixAllDiagnosticProvider` | 0% | 0% | Low | Add with a mocked diagnostic service. Cover document, project and all-diagnostic aggregation, ordering and cancellation propagation. |
| `CodeActionAssemblyIdentityComparer` | 0% | 0% | Low | Add identity equality, reference equality, null alternatives, case-insensitive full identity, distinct identities and stable hash behaviour. Record the no-full-name fallback only if a supported assembly instance can expose it. |
| `CodeActionExecutionOutcomeExtensions` | 0% | 0% | Low | Add one visible theory covering success, no-change, rejected, conflict and faulted outcomes. |

## MEF Composition Boundary

| Target | Line | Branch | Disposition |
| --- | ---: | ---: | --- |
| `MefCodeActionProviderCatalog` | 0% | 0% | Mixed. Unit-test the no-assemblies result and any paths reachable with the existing export-provider seam. Keep host creation, export enumeration, C# metadata filtering and real provider composition in integration/audit coverage. Do not mock a real MEF graph merely to raise coverage. |
| `MefHostExportProviderCompatibilityAdapter` | 0% | 0% | Integration boundary. Its behaviour depends on Roslyn's non-public runtime export shape. Retain and expand the focused real-MEF integration test for successful enumeration and actionable failure reporting when feasible. |
| `CodeActionCompositionOptions` | 0% | 100% | Data/default shape. Assert defaults through catalogue composition rather than a property-only test. |
| `CodeActionProviderCatalogComposition` | 0% | 100% | Internal result shape. Cover through catalogue outcomes. |
| `CodeActionProviderCatalogStatus` | 33.33% | 100% | Internal status shape. Cover availability, message and version through catalogue and Host status tests. |
| `MefHostExportReadResult<T>` | 0% | 100% | Internal result invariant. Cover success and failure factories through adapter/catalogue tests. |

Real MEF execution does not become a unit test merely because it is in the unit assembly. Coverage for this boundary must remain correctly categorised.

## Partial-Coverage Inventory

| Target | Line | Branch | Complexity | Remaining work |
| --- | ---: | ---: | --- | --- |
| `CodeActionMutationExecutionLease` | 44.00% | 0% | Low | Cover staging delegation, mapped result, disposal and the invalid acquired-without-stager invariant. |
| `CodeActionQueryExecutionLease` | 78.57% | 100% | Low | Cover disposal of the underlying Workspace lease. |
| `CodeActionMutationRegistration<THandler,TRequest>` | 45.45% | 100% | Low | Assert metadata, kind, request/response types and mutation visitor dispatch. |
| `CodeActionQueryRegistration<THandler,TRequest,TResponse>` | 72.73% | 100% | Low | Assert metadata, kind and request/response types in addition to existing query visitor dispatch. |
| `CodeActionExecutionContextFactory` | 100% | 50% | Low | Add the complementary rejected-query and acquired-mutation paths so context and failure conditions are independently covered. |
| `CodeActionSelectorHelpers` | 80.39% | 72.22% | Medium | Cover missing selector, location snapshot rejection, resolved/unresolved symbols, missing location data and document-ID versus path projection. Use the normal helper entry points only. |
| `CodeActionToolRegistry` | 90.48% | 62.50% | Low | Add mutation visitor evidence and separate blank name, title and description validation cases. |
| `CodeActionWorkspaceResultMapper` | 98.39% | 94.44% | Low | Cover the invalid failure-status invariant and assert complete data, diagnostics and warning projection for every supported result. |
| `CodeActionDescriptorRegistry` | 66.67% | 60.00% | Medium | Cover overrides, title normalisation, add-import/remove-using special cases, supported parameterised families, hidden ledger families, unknown and blank providers, and descriptor details. |
| `BuiltInCodeActionFamily` | 60.00% | 37.50% | Low | Cover every support-state execution mode and dedicated-tool visibility combination through catalogue/descriptor tests. |
| `CodeActionFixAllService` | 97.66% | 95.12% | Medium expansion | Cover an inconsistent project-set result through the mocked scope boundary. Record the unsupported-scope default only if it is unreachable after `CodeActionScopeResolver` validation. |
| `CodeActionToolMetadata` | 80.00% | 100% | Data/default shape | Assert default behaviour through registry tests. |
| `CodeActionDescriptorContext` | 50.00% | 100% | Data-only contract | Assert populated context through describe-tool outcomes. |
| `CodeActionListItem` and `CodeActionNameOptionInfo` | 0% | 100% | Data-only contracts | Assert their output properties through list/describe tool scenarios; do not add reflection or property-only tests. |
| `CodeActionAnalyzerActivationResult` | 0% | 100% | Internal result shape | Cover through analyzer activator and diagnostic-service outcomes. |

## Files Requiring No Direct Tests

The 46 files without executable sequence points are interfaces, enums, global imports or marker/result declarations. They are covered through assignability, compilation and the behaviour of their consumers. They must not receive reflection-only shape tests.

The remaining data-only request/response records are likewise covered through their owning tools and services. A property should be read and asserted where it is published or consumed; an unused property discovered during that work is a production-design finding, not a reason for a property-only test.

## Production Decisions Requiring Approval

The first coverage pass may expose three cases that must be reviewed before changing production code:

1. `CodeActionTokenService` cannot receive a correctly signed malformed or `null` JSON payload through its supported API because its secret is process-local and generated internally. Do not add a test-only key constructor. Decide whether the defensive deserialisation branch should remain documented or whether token encoding/signing has a runtime reason to be separated.
2. `CodeActionAnalyzerActivator` obtains assemblies from `AppDomain.CurrentDomain`. Normal tests can cover known, missing, incompatible and construction-failing types, but forcing an assembly's `GetType` to throw would be artificial. Treat `InspectionFailed` as a defensive external-runtime branch unless a real supported scenario demonstrates it.
3. The MEF compatibility adapter and most catalogue composition paths require Roslyn's real internal export-provider implementation. Keep those paths in integration/audit coverage unless a production abstraction is justified independently of testing.

No production change should be made during this testing phase without explicit confirmation.

## Recommended Delivery Order

### C1: Resolution boundary

Complete. `CodeActionResolutionServiceTests` use mocked token, descriptor, discovery, diagnostic and Workspace resolver collaborators with one in-memory Roslyn document. All supported token validation, refactoring and code-fix rediscovery, replay identity, provider availability, descriptor visibility and success paths measure 100% line and branch coverage without production changes.

### C2: Diagnostics and activation

Add `CodeActionDiagnosticServiceTests` and `CodeActionAnalyzerActivatorTests`. Keep analyzer activation findings explicit and do not hide real Roslyn compilation setup behind a service harness.

### C3: Operation application

Add `CodeActionOperationServiceTests`, `CodeActionSolutionChangeCounterTests` and `WorkspaceFixAllDiagnosticProviderTests`, then close the two remaining `CodeActionFixAllService` paths. This group shares operation and fix-all data but each target retains separate collaborators and assertions.

### C4: Tokens and small execution boundaries

Add `CodeActionTokenServiceTests` and complete the execution outcome, registration, lease, context-factory, registry, selector-helper, mapper, descriptor and built-in-family gaps. These are mostly low-complexity tests and can be delivered as a group after any token design decision is approved.

### C5: MEF boundary checkpoint

Add only the catalogue/result unit cases that are truly isolated. Expand the existing CodeActions MEF integration coverage for runtime export compatibility rather than disguising composition as unit testing. Retain the audit suite for built-in provider and ledger compatibility.

### C6: Final coverage and pattern audit

Re-run file-aggregated line and branch coverage. Assert data-only properties through owners where currently missing, document any approved defensive branches, sample the new tests against the test pattern, then run the CodeActions unit, integration and audit projects followed by the full suite.

## Completion Criteria

The CodeActions unit-testing round is complete when:

- every executable source file has a completed disposition;
- every unit-testable logic-bearing target reaches 100% line and branch coverage or has an explicitly approved exact defensive branch;
- all data-only output properties are exercised through an owning tool or service where they are actually used;
- unit tests retain visible Moq collaborators and use real Roslyn objects only as data;
- real MEF composition and provider compatibility remain integration/audit evidence;
- no production hook, broader interface or reflection path exists solely for tests;
- test categorisation and the fast-loop filter remain correct; and
- formatting, build, focused coverage, affected integration/audit projects and the full suite are green.

## Measurement Commands

```bash
dotnet test test/Roslyn.Workbench.Mcp.CodeActions.Test/Roslyn.Workbench.Mcp.CodeActions.Test.csproj --collect:"XPlat Code Coverage" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest.csproj --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test test/Roslyn.Workbench.Mcp.CodeActions.AuditTest/Roslyn.Workbench.Mcp.CodeActions.AuditTest.csproj --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```
