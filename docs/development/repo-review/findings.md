# Durable finding ledger

This ledger contains candidates discovered during the staged review. Entries may be amended or removed during final independent validation. Stable identifiers are never reused.

## RWMCP-001 — Dispose a loaded Workspace when advisory instance publication is cancelled

- Severity: P2
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:97-103`
- Failure scenario: A client cancels `workspace-open` after `IWorkspaceLoadWorkflow.LoadAsync` has successfully created an `MSBuildWorkspace`, while `IWorkspaceInstanceStatusPublisher.OpenAsync` is scanning status files and observes the cancellation token. `OperationCanceledException` escapes before the later manifest `try` block, leaving the loaded Workspace and its MSBuild/Roslyn resources undisposed. Repeated cancelled opens can retain substantial memory and file-watcher resources for the process lifetime.
- Evidence/call path: `WorkspaceOpenTool` → `WorkspaceLifecycleService.OpenAsync` → successful `_workspaceLoadWorkflow.LoadAsync` → `_instanceStatusPublisher.OpenAsync(..., cancellationToken)` → `WorkspaceInstanceStatusPublisher.ScanAsync` cancellation check or async JSON read → uncaught cancellation. Disposal begins only in the `BuildManifest` catch at lines 105-118 and subsequent failure paths.
- Affected subsystems: Workspace loading/lifecycle, coordination, Host `workspace-open` tool.
- Remediation direction: Establish ownership immediately after a successful load and wrap every subsequent async/setup step in a `try`/`finally` (or equivalent ownership transfer) that disposes the Workspace unless the session is successfully registered; close any status handle created before failure as part of the same cleanup.
- Resolution: `WorkspaceLifecycleService.OpenAsync` now establishes a cleanup guard immediately after a successful load and transfers ownership only after session registration. Failure or cancellation closes the allocated advisory status identity, disposes any input manifest and disposes the loaded Workspace. Focused unit coverage injects cancellation during instance-status publication and verifies cancellation propagation, status cleanup, Workspace disposal and absence of session registration.

## RWMCP-002 — Count logical lines independently of the Host operating system

- Severity: P2
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetCodeMetricsTool.cs:211-214`
- Failure scenario: Roslyn Workbench runs on Windows and inspects a source document that uses LF-only line endings (a common repository configuration). `syntaxNode.ToString().Split(Environment.NewLine, ...)` searches for CRLF, finds no separators and treats an entire multi-line declaration as one logical line. The published `logicalLines` and derived maintainability index are consequently wrong. The reciprocal mixed-host/source case can also produce inconsistent counting.
- Evidence/call path: MCP `get-code-metrics` → `GetCodeMetricsTool.ExecuteCoreAsync` → `CreateMetricInfo` → `CountLogicalLines`. The method uses the Host's newline sequence rather than Roslyn `SourceText` lines or a line-ending-independent split. Existing tests construct source using the test platform's own source/raw-string line endings and do not exercise a mismatched source/Host newline convention.
- Affected subsystems: Bundled core tools, metrics contracts, cross-platform compatibility.
- Remediation direction: Count line spans through Roslyn text APIs or split on all recognised newline forms (`\r\n`, `\n`, and `\r`) independently of `Environment.NewLine`; add cross-line-ending tests.
- Resolution: `CountLogicalLines` now normalises all newline forms to LF before splitting, and focused coverage constructs source with a line-ending convention different from the Host. Repository-wide revalidation passed the complete focused metrics test class.

## RWMCP-003 — Keep async analysis within the current executable function

- Severity: P2
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeAsyncTool.cs:43-44` and `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeAsyncTool.cs:67-70,117-127`
- Failure scenario: Nested functions are treated as part of the containing method. For `async Task Outer() { Func<Task> f = async () => await SaveAsync(); }`, the lambda's await suppresses the correct `AsyncWithoutAwait` finding for `Outer`. Conversely, for `await Task.Run(() => { SaveAsync(); });`, the discarded `SaveAsync()` is not awaited by its containing lambda, but `IsAwaited` walks through the anonymous-function boundary and finds the outer await, suppressing the correct `UnawaitedTask` finding. Either error can hide a real async defect.
- Evidence/call path: MCP `analyze-async` → `AnalyzeAsyncTool.ExecuteCoreAsync`. The no-await test searches all descendant syntax, including nested functions. Invocation analysis likewise enumerates all descendant operations, and `IsAwaited(invocation)` follows every `Parent` until any `IAwaitOperation` without stopping at an anonymous/local-function boundary or checking whether the invocation contributes the awaited value. Existing tests cover only direct awaits and direct discarded calls.
- Affected subsystems: Bundled core async analysis, inspection result correctness, analyser test coverage.
- Remediation direction: Analyse each executable function independently, excluding nested executable bodies from the containing method; determine whether an invocation's value is the operand of an await without crossing function boundaries or value-consuming operations; add nested-lambda, local-function, conditional, return, assignment, and argument tests.
- Resolution: Async analysis now prunes anonymous and local-function syntax/operation bodies from the containing method and classifies only actually discarded task-like invocations. Focused coverage proves nested-only awaits do not suppress the containing finding and stored-then-awaited tasks are not reported. Repository-wide revalidation passed the complete focused async test class.

## RWMCP-004 — Register Host lifetime services in the shared integration composition

- Severity: P2
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/ComponentWorkspace.cs:52-99`
- Failure scenario: Every integration test that creates the shared `ComponentWorkspace` fails before exercising its scenario. `AddWorkspaceServices` registers `WorkspaceQueryCacheState` and `PluginQueryCacheState`, whose constructors require `IHostApplicationLifetime`, but the raw `ServiceCollection` used by this fixture does not register that service. On the current source, the workspace integration project reports 32 failures out of 73 tests at `BuildServiceProvider(ValidateOnBuild = true)`, removing integration coverage from lifecycle, transaction, resolver, and change-detection behaviour.
- Evidence/call path: Integration test → `ComponentWorkspace.Create` → `new ServiceCollection()` → `AddWorkspaceServices()` → `BuildServiceProvider` validation → unresolved `IHostApplicationLifetime` while activating `WorkspaceQueryCacheState`/`PluginQueryCacheState`. The production Host does receive this service from `Host.CreateApplicationBuilder`; the drift is specific to the shared integration composition. The six unit/contract projects pass because cache tests supply mocked lifetimes and most component tests mock the cache boundary.
- Affected subsystems: Integration test support and all Workspace-, Plugins.Core-, and CodeActions-oriented component integration projects.
- Remediation direction: Build the fixture through a minimal Generic Host or register a fixture-owned `IHostApplicationLifetime` with deterministic stopping/stopped tokens, then rerun every integration project that consumes `ComponentWorkspace`.
- Resolution: `ComponentWorkspace` now builds its registrations through a minimal Generic Host with the existing strict service-provider validation. The Host supplies the framework-owned `IHostApplicationLifetime`, and fixture disposal stops the Host before disposing it so stopping/stopped tokens have production-equivalent semantics. The complete Workspace integration suite passes 74/74 and Code Actions passes 17/17; focused coverage verifies both lifetime shutdown tokens. The shared composition also starts the Plugins.Core and Host integration suites successfully, exposing independent scenario failures after their previously blocking DI error was removed.

## RWMCP-005 — Keep plugin discovery expectations aligned with the complete fixture catalogue

- Severity: P2
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `test/Roslyn.Workbench.Mcp.IntegrationTest/PluginPackageDiscoveryIntegrationTests.cs:19-27` (fixture registration at `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostQueryPluginFixture/HostValidQueryPlugin.cs:14-19`)
- Failure scenario: The external-plugin discovery integration test fails on the current source because the query fixture now registers both `host-valid-query` and `host-query-cache-calibration`, but the assertion still requires exactly two total tools across the query and mutation plugins. Discovery correctly publishes three tools, so this stale assertion keeps the Host integration project red and prevents the catalogue test from serving as reliable regression coverage.
- Evidence/call path: `PluginPackageDiscoveryIntegrationTests` copies the complete query fixture package and mutation fixture package → `PluginCatalogBootstrap.Load` materialises every configured tool → three registrations are returned → line 22 asserts count 2 and line 23 omits `host-query-cache-calibration`. A focused execution of this test fails with exactly those three registrations; the rest of the Host integration project has 59 passing tests, two additional failures attributable to RWMCP-004, and this independent catalogue failure.
- Affected subsystems: Host plugin discovery integration coverage, external plugin fixture catalogue, CI reliability.
- Remediation direction: Assert the full three-tool set (prefer exact names over a count plus partial containment) and keep fixture catalogue changes paired with discovery/publication expectation updates.
- Resolution: The discovery test now asserts the exact complete tool-name set: `host-valid-query`, `host-query-cache-calibration` and `host-valid-mutation`. The focused scenario passes and the complete Host integration project now passes 62/62. No production discovery behaviour changed.

## RWMCP-006 — Do not treat an arbitrary syntactic Dispose call as guaranteed disposal

- Severity: P2
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDisposablesTool.cs:101-148`
- Failure scenario: A disposable local is released only on one conditional branch (`if (success) resource.Dispose();`) or only inside an uninvoked nested local function. `analyze-disposables` nevertheless reports no `UndisposedLocal` finding because it collects any descendant invocation named `Dispose`/`DisposeAsync` for the entire executable syntax node without evaluating reachability, scope, execution order, or whether every exit is covered. The local leaks on the uncovered path even though the tool explicitly says the value is disposed before it goes out of scope.
- Evidence/call path: MCP `analyze-disposables` → `IsDisposed` chooses an enclosing method body (even for locals inside nested local functions when a base method ancestor exists) → `GetDisposedSymbols` scans every descendant invocation and adds the receiver local → membership alone suppresses the finding. No control-flow analysis is used. Existing tests cover unconditional direct disposal and undisposed locals, but no conditional, early-return, exception, loop, or nested-function-only disposal paths.
- Affected subsystems: Bundled core disposable analysis, resource-management diagnostics, inspection test coverage.
- Remediation direction: Use Roslyn control-flow/operation analysis to require disposal on all relevant exits from the local's actual executable scope, and do not cross nested-function boundaries; if the intended contract remains purely syntactic, narrow the published finding semantics so it does not claim lifetime correctness.
- Resolution: Disposable analysis now accepts only a direct unconditional disposal in the local's statement scope or a supported enclosing `finally` region and uses Roslyn control-flow exit analysis across the declaration-to-disposal region. Conditional and nested-function-only calls no longer suppress findings. Repository-wide revalidation passed the complete focused disposable test class.

## RWMCP-007 — Validate state-directory writability before admitting transactions

- Severity: P3
- Confidence: High
- Validation status: Resolved on 2026-07-31
- Location: `src/Roslyn.Workbench.Mcp.Workspace/Recovery/WorkspaceStateDirectory.cs:25-29`, `src/Roslyn.Workbench.Mcp.Workspace/Recovery/WorkspaceStateDirectorySecurity.cs:20-44`, `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionService.cs:42-123` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:309-318`
- Failure scenario: A configured state/recovery directory already exists and can be inspected but does not permit the Host to create files. Startup succeeds because it creates or validates the directory shape without probing file creation, and `transaction-start` succeeds without consulting recovery storage. The user can stage and preview a complete transaction only for commit to fail later while persisting its recovery plan.
- Evidence/call path: Host startup → `WorkspaceStateDirectory.Initialize` → `EnsureDirectory` calls `CreateDirectory` and validates link/Unix-mode properties but performs no create/write/delete probe. `TransactionService.StartAsync` has no state-directory dependency. The first required recovery write is `TransactionCommitService.StageCommitAsync` → `CommitRecoveryStore.PersistPlanAsync`. `IOException`/`UnauthorizedAccessException` is caught before application and returned as `CommitPreparationFailed`/`Retry`, so source targets remain unchanged; the defect is late operational failure rather than data exposure or loss.
- Affected subsystems: Host startup prerequisites, transaction admission, recovery storage and operational diagnostics.
- Remediation direction: Probe create/write/flush/delete capability during startup or immediately before admitting the first transaction, fail with an actionable state-directory error, and retain the existing commit-time check because permissions can change after the probe.
- Resolution: Startup initialisation now creates a unique owner-only probe file inside the recovery directory, writes a byte with write-through enabled, flushes intermediate buffers to disk, validates the resulting file security and deletes it. I/O or access failures abort startup with an actionable error naming the configured `--state-directory`; partial probes receive best-effort cleanup without replacing the original cause. Commit-time recovery writes remain authoritative if permissions or availability change after startup. Focused unit coverage exercises success, invalid paths, creation denial, cleanup failure and deletion failure, while the complete 74-test Workspace integration suite validates physical startup initialisation.
