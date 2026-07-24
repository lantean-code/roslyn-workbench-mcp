# Integration Testing Stage 3 Results

Date: 17 July 2026

## Outcome

Stage 3 is complete. The published Host acceptance suite now exercises representative Workspace query, transactional plugin mutation, Code Action mutation, startup/recovery and external plugin package workflows over real stdio. The suite contains 10 tests: the five Stage 2 process-boundary tests and five Stage 3 workflow tests.

## Public response envelope

The first Stage 3 workflows exposed an avoidable client inconsistency in the deliberately family-specific published responses. Direct lifecycle/status results were flattened, query results used `data`, and mutation results used a separate top-level staged receipt.

The approved greenfield contract now gives every successful tool the same outer shape:

```json
{
  "ok": true,
  "data": {}
}
```

Failures continue to publish `ok: false`, `error` and optional `next`. Lifecycle, query and mutation payloads remain compact and family-specific inside `data`; the richer internal `ToolResult<TData>` contract is not exposed. Runtime serializers, full output schemas, Host/component tests and the authoritative contract/design documents were updated together.

## Representative workflows

### Workspace lifecycle and query

The acceptance test opens a copied SDK project, verifies list and full status results, invokes bundled `search-symbols`, validates the semantic `Sample.Class1` named-type result, closes the Workspace and observes `WorkspaceNotOpen` through the public status path.

### Transactional plugin mutation

The acceptance test opens an isolated SDK project, starts a transaction, stages bundled `rename-symbol`, previews the changed document, commits and verifies the exact CRLF source bytes. A follow-up `search-symbols` call proves that the promoted in-memory Workspace resolves `Sample.RenamedClass` without a reload.

### Code Action mutation

The acceptance test uses the checked-in inspection sample to discover Roslyn's built-in `Convert to raw string` refactoring, stages it by its published action identity, previews the affected document and rolls the transaction back. Exact original bytes remain on disk, Plugins.Core remains present in plugin status, and Code Actions remains outside the plugin catalogue.

### Startup diagnostics and recovery

The acceptance test starts the published Host with an invalid `--default-max-results` value. Full server status reports the default value of 100 and a `StartupConfigurationFallback` warning. It then writes a valid version 2 `RecoveryConflict` manifest beneath the isolated state root, restarts the real Host process and verifies the blocked recovery status through MCP. The unchanged conflict manifest on disk proves the required durable blocked state.

### External plugin package

The acceptance build uses a build-only `ReferenceOutputAssembly=false` reference to assemble the existing Host query plugin fixture. The acceptance assembly receives no production or fixture compile reference. Only the plugin entry DLL, dependency manifest and deliberately private `NuGet.Versioning` assembly are copied as package assets.

At runtime the fixture copies those assets beneath one immediate package directory in an isolated plugin root. The published Host discovers `host.valid.query`, publishes `host-valid-query` through `tools/list`, opens a Workspace and invokes the external query over stdio. The returned value and private dependency version prove real package loading and invocation.

Windows briefly retained the loaded private dependency after process completion during the first external-package run. Scenario disposal now polls only transient `IOException` and `UnauthorizedAccessException` cleanup failures for at most two seconds. Persistent cleanup failures still fail with the scenario path and inner exception.

## Replacement mapping

The following direct-MCP evidence is now superseded at the public process boundary. The existing tests remain in place until Stage 4 applies the replacement-evidence gates.

| Acceptance workflow | Direct/component MCP evidence superseded | Evidence intentionally retained below the process boundary |
| --- | --- | --- |
| Workspace lifecycle and query | `WorkspaceLifecycleMcpIntegrationTests.GIVEN_WorkspaceLifecycleTools_WHEN_OpeningListingReadingReloadingAndClosing_THEN_ShouldReturnStructuredResults`; query publication path in `RepresentativeMcpToolIntegrationTests.GIVEN_InspectionWorkspace_WHEN_InvokingRepresentativeQueryThroughMcp_THEN_ShouldReturnStructuredResult` | Workspace loading, resolver and lifecycle edge cases |
| Transactional plugin mutation | Public transaction/mutation path in `WorkspaceLifecycleMcpIntegrationTests.GIVEN_OpenedWorkspace_WHEN_InvokingMutationAndTransactionToolsThroughMcp_THEN_ShouldCompleteTransactionWorkflows` | Transaction history, conflicts, staging capacity, durable commit and recovery mechanics |
| Code Action mutation | Public list/stage path in `RepresentativeMcpToolIntegrationTests.GIVEN_ControlledCodeActionProvider_WHEN_ListingAndStagingThroughMcp_THEN_ShouldStageRepresentativeCodeAction` | Controlled-provider parameter, token, fix-all and failure workflows |
| Startup diagnostics and recovery | Public status projection in `ServerStatusRecoveryIntegrationTests.GIVEN_UnfinishedRecoveryRecord_WHEN_RequestingFullServerStatus_THEN_ShouldReturnPersistedRecoveryDiagnostics` | Recovery-store validation, restoration, divergence and commit-writer behaviour |
| External plugin package | Public publication/invocation path in `PluginDiscoveryAndMcpToolIntegrationTests.GIVEN_LoadedRegisteredTool_WHEN_PublishingAndInvokingThroughMcp_THEN_ShouldExposeProtocolMetadataSchemaAndStructuredContent` | Package validation, collisions, disabled diagnostics and private-dependency routing detail |

## Verification

- Host unit and contract coverage affected by response shaping: 178 passed;
- repository fast loop (`Category!=Integration&Category!=Audit`): 1,754 passed;
- Host integration: 23 passed;
- Workspace integration: 65 passed;
- Linux Debug published-Host acceptance: 10 passed;
- Windows Debug published-Host acceptance through native Windows .NET 10.0.302: 10 passed;
- complete repository suite, including acceptance and audit coverage: 1,979 passed; and
- no acceptance scenario roots or Host child processes remained after final verification.
