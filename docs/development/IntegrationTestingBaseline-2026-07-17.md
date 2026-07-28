# Integration Testing Baseline Evidence

Date: 2026-07-17 Status: Pre-Stage 0 baseline Revision: `3e405e82cb8fb1eedfe257642809393c0e01b5ed`

## Purpose

This document records the reproducible performance baseline captured before the integration-testing redesign began. It replaces the one-off diagnostic timings in `IntegrationTestingStrategyProposal.md` with three warm measurements of every current integration and audit project.

The evidence is intended for comparison after lifecycle hardening, fixture migration, scenario consolidation and safe fixture reuse. It is not a portable performance target or a test assertion.

## Environment

| Item                     | Value                                          |
| ------------------------ | ---------------------------------------------- |
| Operating system         | Ubuntu 24.04.4 LTS under WSL2                  |
| Kernel                   | Linux 6.18.33.2-microsoft-standard-WSL2 x86_64 |
| .NET SDK                 | 10.0.102                                       |
| Target framework         | `net10.0`                                      |
| Build configuration      | Debug                                          |
| Test runner              | VSTest 18.0.1, x64                             |
| Test authoring framework | xUnit v3                                       |

The working tree contained pre-existing documentation changes when the measurements were taken. The solution built successfully with no warnings or errors before the measured runs.

## Method

The solution was restored and built once:

```bash
dotnet restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet build --no-restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

Each project was then run three times sequentially without rebuilding or restoring. Every run used the following command shape:

```bash
/usr/bin/time -v -o <timing-file> \
  dotnet test <project> \
  --no-build \
  --no-restore \
  --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp \
  --results-directory <run-directory> \
  --logger "trx;LogFileName=results.trx"
```

Wall time and maximum resident set size are the values reported by GNU `time` for the `dotnet test` command. Project medians are the middle values from the three runs. The slowest-test tables use each test case's median TRX duration across the three runs, so a single noisy run does not determine its position.

## Project Summary

| Project | Tests | Wall time runs | Median wall time | Peak RSS runs | Median peak RSS |
| --- | --: | --- | --: | --- | --: |
| Workspace integration | 62 | 21.81 s, 21.02 s, 20.94 s | 21.02 s | 383.2 MiB, 356.4 MiB, 368.3 MiB | 368.3 MiB |
| Plugins.Core integration | 21 | 5.92 s, 5.89 s, 5.90 s | 5.90 s | 386.8 MiB, 391.7 MiB, 391.0 MiB | 391.0 MiB |
| CodeActions integration | 11 | 15.87 s, 15.72 s, 15.65 s | 15.72 s | 496.8 MiB, 491.0 MiB, 487.6 MiB | 491.0 MiB |
| Host integration | 23 | 5.59 s, 5.64 s, 5.59 s | 5.59 s | 364.9 MiB, 355.8 MiB, 363.0 MiB | 363.0 MiB |
| CodeActions audit | 95 | 57.23 s, 57.03 s, 57.47 s | 57.23 s | 1,093.2 MiB, 1,124.9 MiB, 1,155.3 MiB | 1,124.9 MiB |

All 15 runs passed without failed or skipped tests. The four normal integration-project medians total 48.23 seconds when run sequentially. They contain 117 tests; the audit adds 95 tests.

## Ten Slowest Tests by Project

### Workspace Integration

| Rank | Median | Test case |
| --: | --: | --- |
| 1 | 1.468 s | `WorkspaceCoordinatorIntegrationTests.GIVEN_TwoOpenedWorkspaces_WHEN_StartingTransactionOnSecondWorkspace_THEN_ShouldRejectUntilOwnerRollsBack` |
| 2 | 1.448 s | `WorkspaceCoordinatorIntegrationTests.GIVEN_AnotherLiveServerInstance_WHEN_OpeningAndQueryingStatus_THEN_ShouldSurfaceItsAdvisoryState` |
| 3 | 1.419 s | `WorkspaceCoordinatorIntegrationTests.GIVEN_StagedTransaction_WHEN_MovingHistoryBackwardAndForward_THEN_ShouldUpdateCurrentRevision` |
| 4 | 1.389 s | `WorkspaceResolverIntegrationTests.GIVEN_WorkspaceRelativeProjectPath_WHEN_ResolvingProject_THEN_ShouldResolveAgainstWorkspaceRoot` |
| 5 | 1.286 s | `WorkspaceResolverIntegrationTests.GIVEN_LocationBasedSymbolSelector_WHEN_ResolvingSymbol_THEN_ShouldReturnCanonicalSymbolReference` |
| 6 | 1.167 s | `WorkspaceCoordinatorIntegrationTests.GIVEN_TwoOpenedWorkspaces_WHEN_ListingAndGettingStatus_THEN_ShouldRequireExplicitSelection` |
| 7 | 1.129 s | `WorkspaceCoordinatorIntegrationTests.GIVEN_OutOfDateWorkspace_WHEN_Reloading_THEN_ShouldTransitionBackToReady` |
| 8 | 1.038 s | `WorkspaceResolverIntegrationTests.GIVEN_AmbiguousDocumentPath_WHEN_ResolvingDocument_THEN_ShouldReturnAmbiguous` |
| 9 | 0.831 s | `WorkspaceResolverIntegrationTests.GIVEN_AmbiguousProjectSelector_WHEN_ResolvingProject_THEN_ShouldReturnAmbiguous` |
| 10 | 0.748 s | `WorkspaceCoordinatorIntegrationTests.GIVEN_QueryLeaseInFlight_WHEN_GettingWorkspaceStatus_THEN_ShouldSucceed` |

### Plugins.Core Integration

| Rank | Median | Test case |
| --: | --: | --- |
| 1 | 3.581 s | `MutationPipelineIntegrationTests.GIVEN_ActiveTransaction_WHEN_ExecutingBundledMutations_THEN_ShouldStageRevisionsAndPreviewResultingContent` |
| 2 | 3.543 s | `SemanticInspectionIntegrationTests.GIVEN_LoadedSemanticWorkspace_WHEN_InspectingDiagnosticsOperationsAndFlow_THEN_ShouldReturnRoslynProjections` |
| 3 | 3.184 s | `SolutionSearchIntegrationTests.GIVEN_CrossProjectSolution_WHEN_SearchingRelationships_THEN_ShouldResolveAcrossProjectBoundary` |
| 4 | 3.078 s | `SelectorAndSnapshotIntegrationTests.GIVEN_MetadataSymbolAndBoundedSearch_WHEN_InspectingSelectors_THEN_ShouldProjectMetadataAndTruncation` |
| 5 | 2.679 s | `WorkspaceProjectionIntegrationTests.GIVEN_LoadedProject_WHEN_ProjectingWorkspaceDetails_THEN_ShouldIncludeDocumentsOptionsAndMetadataReferences` |
| 6 | 1.225 s | `WorkspaceProjectionIntegrationTests.GIVEN_MultiProjectSolution_WHEN_ProjectingWorkspace_THEN_ShouldIncludeFoldersAndProjectReferences` |
| 7 | 0.804 s | `SelectorAndSnapshotIntegrationTests.GIVEN_AmbiguousTextSelection_WHEN_ResolvingSymbol_THEN_ShouldRejectAmbiguousLocation` |
| 8 | 0.696 s | `SelectorAndSnapshotIntegrationTests.GIVEN_StaleSnapshot_WHEN_ResolvingSymbol_THEN_ShouldRejectSnapshotMismatch` |
| 9 | 0.303 s | `DefaultProjectStructureServiceIntegrationTests.GIVEN_ProjectWithoutFilePath_WHEN_GettingTargetFrameworks_THEN_ShouldReturnSuccessfulEmptyResult` |
| 10 | 0.234 s | `DefaultProjectStructureServiceIntegrationTests.GIVEN_TargetFrameworksImportedFromProps_WHEN_GettingTargetFrameworks_THEN_ShouldReturnEvaluatedValues` |

### CodeActions Integration

| Rank | Median | Test case |
| --: | --: | --- |
| 1 | 4.010 s | `BuiltInCodeActionStagingIntegrationTests.GIVEN_BuiltInCodeFixProvider_WHEN_RemovingUnusedUsings_THEN_ShouldStageRepresentativeBuiltInMutation` |
| 2 | 3.971 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_ControlledRefactoringAndCodeFix_WHEN_StagingBoth_THEN_ShouldAdvanceRevisionsAndPreviewChanges` |
| 3 | 2.504 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_TamperedExpiredOrStaleActionTokens_WHEN_Staging_THEN_ShouldRejectEachToken` |
| 4 | 2.339 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_ControlledCodeFix_WHEN_StagingFixAllAtSupportedScope_THEN_ShouldStageRequestedScope(scopeKind: Solution)` |
| 5 | 2.267 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_ControlledCodeFix_WHEN_StagingFixAllAtSupportedScope_THEN_ShouldStageRequestedScope(scopeKind: Project)` |
| 6 | 1.244 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_ControlledCodeFix_WHEN_StagingFixAllAtSupportedScope_THEN_ShouldStageRequestedScope(scopeKind: Document)` |
| 7 | 1.223 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_ControlledProviderActions_WHEN_ListingDescribingAndStagingParameterisedAction_THEN_ShouldPreserveWorkflowContracts` |
| 8 | 0.798 s | `ControlledProviderWorkflowIntegrationTests.GIVEN_StaleSnapshot_WHEN_ListingControlledActions_THEN_ShouldRejectSnapshotMismatch` |
| 9 | 0.527 s | `MefCodeActionCompositionIntegrationTests.GIVEN_RoslynMefHost_WHEN_ReadingExportsThroughCompatibilityAdapter_THEN_ShouldReturnTypedProviders` |
| 10 | 0.086 s | `MefCodeActionCompositionIntegrationTests.GIVEN_TestProviderAssembly_WHEN_CreatingCatalog_THEN_ShouldReportAvailableStatus` |

### Host Integration

| Rank | Median | Test case |
| --: | --: | --- |
| 1 | 3.164 s | `WorkspaceLifecycleMcpIntegrationTests.GIVEN_OpenedWorkspace_WHEN_InvokingMutationAndTransactionToolsThroughMcp_THEN_ShouldCompleteTransactionWorkflows` |
| 2 | 2.960 s | `RepresentativeMcpToolIntegrationTests.GIVEN_ControlledCodeActionProvider_WHEN_ListingAndStagingThroughMcp_THEN_ShouldStageRepresentativeCodeAction` |
| 3 | 1.296 s | `RepresentativeMcpToolIntegrationTests.GIVEN_InspectionWorkspace_WHEN_InvokingRepresentativeQueryThroughMcp_THEN_ShouldReturnStructuredResult` |
| 4 | 0.790 s | `WorkspaceLifecycleMcpIntegrationTests.GIVEN_WorkspaceLifecycleTools_WHEN_OpeningListingReadingReloadingAndClosing_THEN_ShouldReturnStructuredResults` |
| 5 | 0.661 s | `HostCompositionIntegrationTests.GIVEN_ComposedCodeActions_WHEN_RequestingFullServerStatus_THEN_ShouldNotPublishCodeActionsAsPlugin` |
| 6 | 0.361 s | `PluginDiscoveryAndMcpToolIntegrationTests.GIVEN_PackagedMutationPlugin_WHEN_InvokingThroughMcp_THEN_ShouldExecuteAndStageProposal` |
| 7 | 0.263 s | `ServerStatusRecoveryIntegrationTests.GIVEN_UnfinishedRecoveryRecord_WHEN_RequestingFullServerStatus_THEN_ShouldReturnPersistedRecoveryDiagnostics` |
| 8 | 0.235 s | `HostCompositionIntegrationTests.GIVEN_ConfiguredBuilder_WHEN_ComposingHost_THEN_ShouldRegisterHostServicesAndAllMcpTools` |
| 9 | 0.234 s | `PluginDiscoveryAndMcpToolIntegrationTests.GIVEN_LoadedRegisteredTool_WHEN_PublishingAndInvokingThroughMcp_THEN_ShouldExposeProtocolMetadataSchemaAndStructuredContent` |
| 10 | 0.100 s | `McpSdkSchemaProviderIntegrationTests.GIVEN_RequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishRequestProperties` |

### CodeActions Audit

| Rank | Median | Test case |
| --: | --: | --- |
| 1 | 3.548 s | `ReplayRefactoringToolsTests.GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(toolName: "convert-if-to-switch")` |
| 2 | 2.123 s | `InternalArgumentNullGuardAuditTests.GIVEN_InternalProductionTypes_WHEN_InspectingArgumentNullGuards_THEN_ShouldContainNoRedundantGuards` |
| 3 | 2.123 s | `ProductionNullForgivingOperatorAuditTests.GIVEN_ProductionSource_WHEN_InspectingNullableSuppressionSyntax_THEN_ShouldContainNoNullForgivingOperators` |
| 4 | 1.326 s | `BuiltInCodeActionCompatibilityTests.GIVEN_SupportedCompatibilityCase_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable` (`FullyQualify` provider case) |
| 5 | 1.168 s | `BuiltInCodeActionCompatibilityTests.GIVEN_SupportedCompatibilityCase_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable` (`RemoveUnusedVariable` provider case) |
| 6 | 1.164 s | `BuiltInCodeActionCompatibilityTests.GIVEN_SupportedCompatibilityCase_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable` (`SpellCheck` provider case) |
| 7 | 1.110 s | `ReplayRefactoringToolsTests.GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(toolName: "add-debugger-display")` |
| 8 | 1.097 s | `ReplayRefactoringToolsTests.GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingConvertPropertyToAutoWhenSafe_THEN_ShouldStageStructuredMutation` |
| 9 | 1.091 s | `ReplayRefactoringToolsTests.GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(toolName: "convert-to-record")` |
| 10 | 1.090 s | `ReplayRefactoringToolsTests.GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(toolName: "convert-for-to-foreach")` |

## Initial Observations

- The four normal integration projects remain close to the proposal's approximately 49-second sequential warm baseline; their measured median total is 48.23 seconds.
- Workspace integration remains the longest normal integration project at 21.02 seconds.
- The CodeActions audit remains the largest process by a wide margin, with a 57.23-second median and a 1,124.9-MiB median peak resident set size.
- The slowest Host cases are the direct in-process MCP harness workflows that the acceptance programme is intended to replace.
- The two source-governance scans appear among the audit's slowest cases and are scheduled to move out of compatibility-audit ownership in Stage 6.

These observations identify comparison points only. Retention, movement or deletion decisions remain governed by the staged implementation plan and its replacement-evidence gates.
