# Test Architecture Re-audit

Date: 2026-07-18

Status: Current architecture record after the integration-testing redesign

## Outcome

The redesign is complete. The suite now separates isolated Unit/Contract evidence, owner-aligned component integration, published-process acceptance and Roslyn compatibility audit coverage. The audit found no production-boundary, isolation, lifetime or scenario-retention gap.

One CI portability defect was found and corrected during the audit: the acceptance project located its external plugin fixture through the agent-only `ArtifactsPath` layout. It now asks MSBuild for the fixture project's `GetTargetPath` and derives the package assets beside that resolved output. A fresh isolated build copied exactly the fixture assembly, dependency manifest and private dependency without relying on a particular output root.

This document supersedes the current-state conclusions and counts in `TestArchitectureReaudit-2026-07-10.md`. That file remains historical evidence.

## Current Project Topology

| Layer | Projects | Tests | Responsibility |
| --- | --: | --: | --- |
| Unit/Contract | 5 | 1,732 | Reachable behaviour, branches, public contracts, MCP schemas and isolated adapters |
| Component integration | 4 | 117 | Real owner boundaries: filesystem/MSBuild/Roslyn, composition and assembly loading |
| Published-Host acceptance | 1 | 10 | Published executable, official MCP client, stdio and representative public workflows |
| Compatibility audit | 1 | 93 | Version-sensitive built-in Roslyn provider discovery and replay compatibility |
| Total | 11 | 1,952 | Complete test solution |

Support and fixture projects are not test assemblies. `Roslyn.Workbench.Mcp.TestSupport` remains unit-safe; `Roslyn.Workbench.Mcp.IntegrationTestSupport` provides asset, lifetime, MSBuild and controlled-provider support without composing MCP or the production Host. Plugin fixture projects live under `test/TestFixtures/Plugins` and are built only as dependencies of the owning integration or acceptance project.

## Named Component Boundaries

Every component class name identifies a real boundary; the table records the concrete boundary and prevents the suite from being interpreted as a per-tool matrix.

| Owner | Classes | Boundary protected |
| --- | --- | --- |
| Workspace | `WorkspaceAssetMaterializerIntegrationTests`, `AtomicFileWriterIntegrationTests` | Exact filesystem copying, overlays, deletion and atomic replacement |
| Workspace | `WorkspaceLifecycleIntegrationTests`, `WorkspaceResolverIntegrationTests` | MSBuild-backed loading, multi-workspace lifecycle, advisory instance status and real Roslyn resolution |
| Workspace | `WorkspaceTransactionIntegrationTests`, `DurableWorkspaceCommitIntegrationTests` | Real-file commit, rollback, history restoration, journal recovery, divergence and inter-process locking |
| Workspace | `WorkspaceExternalChangeIntegrationTests`, `WorkspaceChangeDetectorIntegrationTests` | Filesystem/import manifests, out-of-date detection and reload |
| Workspace | `WorkspaceProjectCompatibilityInspectorIntegrationTests` | Real MSBuild project compatibility inspection |
| Plugins.Core | `WorkspaceProjectionIntegrationTests`, `SemanticInspectionIntegrationTests` | Real Roslyn workspace, semantic model, operation and control-flow projection |
| Plugins.Core | `SolutionSearchIntegrationTests`, `SelectorAndSnapshotIntegrationTests` | Cross-project Roslyn relationships, metadata symbols, ambiguity, snapshots and bounded results |
| Plugins.Core | `SolutionHierarchyServiceIntegrationTests`, `ProjectTargetFrameworkServiceIntegrationTests` | Real solution persistence and MSBuild target-framework evaluation |
| Plugins.Core | `MutationPipelineIntegrationTests` | Bundled Roslyn mutation proposals staged through a real Workspace transaction |
| CodeActions | `ControlledProviderWorkflowIntegrationTests`, `BuiltInCodeActionStagingIntegrationTests` | Controlled and representative bundled providers across discovery, replay, fix-all and staging |
| CodeActions | `MefCodeActionProviderCatalogIntegrationTests` | Real MEF provider composition |
| Host | `HostCompositionIntegrationTests`, `HostToolCompositionIntegrationTests` | Production DI graph and all four typed Host adapter families without transport emulation |
| Host | `PluginPackageDiscoveryIntegrationTests`, `PluginCatalogBootstrapIntegrationTests` | Real package enumeration, bundled catalogue materialisation, collisions and failure isolation |
| Host | `PluginAssemblyMetadataReaderIntegrationTests`, `PluginAssemblyLoadContextIntegrationTests` | Real PE metadata and shared/private managed and unmanaged dependency routing |
| Host | `MefPluginComposerIntegrationTests` | Real MEF plugin composition |
| Host | `MsBuildRegistrationServiceIntegrationTests`, `StartupPrerequisiteLifecycleServiceIntegrationTests` | Process-global MSBuild registration and Generic Host lifecycle ordering |
| Host | `McpSdkSchemaProviderIntegrationTests` | Real MCP SDK schema export and caching |
| Host | `ServerStatusRecoveryIntegrationTests` | Persisted recovery state mapped through the Host service boundary |

## Acceptance Boundaries

| Class | Published boundary protected |
| --- | --- |
| `PublishedHostExecutableIntegrationTests` | Actionable prerequisite failures for a missing published executable |
| `StdioStartupIntegrationTests` | Startup failure and captured stderr through the official client transport |
| `PublishedHostProtocolIntegrationTests` | Initialisation, catalogue, schemas and server status over stdio |
| `WorkspaceWorkflowIntegrationTests` | Workspace lifecycle, semantic query and transactional plugin mutation over public MCP |
| `CodeActionWorkflowIntegrationTests` | Built-in Code Action list, stage and rollback over public MCP |
| `ExternalPluginWorkflowIntegrationTests` | External package discovery, private dependency loading and invocation over stdio |
| `StartupAndRecoveryWorkflowIntegrationTests` | Configuration fallback and persisted recovery across published-Host restarts |
| `PublishedHostLifetimeIntegrationTests` | Graceful published-process shutdown when stdin reaches end-of-stream |

Acceptance references only the MCP client/test packages and one fixture project with `ReferenceOutputAssembly=false`. It has no production project reference, Moq dependency or `InternalsVisibleTo` access and never invokes a production tool object directly.

## Architecture Findings

- Production MCP package references and transport composition remain confined to `Roslyn.Workbench.Mcp`; acceptance references the official client package only as an external consumer.
- Component support registers Workspace plus only the Plugin or Code Action owner path required by a scenario. It does not call the Host composition root, create an MCP server/client or emulate stdio.
- All mutable scenarios receive a copied workspace and unique state/recovery root. Reused catalogues are immutable.
- Stateful fixtures implement deterministic synchronous or asynchronous disposal. `ComponentWorkspace` closes published instance state, disposes every loaded Workspace, then disposes its service provider and owned state directory. Child processes use bounded termination and disposal paths.
- Unit inventories retain the branch dispositions that replaced removed wrapper-style integration scenarios. The Host boundary migration left 277 isolated Host tests plus 40 real Host boundary tests; no boundary case is being counted as unit isolation.
- Transaction success, rollback, history restoration, crash recovery, partial-commit divergence and same/different-root locking retain real-filesystem evidence.
- Plugin discovery retains real fixture assemblies, PE metadata, MEF and load-context/private-dependency evidence.
- Controlled providers, fix-all, a representative bundled provider and MEF Code Action discovery retain component evidence.
- MCP tool names, request/response schemas and JSON contracts remain owned by Contract tests and are also sampled through published-Host catalogue/workflow acceptance.
- `test/Directory.Build.targets` enforces Unit/Contract, Integration, Acceptance and Audit project categories and rejects unit-safe references to integration support.

## Dependency Update Evidence

Stage 8 updated `Microsoft.Build` and `Microsoft.Build.Framework` from 18.7.1 to 18.8.2 before the final audit. Both remain compile/API references with runtime assets excluded; `Microsoft.Build.Locator` continues to select the installed SDK's MSBuild implementation. Restore, solution build and the MSBuild-sensitive Workspace, Plugins.Core and Host integration projects all passed against the new package versions before documentation work began.

## Performance and Platform Position

The retained component suite has 117 tests across 30 classes. Its Stage 7 median sequential wall time is 27.57 seconds with project median peak memory between 344.2 MiB and 506.5 MiB, a 42.8% improvement over the 48.23-second Stage 0 baseline. The four-owner concurrent proxy has a 12.84-second median critical path. The compatibility audit retains 93 tests with a 53.28-second median and 1,074.2 MiB median peak memory. Acceptance remains deliberately small at 10 tests.

Linux covers all component owners and published acceptance. Pull-request CI adds Windows published acceptance and the full Workspace durability project; scheduled macOS coverage is non-gating pending operational evidence.

## Deferred Decisions

- MTP v2 is the intended future runner direction, but migration remains deferred until xUnit 4 is stable.
- Collection-scoped concurrent Workspace reuse requires separate thread-safety evidence.
- NuGet lock files and package caching remain a dependency-policy decision.
- macOS pull-request gating remains dependent on scheduled reliability evidence.
- Cross-instance mutation guidance remains a product/tool-description decision; the readable advisory status file is now supported on Windows.

Final command results and changed-file checks are recorded in `IntegrationTestingStage8Results-2026-07-18.md`.
