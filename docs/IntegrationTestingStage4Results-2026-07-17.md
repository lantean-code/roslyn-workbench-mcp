# Integration Testing Stage 4 Results

Date: 17 July 2026

This document records the before-and-after evidence and retention decisions for Stage 4 of the integration-testing implementation plan. Results are added by component substage so the migration remains reviewable and independently committable.

## 4A: Workspace component integration

### Baseline

The committed Stage 3 Workspace integration project passed 65 tests in 19 seconds of VSTest-reported duration. The measured command took 21.38 seconds wall time with a peak resident set of 363,068 KiB.

### Retention and restructuring

The retained tests continue to exercise boundaries that depend on real MSBuild evaluation, Roslyn workspaces, filesystem state, durable recovery data, native atomic replacement or inter-process locking:

- Atomic creation and replacement now assert exact non-text byte sequences.
- Durable commit and recovery scenarios remain isolated in `DurableWorkspaceCommitIntegrationTests`.
- Lifecycle scenarios cover real open/close, multiple workspaces, compatibility diagnostics, advisory instance state and recovery blocking.
- External-change scenarios cover changed and added source inputs, imported build inputs, editor configuration, reload and malformed reload diagnostics.
- Transaction scenarios cover ownership, staging and preview, history movement, rollback, commit, multi-file persistence, encoding preservation and real external conflict.
- Resolver scenarios retain real project/document ambiguity and source resolution, and add explicit metadata-symbol and referenced-project documentation-ID resolution.

`WorkspaceCoordinatorIntegrationTests` was split into `WorkspaceLifecycleIntegrationTests`, `WorkspaceExternalChangeIntegrationTests` and `WorkspaceTransactionIntegrationTests`. Mutation and durable recovery scenarios remain isolated and no mutable Workspace fixture is shared between tests.

The retained transaction tests stage Roslyn solutions through the Workspace mutation lease. They no longer construct plugin catalogues, invoke an in-process MCP tool harness or deserialize MCP response envelopes.

### Removed duplication

The Workspace project removed 13 net integration cases while adding two missing resolver boundary cases:

- Coordinator cancellation, unloaded-close, missing-transaction, configured-query-context and in-flight status branches duplicate focused lifecycle, execution-context and operation-gate unit tests.
- Redo-history truncation, unsupported mutation shapes and zero-context diff rendering duplicate focused transaction model, candidate validator and diff unit tests.
- Resolver not-found and snapshot match/mismatch branches duplicate focused resolver and snapshot-guard unit tests.
- The missing-project manifest branch was a direct filesystem comparison branch; real imported-project evaluation and malformed evaluation remain.

The atomic-writer failure case was replaced rather than removed: collaborator-driven cleanup failure remains unit-tested, while component integration now covers both native create and native replace behaviour.

### Result

After migration, the Workspace integration project passes 52 tests in 9 seconds of VSTest-reported duration. The measured command took 15.18 seconds wall time with a peak resident set of 360,348 KiB.

Compared with the committed baseline:

- Integration case count decreased from 65 to 52 (20%).
- VSTest-reported duration decreased from 19 seconds to 9 seconds (53%).
- Measured wall time decreased from 21.38 seconds to 15.18 seconds (29%).
- Peak resident memory remained effectively flat, decreasing from 363,068 KiB to 360,348 KiB (less than 1%).

Verification:

- Workspace unit tests: 631 passed.
- Workspace component integration tests: 52 passed.
- Repository-wide tests: 1,966 passed, including all 10 published-host acceptance tests run against the explicitly published Debug executable.

## 4B: Plugins.Core component integration

### Baseline

The committed Stage 3 Plugins.Core integration project passed 21 tests in 4 seconds of VSTest-reported duration. The warm measured command took 6.15 seconds wall time with a peak resident set of 387,284 KiB.

### Retention and restructuring

The retained cases cover projection of a real solution, project and document; diagnostics and representative operation/control-flow semantics; cross-project search and dependency relationships; real selector ambiguity; metadata definition projection and bounded search; bundled rename, sort and format staging; solution formats; and imported MSBuild properties.

The stale-snapshot selector case was removed because focused Workspace snapshot and Plugins.Core handler unit tests own that response branch. Real ambiguous text selection remains because its outcome depends on the loaded document.

The implementation plan assumed that malformed, missing and cancellation paths in `DefaultProjectStructureService` already had service unit coverage. No such unit suite exists, and several paths depend directly on solution persistence, MSBuild evaluation or the filesystem. Those cases remain under the Stage 4 general retention rule. The former 13-case class was split into `SolutionHierarchyServiceIntegrationTests` and `ProjectTargetFrameworkServiceIntegrationTests`, making the external capability boundary explicit without discarding unique coverage.

### Result

After migration, the Plugins.Core integration project passes 20 tests in 4 seconds of VSTest-reported duration. The warm measured command took 5.62 seconds wall time with a peak resident set of 391,612 KiB.

Compared with the committed baseline:

- Integration case count decreased from 21 to 20 (5%).
- Measured wall time decreased from 6.15 seconds to 5.62 seconds (9%).
- Peak resident memory remained effectively flat, increasing by approximately 1%.

Verification:

- Plugins.Core unit tests: 269 passed.
- Plugins.Core component integration tests: 20 passed.

## 4C: CodeActions component integration

### Baseline

The committed Stage 3 CodeActions integration project passed 11 tests in 14 seconds of VSTest-reported duration. The warm measured command took 16.12 seconds wall time with a peak resident set of 508,864 KiB.

### Retention and restructuring

The retained cases cover one real MEF composition with typed controlled providers, controlled list/describe/stage workflows, controlled solution fix-all, and one supported built-in provider reaching Workspace staging. Provider families remain separated between MEF composition, controlled providers and built-in providers, and each mutation uses its own materialised Workspace.

The three real MEF checks were consolidated into one composition flow that asserts host services and typed refactoring/code-fix exports. Token tampering, expiry, action staleness and snapshot mismatch cases were removed because focused token, resolution and selector unit suites own those branches. The fix-all theory was narrowed from document, project and solution repetitions to one real solution-wide flow; scope mapping remains unit-tested.

Controlled-provider tests share one immutable provider catalogue at class scope. xUnit serialises methods in the class, while every test retains an independent mutable Workspace and coordinator.

The retained component workflows now use typed Code Action requests through `CodeActionComponentTestSession`. They no longer construct MCP server tools, bind JSON arguments or deserialize published envelopes. Published transport behaviour remains covered by Stage 3 acceptance tests.

### Result

After migration, the CodeActions integration project passes 5 tests in 7 seconds of VSTest-reported duration. The warm measured command took 8.41 seconds wall time with a peak resident set of 391,808 KiB.

Compared with the committed baseline:

- Integration case count decreased from 11 to 5 (55%).
- VSTest-reported duration decreased from 14 seconds to 7 seconds (50%).
- Measured wall time decreased from 16.12 seconds to 8.41 seconds (48%).
- Peak resident memory decreased from 508,864 KiB to 391,808 KiB (23%).

Verification:

- CodeActions unit tests: 498 passed.
- CodeActions component integration tests: 5 passed.
- Repository-wide tests after 4B and 4C: 1,959 passed, including all 10 published-host acceptance tests and all 95 Code Action audit tests.

## 4D: Host component integration

### Baseline

The committed Stage 3 Host integration project passed 23 tests in 4 seconds of VSTest-reported duration. The warm measured command took 5.80 seconds wall time with a peak resident set of 371,156 KiB.

The Host unit-test inventory separately identified 24 cases that exercised production Host composition, Generic Host lifecycle ordering, process-global MSBuild registration, real MEF composition, PE metadata or assembly-load contexts. Those cases were previously included in the Host unit project even though their evidence was integration-owned.

### Retention and restructuring

The retained and reclassified cases now name the real Host boundary they prove:

- `HostCompositionIntegrationTests` and `HostToolCompositionIntegrationTests` cover production DI validation, singleton registration, complete tool-catalogue composition, server-owned versus plugin-owned status, request-filter composition and projection of valid or fallback startup options into dependent options.
- `PluginPackageDiscoveryIntegrationTests`, `PluginCatalogBootstrapIntegrationTests` and `PluginAssemblyMetadataReaderIntegrationTests` cover real package enumeration, collision and isolation policy, bundled default-context bootstrap, PE metadata, malformed assemblies, marker cardinality and metadata validation.
- `MefPluginComposerIntegrationTests` covers zero, one and multiple real MEF exports; complete Host and bundled-catalogue composition retain closed generic tool materialisation.
- `PluginAssemblyLoadContextIntegrationTests` covers load-context creation, shared contract identity, default-context delegation, private dependency routing and package containment.
- `McpSdkSchemaProviderIntegrationTests` retains compatibility evidence against the real MCP SDK exporter and its cache.
- `MsBuildRegistrationServiceIntegrationTests` covers the process-global registration state, while `StartupPrerequisiteLifecycleServiceIntegrationTests` covers real Generic Host ordering before transport startup.
- `ServerStatusRecoveryIntegrationTests` now reads a real persisted recovery record through `ServerStatusService`; serialised status shape remains contract and published-host acceptance evidence.

All 24 boundary cases recorded by `HostUnitTestInventory.md` were moved from the Host unit project into classes with the `IntegrationTests` suffix. The unit project no longer references or copies the plugin fixture projects that existed only for those cases.

### Removed duplication

Eight direct-protocol component cases were removed after their replacement evidence became available:

- The representative Workspace query and Code Action list/stage cases are covered over stdio by the Stage 3 Workspace and Code Action acceptance workflows.
- The Workspace lifecycle and transaction MCP workflows are covered over stdio by the Stage 3 acceptance suite; detailed lifecycle, history and persistence behaviour remains in the owning component suites.
- The invalid-binding and throwing-handler cases duplicate focused Host request-binding, server-tool and exception-filter unit coverage.
- External query publication and invocation are covered by the external-plugin acceptance workflow. Private dependency routing remains in load-context integration coverage.
- Packaged mutation discovery remains in `PluginPackageDiscoveryIntegrationTests`; mutation acquisition, result mapping and staging are covered by Host unit tests and a public mutation workflow is covered by acceptance.

The recovery case was retained but narrowed from an in-process MCP envelope assertion to the unique persistence-to-service mapping boundary. No retained Host component test consumes the test-owned MCP invocation harness; removal of the now-orphaned harness belongs to Stage 5.

One options-fallback composition case was added because the retained Host suite previously projected only valid command-line options and therefore did not prove that fallback values and warnings survived production composition.

### Result

After migration, the Host integration project passes 40 tests in 1 second of VSTest-reported duration. The warm measured command took 2.74 seconds wall time with a peak resident set of 519,300 KiB.

Compared with the committed baseline:

- Integration case count increased from 23 to 40 because 24 existing boundary cases were correctly classified, eight duplicated protocol cases were removed and one missing fallback-composition case was added.
- VSTest-reported duration decreased from 4 seconds to 1 second (75%).
- Measured wall time decreased from 5.80 seconds to 2.74 seconds (53%).
- Peak resident memory increased from 371,156 KiB to 519,300 KiB (40%); the moved PE compilation and composition cases now run in the integration assembly, and memory/concurrency optimisation remains Stage 7 work rather than a timing assertion.

Verification:

- Host unit tests: 275 passed.
- Host component integration tests: 40 passed.
- Repository-wide tests: 1,952 passed, including all 10 published-host acceptance tests and all 95 Code Action audit tests.
