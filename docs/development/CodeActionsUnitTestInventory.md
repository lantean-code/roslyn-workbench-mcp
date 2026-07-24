# CodeActions Unit-Test Inventory

Date: 2026-07-16

Status: Unit testing complete; integration-test redesign deferred

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

The current measurement was captured from the CodeActions unit project with Coverlet after C6 completed.

| Measure                                                   | Current value |
| --------------------------------------------------------- | ------------: |
| Production C# files                                       |           181 |
| Files with executable sequence points                     |           135 |
| Interfaces, enums and other files without sequence points |            46 |
| Discovered unit tests                                     |           496 |
| Covered executable lines                                  | 4,081 / 4,263 |
| Line coverage                                             |        95.73% |
| Covered branches                                          |     685 / 762 |
| Branch coverage                                           |        89.94% |
| Executable files at 100% line and branch                  |           126 |
| Executable files with partial coverage                    |             6 |
| Executable files with zero unit coverage                  |             3 |

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

The C6 repository-wide scan also found no expression-bodied test members or test methods outside the required `GIVEN_..._WHEN_..._THEN_Should...` naming pattern. Controlled `DiagnosticAnalyzer`, `CodeAction`, operation and typed-handler subclasses are Roslyn or registration input fixtures; they do not replace production collaborators.

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
| Diagnostics | `CodeActionDiagnosticServiceTests` cover compiler, configured analyzer, additional analyzer, document/project, ID/span, synthetic, unavailable and cancellation paths; only the defensive null syntax-tree result after a successful C# compilation remains | Complete with approved defensive gap |
| Analyzer activation | Available, incompatible, missing and four construction-failure shapes covered; external assembly-inspection failures and impossible assignable-type null construction remain approved defensive gaps | Complete with approved defensive gaps |
| Operation application | `CodeActionOperationServiceTests` cover mutation materialisation, absent, multiple and unsupported operations, recognised wrapping bookkeeping, null fix-all actions, document/project fix-all contexts and successful candidates at 100% line and branch | Complete |
| Solution change counting | `CodeActionSolutionChangeCounterTests` cover unchanged, changed, multiple and missing candidate documents plus cancellation at 100% line and branch | Complete |
| Fix-all diagnostic projection | `WorkspaceFixAllDiagnosticProviderTests` cover document and project delegation plus ordered all-diagnostic aggregation at 100% line and branch | Complete |
| Fix-all orchestration | `CodeActionFixAllServiceTests` cover all reachable paths, including an inconsistent project-set result from the mocked scope boundary; only the defensive invalid-scope default remains | Complete with approved defensive gap |
| Token encoding and validation | `CodeActionTokenServiceTests` cover complete payload round-trip, process-local signing, missing and malformed parts, tampering and unpadded Base64Url output; only the approved correctly-signed `null` JSON payload guard remains | Complete with approved defensive gap |
| Execution leases and context adaptation | Query/mutation disposal, staging delegation, missing-stager invariant and all acquired/rejected context-factory paths at 100% line and branch | Complete |
| Typed registration and registry | Query/mutation metadata, contract types, visitor dispatch, duplicate names, individual required metadata validation and optional defaults at 100% line and branch | Complete |
| Workspace result mapping and outcome classification | Every operation status, full success/failure payload projection, invalid statuses and every error classification at 100% line and branch | Complete |
| Selector projection | Missing, mismatched, resolved, not-found and ambiguous symbol resolution plus every resolved-location projection shape at 100% line and branch | Complete |
| Descriptor and built-in family classification | Overrides, title normalisation, special using families, replay, parameterised, hidden, blank and unknown providers plus every family-derived state at 100% line and branch | Complete |
| MEF unit boundary | Empty configuration, unavailable catalogue projection, export-read success/failure invariants, composition options and result/status shapes, and assembly identity comparison at 100% line and branch for the selected isolated targets | Complete |
| Result construction and identity | Execution result factory, information factory and candidate identity at 100% line and branch | Complete |
| Internal catalogue and ledger | Catalogue, registrar and ledger at 100% line and branch; one contract snapshot locks every published registration and metadata value; architecture and audit evidence retained | Complete |
| Context capability shape | Query and mutation context projection at 100% line and branch | Complete |

Data-only contracts that already execute through these tests do not need one test per property. Their values remain assertions of the handlers and services that consume or publish them.

## Zero-Coverage Logic Inventory

No uncovered logic-bearing target in this section remains suitable for ordinary unit testing. The remaining zero-coverage files are the real-MEF compatibility adapter and the two unused contract types recorded below.

## MEF Composition Boundary

| Target | Line | Branch | Disposition |
| --- | --: | --: | --- |
| `MefCodeActionProviderCatalog` | 28.57% | 9.09% | Unit portion complete. Empty configuration and complete unavailable catalogue projection are covered without constructing MEF. Every remaining branch creates or consumes a real `MefHostServices`, including assembly resolution, export reads, metadata filtering and available composition; defer those branches to the later integration-test pattern. |
| `MefHostExportProviderCompatibilityAdapter` | 0% | 0% | Integration boundary. Its behaviour depends on Roslyn's non-public runtime export shape. Retain and expand the focused real-MEF integration test for successful enumeration and actionable failure reporting when feasible. |
| `CodeActionCompositionOptions` | 100% | 100% | Complete through empty catalogue configuration. |
| `CodeActionProviderCatalogComposition` | 100% | 100% | Complete through the unavailable catalogue outcome. |
| `CodeActionProviderCatalogStatus` | 100% | 100% | Complete through the unavailable catalogue outcome and existing consumers. |
| `MefHostExportReadResult<T>` | 100% | 100% | Complete through explicit success and failure factory behaviour. |
| `CodeActionAssemblyIdentityComparer` | 100% | 100% | Complete for reference/null equality, identity equality, missing identities and both hashing strategies. |

Real MEF execution does not become a unit test merely because it is in the unit assembly. Coverage for this boundary must remain correctly categorised.

## Partial-Coverage Inventory

| Target | Line | Branch | Complexity | Remaining work |
| --- | --: | --: | --- | --- |
| `CodeActionAnalyzerActivator` | 84.34% | 52.63% | Approved external-runtime gaps | Available, missing, incompatible and four supported construction-failure shapes are covered. The remaining lines are the loaded-assembly inspection-exception path; remaining branches distinguish runtime exception types that normal loaded assemblies and analyzer construction cannot deterministically produce. |
| `CodeActionDiagnosticService` | 98.28% | 97.92% | Approved Roslyn defensive gap | Lines 163-164 handle `GetSyntaxTreeAsync` returning `null` after the same source document successfully produced a C# compilation. Supported in-memory and loaded-workspace flows cannot produce that inconsistent Roslyn state. |
| `CodeActionFixAllService` | 98.60% | 97.56% | Approved defensive gap | All reachable behaviour is covered. Lines 145-147 are the unsupported-scope default; the real `CodeActionScopeResolver` rejects invalid `ScopeKind` values before this method is called. Covering it would require an inconsistent mocked boundary and would duplicate the resolver's validation rather than model production behaviour. |
| `CodeActionTokenService` | 95.56% | 90.00% | Approved defensive gap | All supported token behaviours are covered. Lines 45-46 reject a correctly signed JSON `null`; the process-local secret and valid-payload encoder make that input unreachable through the supported API. |
| `CodeActionDescriptorContext` | 50.00% | 100% | Production review required | `Kind` and `Message` are published and asserted by `DescribeCodeActionToolTests`; `NameOptions` and `Members` are never populated by production code. Do not add property-only tests. |
| `CodeActionListItem` and `CodeActionNameOptionInfo` | 0% | 100% | Production review required | Neither type is constructed or consumed anywhere in production. They are dead-contract candidates rather than missing unit-test scenarios. |

## Files Requiring No Direct Tests

The 46 files without executable sequence points are interfaces, enums, global imports or marker/result declarations. They are covered through assignability, compilation and the behaviour of their consumers. They must not receive reflection-only shape tests.

The remaining data-only request/response records are likewise covered through their owning tools and services. A property should be read and asserted where it is published or consumed; an unused property discovered during that work is a production-design finding, not a reason for a property-only test.

## Production Decisions Requiring Approval

The coverage passes exposed the following cases that must be reviewed before changing production code:

1. `CodeActionTokenService` cannot receive a correctly signed malformed or `null` JSON payload through its supported API because its secret is process-local and generated internally. Do not add a test-only key constructor. Decide whether the defensive deserialisation branch should remain documented or whether token encoding/signing has a runtime reason to be separated.
2. Resolved: `CodeActionAnalyzerActivator` covers known, missing, incompatible and construction-failing types. `InspectionFailed`, the associated expected-exception alternatives and the assignable-type construction-to-null branch remain documented defensive runtime gaps because normal loaded assemblies cannot produce them and `Activator.CreateInstance` cannot return `null` for a successfully constructed `DiagnosticAnalyzer` type.
3. The MEF compatibility adapter and most catalogue composition paths require Roslyn's real internal export-provider implementation. Keep those paths in integration/audit coverage unless a production abstraction is justified independently of testing.
4. Resolved under the existing Roslyn defensive-guard exception: after `Document.Project.GetCompilationAsync` returns a C# compilation for a source document, the same document's `GetSyntaxTreeAsync` cannot return `null` through a supported in-memory or loaded-workspace path. The additional-analyzer guard remains defensive and is not covered with a fake Roslyn document.
5. Resolved: `CodeActionFixAllService.ApplyScopeAsync` retains an unsupported-scope default as a local defensive guard. The real `CodeActionScopeResolver` rejects invalid `ScopeKind` values first, so the default is not reachable through the production flow and does not justify a production seam.
6. Resolved with user approval: `CodeActionTokenService.TryDecode` retains the correctly-signed JSON `null` guard. The public encoder cannot create that payload and the signing secret is deliberately process-local, so no key exposure or test-only seam will be introduced.
7. `CodeActionListItem` and `CodeActionNameOptionInfo` are unused production contracts. `CodeActionDescriptorContext.NameOptions` and `Members` are likewise never populated. Decide whether to remove these surfaces or implement their intended owning behaviour before testing them.

No production change should be made during this testing phase without explicit confirmation.

## Recommended Delivery Order

### C1: Resolution boundary

Complete. `CodeActionResolutionServiceTests` use mocked token, descriptor, discovery, diagnostic and Workspace resolver collaborators with one in-memory Roslyn document. All supported token validation, refactoring and code-fix rediscovery, replay identity, provider availability, descriptor visibility and success paths measure 100% line and branch coverage without production changes.

### C2: Diagnostics and activation

Complete. `CodeActionDiagnosticServiceTests` use in-memory Roslyn documents with visible analyzer and analyzer-reference mocks and cover every supported compiler, configured-analyzer, additional-analyzer, filtering, synthetic and cancellation path. `CodeActionAnalyzerActivatorTests` use controlled analyzer types for available, incompatible, missing, constructorless, throwing, abstract and open-generic outcomes. The exact Roslyn and external-runtime defensive gaps are recorded above; no production seam or test-only hook was introduced.

### C3: Operation application

Complete. `CodeActionOperationServiceTests`, `CodeActionSolutionChangeCounterTests` and `WorkspaceFixAllDiagnosticProviderTests` retain separate collaborators and assertions while covering each target at 100% line and branch. `CodeActionFixAllServiceTests` now cover the remaining reachable inconsistent-project-set path. The exact invalid-scope defensive gap is recorded above. No production change or test-only seam was introduced.

### C4: Tokens and small execution boundaries

Complete. `CodeActionTokenServiceTests` cover every supported token behaviour with the exact approved defensive exception recorded above. Execution outcomes, registrations, leases, context adaptation, registry validation, selector projection, Workspace result mapping, descriptor classification and built-in-family properties now measure 100% line and branch. No production seam was introduced. The unused contract surfaces discovered by owner-based testing are recorded as a separate production decision rather than covered with property-only tests.

### C5: MEF boundary checkpoint

Unit portion complete. `MefCodeActionProviderCatalogTests` cover empty configuration and the complete unavailable projection without constructing MEF, plus export-read success/failure invariants. `CodeActionAssemblyIdentityComparerTests` cover every equality and hashing branch. Composition options, result and status shapes now measure 100% line and branch through their owners. All paths that create or interrogate a real Roslyn `MefHostServices`, including the compatibility adapter, remain deferred until the integration-test pattern is redesigned; no new integration tests were added in this round.

### C6: Final unit-coverage and pattern audit

Complete. File-aggregated coverage identifies 126 fully covered files, six partial files and three zero-coverage files. Every non-full file now has an exact approved defensive, external-runtime, real-MEF or unused-contract disposition. The repository-wide CodeActions unit scan found no prohibited mocking, null-forgiving input, timing, invocation-history, reflection-invocation, comment, expression-body or test-naming pattern. Data-only properties are asserted through owners where production uses them; unused surfaces are recorded for production review rather than covered artificially. Integration-test structure remains unchanged and deferred.

## Completion Criteria

The CodeActions unit-testing round is complete:

- [x] Every executable source file has a completed disposition.
- [x] Every unit-testable logic-bearing target reaches 100% line and branch coverage or has an explicitly approved exact defensive/runtime branch.
- [x] Data-only output properties are exercised through an owning tool or service wherever production uses them; unused surfaces are recorded for production review.
- [x] Unit tests retain visible Moq collaborators and use real Roslyn objects only as data.
- [x] Real MEF composition and provider compatibility remain integration/audit evidence.
- [x] No production hook, broader interface or reflection path exists solely for tests.
- [x] Test categorisation and the fast-loop filter remain correct.
- [x] Formatting, build, focused coverage and the full regression suite are green.

## Measurement Commands

```bash
dotnet test test/Roslyn.Workbench.Mcp.CodeActions.Test/Roslyn.Workbench.Mcp.CodeActions.Test.csproj --collect:"XPlat Code Coverage" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest.csproj --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test test/Roslyn.Workbench.Mcp.CodeActions.AuditTest/Roslyn.Workbench.Mcp.CodeActions.AuditTest.csproj --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```
