# Final repository review findings

## Repository-level assessment

The repository has a clear acyclic architecture and unusually strong transaction/recovery, snapshot, plugin-boundary and error-reporting designs. The normal solution build was clean and 1,925 unit/contract tests passed during the original review. No P0 or P1 issue was substantiated in that review. All seven original findings, RWMCP-001 through RWMCP-007, are now resolved against current source; the later deep-dive programme and its active findings are recorded in the [deep-dive review artefacts](deep-dives/).

## Validated findings

### RWMCP-001 — Dispose a loaded Workspace when advisory instance publication is cancelled

- Status: Resolved on 2026-07-31
- Severity: P2
- Confidence: High
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:97-103`
- Concrete failure scenario: A client cancels `workspace-open` after MSBuild/Roslyn loading succeeds while instance-status scanning is still using the request token. Cancellation escapes before the later cleanup block, leaving the loaded Workspace, compilations and watcher-related resources alive. Repeated timed-out opens can grow process memory/resources indefinitely.
- Supporting call path/evidence: `WorkspaceOpenTool` → `WorkspaceLifecycleService.OpenAsync` → successful `_workspaceLoadWorkflow.LoadAsync` → `_instanceStatusPublisher.OpenAsync(..., cancellationToken)` → cancellation in `WorkspaceInstanceStatusPublisher.ScanAsync`/JSON read. Workspace disposal begins only in the later manifest and registration failure paths.
- Affected projects/subsystems: Workspace lifecycle/loading/coordination and Host workspace tools.
- Remediation direction: Establish a cleanup guard immediately after successful load and transfer ownership only after session registration; include any partially created status handle in the same cleanup.
- Resolution: `WorkspaceLifecycleService.OpenAsync` now guards ownership immediately after load and cleans up the advisory identity, input manifest and loaded Workspace on cancellation or failure until session registration succeeds. Focused unit coverage verifies the cancellation window and cleanup behaviour.

### RWMCP-003 — Keep async analysis within the current executable function

- Status: Resolved on 2026-07-31
- Severity: P2
- Confidence: High
- Exact location: `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeAsyncTool.cs:43-44`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeAsyncTool.cs:67-70,117-127`
- Concrete failure scenario: In `async Task Outer() { Func<Task> f = async () => await SaveAsync(); }`, the nested lambda's await suppresses the correct `AsyncWithoutAwait` finding for `Outer`. In `await Task.Run(() => { SaveAsync(); });`, the discarded inner task is not awaited in its lambda, but the parent walk reaches the outer await and suppresses `UnawaitedTask`.
- Supporting call path/evidence: `analyze-async` scans every descendant await/invocation under a method body. `IsAwaited` walks every operation parent until any `IAwaitOperation` and never stops at anonymous/local-function or value-consuming boundaries. Tests cover direct awaited/discarded calls only.
- Affected projects/subsystems: Plugins.Core async analysis and its inspection contracts/tests.
- Remediation direction: Analyse executable functions independently, exclude nested bodies from containing methods and require the invocation value itself to reach the await operand.
- Resolution: Traversal now prunes anonymous and local-function syntax/operation bodies and reports only genuinely discarded task-like invocations. Focused tests cover nested-only awaits and stored-then-awaited values; the repository-wide revalidation passed the full focused async test class.

### RWMCP-006 — Do not treat an arbitrary syntactic Dispose call as guaranteed disposal

- Status: Resolved on 2026-07-31
- Severity: P2
- Confidence: High
- Exact location: `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDisposablesTool.cs:101-148`
- Concrete failure scenario: A disposable is released only under `if (success)` or only inside an uninvoked nested local function. The tool reports no `UndisposedLocal` finding, even though another exit leaks it, because one descendant call named `Dispose`/`DisposeAsync` is sufficient to mark the symbol disposed.
- Supporting call path/evidence: `analyze-disposables` → `IsDisposed` selects an enclosing body → `GetDisposedSymbols` scans all descendant invocations → receiver-symbol set membership suppresses the finding. There is no control-flow/reachability/all-exits analysis, and nested executable bodies are not excluded.
- Affected projects/subsystems: Plugins.Core disposable analysis and resource-management inspection tests.
- Remediation direction: Use operation/control-flow analysis over the local's actual executable scope and require disposal on all relevant exits, or narrow the public semantics so they do not claim lifetime correctness.
- Resolution: Disposal detection now accepts only an unconditional same-scope disposal or supported `finally` region and uses Roslyn control-flow exit analysis. Conditional and nested-only calls remain findings; the repository-wide revalidation passed the full focused disposable test class.

### RWMCP-002 — Count logical lines independently of the Host operating system

- Status: Resolved on 2026-07-31
- Severity: P2
- Confidence: High
- Exact location: `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetCodeMetricsTool.cs:211-214`
- Concrete failure scenario: A Windows Host inspects an LF-only source file. Splitting on `Environment.NewLine` (CRLF) finds no separator and counts a multi-line declaration as one logical line, corrupting both `logicalLines` and the derived maintainability index. Equivalent inconsistencies occur for other mismatched/mixed conventions.
- Supporting call path/evidence: `get-code-metrics` → `CreateMetricInfo` → `CountLogicalLines` → `syntaxNode.ToString().Split(Environment.NewLine, ...)`. Tests use platform-native source literals and do not force source/Host newline mismatch.
- Affected projects/subsystems: Plugins.Core metrics and cross-platform compatibility.
- Remediation direction: Count Roslyn `SourceText` line spans or recognise CRLF, LF and CR independently; add cross-line-ending cases.
- Resolution: Logical-line counting now normalises all newline forms before splitting, and a focused test uses a source convention different from the Host. The repository-wide revalidation passed the full focused metrics test class.

### RWMCP-004 — Register Host lifetime services in the shared integration composition

- Status: Resolved on 2026-07-31
- Severity: P2
- Confidence: High
- Exact location: `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/ComponentWorkspace.cs:52-99`
- Concrete failure scenario: Integration tests using `ComponentWorkspace` fail before their scenarios because cache state constructors require `IHostApplicationLifetime`, but the fixture starts from a raw `ServiceCollection` and never registers it. Current execution produced 32 failures in the 73-test Workspace integration project; affected Plugins.Core/CodeActions component projects cannot initialise meaningfully.
- Supporting call path/evidence: integration test → `ComponentWorkspace.Create` → `AddWorkspaceServices` → `BuildServiceProvider(ValidateOnBuild = true)` → unresolved lifetime while activating both query-cache states. The production Generic Host supplies the dependency, so this is fixture drift.
- Affected projects/subsystems: IntegrationTestSupport and Workspace, Plugins.Core, CodeActions and two Host containment integration tests.
- Remediation direction: Build through a minimal Generic Host or register a deterministic fixture-owned `IHostApplicationLifetime`, then rerun every consuming project.
- Resolution: `ComponentWorkspace` now uses a minimal Generic Host with strict DI validation, and disposal stops and disposes that Host. The complete Workspace integration suite passes 74/74, Code Actions passes 17/17 and focused coverage proves the framework lifetime's stopping/stopped tokens. Plugins.Core and Host integration composition now succeeds; the Host catalogue expectation was corrected separately under RWMCP-005.

### RWMCP-005 — Keep plugin discovery expectations aligned with the complete fixture catalogue

- Status: Resolved on 2026-07-31
- Severity: P2
- Confidence: High
- Exact location: `test/Roslyn.Workbench.Mcp.IntegrationTest/PluginPackageDiscoveryIntegrationTests.cs:19-27`; fixture registration at `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostQueryPluginFixture/HostValidQueryPlugin.cs:14-19`
- Concrete failure scenario: Plugin discovery correctly returns three tools because the query fixture now includes `host-query-cache-calibration`, but the integration test still requires two and checks only the earlier query/mutation names. This independently keeps the Host integration project red.
- Supporting call path/evidence: fixture package copy → `PluginCatalogBootstrap.Load` materialises all registrations → focused test execution returns exactly `host-valid-query`, `host-query-cache-calibration` and `host-valid-mutation` → stale count assertion fails. The Host integration project otherwise passed 59 tests; two other failures were RWMCP-004.
- Affected projects/subsystems: Host plugin discovery integration coverage and external query fixture catalogue.
- Remediation direction: Assert the exact complete three-name set and update catalogue expectations alongside fixture registration changes.
- Resolution: The test now asserts the exact set `host-valid-query`, `host-query-cache-calibration` and `host-valid-mutation`. Focused validation passes and the complete Host integration project passes 62/62; production discovery code was unchanged.

### RWMCP-007 — Validate state-directory writability before admitting transactions

- Status: Resolved on 2026-07-31
- Severity: P3
- Confidence: High
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Recovery/WorkspaceStateDirectory.cs:25-29`; `src/Roslyn.Workbench.Mcp.Workspace/Recovery/WorkspaceStateDirectorySecurity.cs:20-44`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionService.cs:42-123`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:309-318`
- Concrete failure scenario: A configured state/recovery directory already exists and can be inspected but does not permit file creation. Startup and `transaction-start` succeed, so the user can stage and preview a transaction, but commit later fails while attempting to persist its recovery plan.
- Supporting call path/evidence: Startup `WorkspaceStateDirectory.Initialize` creates/validates directory shape without a file-creation probe. `TransactionService.StartAsync` has no recovery-storage dependency. The first required write is `StageCommitAsync` → `CommitRecoveryStore.PersistPlanAsync`. An I/O/access failure is caught before application and returned as `CommitPreparationFailed`/`Retry`, leaving source targets unchanged.
- Affected projects/subsystems: Host startup prerequisites, transaction admission, recovery storage and operational diagnostics.
- Remediation direction: Probe create/write/flush/delete capability during startup or before admitting the first transaction, report an actionable state-directory error and retain commit-time validation because permissions may subsequently change.
- Resolution: Startup now probes exclusive owner-only file creation, write-through persistence, disk flush, file-security validation and deletion inside the recovery directory. Expected filesystem failures abort startup with an actionable `--state-directory` error while best-effort cleanup preserves the original failure; commit-time handling remains unchanged for later permission or availability changes. Focused unit tests cover successful and failed probe paths, and the physical Workspace integration suite passes 74/74.

## Original-review remediation validation

- Thirty focused current-source Plugins.Core tests covering metrics, async analysis and disposable analysis passed during the repository-wide revalidation.
- The original seven-finding report has no remaining active finding; current active repository findings are maintained in the [deep-dive final report](deep-dives/final-findings.md).

## Validation and review limitations

- `dotnet restore` and `dotnet build` succeeded with .NET SDK 10.0.102 and zero warnings/errors. Six fast-loop projects passed 1,925 unit/contract tests.
- Current post-remediation component evidence is: Workspace integration 74/74 passed, Code Actions integration 17/17 passed and Host integration 62/62 passed. Plugins.Core integration passes 6/7; its remaining mutation scenario assumes successful data after `format-document` returns no change and requires separate classification.
- Acceptance, Code Action audit and external-repository scenario suites were not run because repository policy requires explicit acceptance authorisation and keeps audit/scenario work outside the default loop.
- Roslyn MCP tooling requested by repository guidance was not available in this session; solution/project inspection used local project files, compiler builds and source-aware Roslyn usage in the code/tests.
- Provider-specific built-in Code Action behaviour beyond controlled/audit catalogues was not independently exercised because the audit suite was not run.
