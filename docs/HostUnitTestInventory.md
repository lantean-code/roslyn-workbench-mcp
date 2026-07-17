# Host Unit-Test Inventory

Date: 2026-07-17

Status: Unit testing complete; integration-test redesign deferred

## Purpose

This document inventories unit-test coverage for `Roslyn.Workbench.Mcp` after completion of the H1-H7 Host architecture review. It separates isolated unit evidence from tests that currently exercise real Host, MSBuild, PE-metadata, MEF or assembly-load-context boundaries, records the measured baseline, and identifies the remaining decisions before the Host unit-testing round can be closed.

The inventory follows `TestingStrategy.md` and `test/AGENTS.md`:

- use xUnit, Moq and AwesomeAssertions;
- keep production collaborators as visible mocks;
- do not build the production Host or exercise real filesystem, MSBuild, MEF, PE-metadata or load-context boundaries in unit tests;
- do not use reflection to invoke implementation code or create test-only production seams;
- require 100% line and branch coverage for selected unit-testable implementations unless an exact defensive or external-runtime branch is approved and recorded here; and
- defer the integration-test redesign until the remaining structural and unit-test work is stable.

## Architecture Handoff

The H1-H7 re-audit found no unresolved Host responsibility or dependency issue. Tests can now target stable responsibilities:

- startup option parsing, fallback configuration and warning projection are separate from runtime composition;
- the Host owns MCP schemas, request binding, publication and exception filtering;
- plugin and Code Action registration visitors retain their closed generic types;
- the four MCP adapters separately own query or mutation acquisition and result mapping;
- plugin discovery, preparation, collision policy and catalogue orchestration have isolated collaborators;
- Code Actions remain outside plugin discovery and plugin status; and
- server-owned lifecycle and transaction tools adapt Workspace results without leaking transport concerns into Workspace.

No production change is authorised by this inventory. Any proposed seam or defensive-branch cleanup must be called out for confirmation before implementation.

## Measured Baseline

Two measurements are required because 24 currently uncategorised tests cross boundaries that `TestingStrategy.md` assigns to integration coverage.

| Measure | Fast-loop project result | Isolation-only result |
| --- | ---: | ---: |
| Executed tests | 300 | 276 |
| Production C# files | 118 | 118 |
| Files with executable sequence points | 93 | 93 |
| Interfaces, enums and other files without sequence points | 25 | 25 |
| Covered executable lines | 2,741 / 2,808 | 2,315 / 2,808 |
| Line coverage | 97.61% | 82.44% |
| Covered branches | 520 / 538 | 454 / 538 |
| Branch coverage | 96.65% | 84.39% |
| Executable files at 100% line and branch | 87 | 82 |
| Executable files with partial coverage | 5 | 1 |
| Executable files with zero coverage | 1 | 10 |

The isolation-only result excludes tests that build the production Host, perform real MEF composition, inspect real PE metadata, route through a real `AssemblyLoadContext`, use the process-global `MSBuildLocator`, or prove Generic Host lifecycle ordering. The lower assembly percentage therefore does not identify ten missing unit-test suites: those ten files are integration boundaries. It shows that the remaining isolated Host implementation consists of 82 fully covered executable files and one file with a compiler-generated defensive branch gap.

Coverlet emits compiler-generated async classes separately. The figures in this document aggregate their sequence points into the owning source file.

## Existing Test Quality

The isolated Host tests generally follow the active unit-test pattern:

- no `Mock.Of<T>`, prohibited `null!` input, `Task.Delay`, invocation-history inspection, test comments or reflection-based implementation invocation is present;
- shared and local mocks use explicit `new Mock<T>()` construction;
- collaborator mocks and scenario-specific setup remain visible in test classes;
- request binding, protocol construction, result publication and all four typed MCP adapters are exercised through supported entry points;
- shared helpers construct protocol data and repeatable Moq objects without hiding scenario-specific setup or assertions;
- deliberate schema reflection is restricted to `Category=Contract` tests; and
- the architecture test uses assembly metadata only to locate the repository before inspecting project files.

The material pattern issue is categorisation. The following tests are useful boundary evidence but currently have ordinary unit names and no `Integration` category:

| Current test | Boundary exercised | Test cases |
| --- | --- | ---: |
| `HostToolCompositionTests` | Production Host DI and MCP composition | 2 |
| `MefPluginComposerTests` | Real MEF composition | 3 |
| `PluginCatalogBootstrapTests` | Bundled plugin composition and materialisation | 1 |
| `PluginAssemblyLoadContextTests` | Real assembly loading, dependency resolution and load-context identity | 6 |
| `PluginAssemblyMetadataReaderTests` | Real PE metadata and dynamically compiled assemblies | 9 |
| `MsBuildRegistrationServiceTests` | Process-global `MSBuildLocator` state | 2 |
| `StartupPrerequisiteLifecycleServiceTests.GIVEN_HostedTransport...` | Real Generic Host lifecycle ordering | 1 |

These 24 tests should not be deleted. They should be moved or recategorised when the integration-test structure is redesigned. Until then, coverage produced by them is reported separately and does not count as isolated unit evidence.

## Completed Unit-Coverage Families

Every executable file in the following families measures 100% line and branch coverage in the isolation-only run.

| Family | Covered targets | Disposition |
| --- | --- | --- |
| Startup configuration | `StartupConfigurationReporter`, `StartupConfigurationSnapshot`, `StartupOptions`, `StartupOptionsResolver`, `StartupOptionsRules`, `StartupOptionsValidator` | Complete |
| Host-owned results and validation | Tool outcomes, result envelopes, server and transaction DTO behaviour, and `ContractValidator` | Complete through owning services and contract tests |
| Isolated hosting behaviour | `HostConfiguredMsBuildWorkspaceFactory`, `HostStartupComposer`, `HostStartupComposition`, and the mocked lifecycle paths of `StartupPrerequisiteLifecycleService` | Complete |
| Plugin preparation orchestration | `LoadedPluginPreparer`, `PluginCandidatePreparer`, preparation and inspection results, status creation, entry-point validation and preparation diagnostics | Complete |
| Plugin collision and catalogue orchestration | `PluginCollisionPolicy`, `PluginCatalogLoader`, `PluginCatalogEntryMaterializer`, catalogue snapshots and materialisation results | Complete |
| Mocked package discovery | `PluginPackageDiscovery` and `PluginPackagePathPolicy` with visible `IFileSystem`, metadata-reader and path-policy mocks | Complete; real package discovery remains integration evidence |
| Plugin handler transport inspection | `QueryResponseContractInspector` and closed generic catalogue materialisation | Complete |
| MCP protocol | `McpPublishedResultSerializer`, `McpToolProtocolFactory`, `ToolRequestBinder`, `ToolSchemaBuilder` and `ToolSchemaFactory` | Complete; real SDK schema export remains integration evidence |
| Typed registration visitors | Plugin query/mutation and Code Action query/mutation visitor dispatch | Complete |
| MCP execution adapters | All four query/mutation server tools, including acquisition, rejection, handler results, staging, cancellation and exceptions | Complete |
| Shared Host execution | `McpServerToolBase` and `UnhandledToolExceptionFilter` | Complete |
| Server-owned tools | Registration, base behaviour, status, Workspace lifecycle and transaction tools, plus `WorkspaceToolResultMapper` | Complete |

Data-only contracts that execute through these tests do not need one test per property. Their values should remain assertions of the services and tools that consume or publish them.

## External and Composition Boundaries

The isolation-only zero-coverage files all require real runtime or composition behaviour.

| Target | Isolation line | Isolation branch | Required evidence |
| --- | ---: | ---: | --- |
| `Program` | 0 / 6 | No branches | Process startup and stdio Host integration; do not invoke the private entry point by reflection. |
| `RoslynWorkbenchHostApplicationBuilderExtensions` | 0 / 31 | No branches | Production Host composition and published MCP tool set. |
| `RoslynWorkbenchServiceCollectionExtensions` | 0 / 125 | 0 / 4 | DI registration, container validation and singleton lifetime integration. |
| `MsBuildRegistrationService` | 0 / 53 | 0 / 6 | First registration, prior registration and unavailable SDK behaviour against process-global `MSBuildLocator`. |
| `McpSdkSchemaProvider` | 0 / 50 | 0 / 10 | Real MCP SDK schema export and caching. Focused integration tests already exist for supported request, value and bounded-collection contracts. |
| `MefPluginComposer` | 0 / 14 | 0 / 4 | Zero, one and multiple real MEF export composition. |
| `PluginAssemblyMetadataReader` | 0 / 137 | 0 / 36 | Real managed PE metadata, malformed metadata and marker cardinality. |
| `PluginAssemblyLoadContext` | 0 / 39 | 0 / 20 | Shared/private managed dependency routing, unmanaged dependency routing and containment. |
| `PluginLoadContextFactory` | 0 / 12 | 0 / 2 | Creation of the real non-collectible plugin load context. |
| `PluginCatalogBootstrap` | 0 / 26 | No branches | Bundled default-context composition through the complete plugin catalogue path. |

The fast-loop run covers portions of these files and raises the raw assembly result to 97.61% line coverage. That is useful regression evidence, but it must not be used to claim unit isolation.

## Remaining Isolated Gap

| Target | Isolation line | Isolation branch | Complexity | Remaining work |
| --- | ---: | ---: | --- | --- |
| `ServerStatusService` | 56 / 56 (100%) | 12 / 14 (85.71%) | Approved framework-nullable branch | All supported summary/full, recovery, catalogue, configuration caching, availability and cancellation behaviour is covered. `AssemblyName.Version` is nullable, so the public status fields now preserve that nullable contract and publish explicit JSON nulls. The two missing branches are the null-propagation paths for the concrete Host and Roslyn assemblies; both assemblies carry version metadata in supported deployments. |

No ordinary unit-testable Host behaviour is otherwise missing from the isolation-only result.

## Files Requiring No Direct Tests

The 25 production C# files without executable sequence points are interfaces, enums, global imports or marker declarations. They are covered through compilation, typed visitor dispatch and the behaviour of their consumers. They must not receive reflection-only shape tests.

Data-only request and response records with generated accessors are asserted through their owning tools and serializers. An unused property discovered during owner-based testing is a production-design finding, not a reason for a property-only test.

## Decisions Requiring Approval

1. Resolved: `ServerStatusData.ServerVersion` and `RoslynVersion` now mirror the framework's nullable `AssemblyName.Version` contract and publish explicit JSON nulls when metadata is unavailable. The two concrete-assembly null paths remain approved external-runtime gaps; no test-only assembly/version seam is introduced.
2. Keep the 24 boundary tests unchanged until the integration-test redesign, while excluding them from unit-coverage claims; then move or recategorise them as part of that redesign. Moving them now would begin the explicitly deferred integration restructuring.
3. Treat the ten external/composition files as integration-owned rather than introducing wrappers around `MSBuildLocator`, the MCP SDK, MEF, PE metadata or `AssemblyLoadContext` solely to improve unit percentages.

No production change should be made during the testing phase without explicit confirmation.

## Recommended Delivery Order

### HUT1: Close the isolated coverage disposition

Complete. The contract now preserves nullable framework version metadata, explicit-null serialisation is locked by a contract test, and the two concrete-assembly null paths are recorded as approved external-runtime branches.

### HUT2: Final Host unit-pattern audit

Complete. The isolated coverage filter and prohibited-pattern scans are green. All ordinary unit-testable Host behaviour remains at 100% line and branch coverage, with the exact approved external-runtime branches recorded above.

### Deferred Host integration redesign

Move or categorise the 24 boundary tests, then add the missing real-boundary scenarios in the integration phase:

- process startup and stdio transport;
- real Host DI and MCP publication;
- MSBuild registration outcomes;
- MCP SDK schema export;
- MEF composition and bundled/default-context loading;
- managed and unmanaged plugin dependency routing;
- PE metadata discovery and malformed package isolation; and
- durable recovery and representative query/mutation execution paths.

## Final Audit Result

The final HUT2 audit completed successfully on 2026-07-17:

- all 276 isolation-only tests passed;
- 82 isolated executable files remain at 100% line and branch coverage;
- `ServerStatusService` remains at 100% line coverage with only the two approved framework-nullable assembly-version branches;
- all ten zero-coverage files match the external or composition boundaries recorded above;
- no `Mock.Of<T>`, prohibited `null!` input, `Task.Delay`, invocation-history assertion, test comment, expression-bodied test member or invalid test name was found;
- no constructor null guard, null-forgiving operator or expression-bodied method was found in Host production source; and
- changed governed files pass the diff and CRLF checks.

## Completion Criteria

- [x] Every executable Host source file has a unit or external-boundary disposition.
- [x] Every ordinary unit-testable logic-bearing target reaches 100% line and branch coverage.
- [x] Existing unit tests retain visible Moq collaborators and supported behavioural entry points.
- [x] Real Host, MSBuild, MEF, PE metadata and load-context execution is separated from isolated coverage reporting.
- [x] The exact `ServerStatusService` framework-nullable gap is approved and recorded.
- [x] The final isolated coverage and pattern audit is green.
- [ ] Boundary tests are moved or recategorised during the deferred integration-test redesign.

## Measurement Commands

Fast-loop project result:

```bash
dotnet test test/Roslyn.Workbench.Mcp.Test/Roslyn.Workbench.Mcp.Test.csproj --filter "Category!=Integration&Category!=Audit" --collect:"XPlat Code Coverage" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

Isolation-only analysis additionally excluded the seven boundary-test entries listed under Existing Test Quality. That exclusion is an audit measurement, not the intended long-term command. Once those tests are correctly categorised, the normal fast-loop filter will produce the isolation-only result without a class-name exclusion list.
