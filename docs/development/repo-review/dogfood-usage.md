# RWMCP3 dogfood usage

This log records every request sent to the published dogfood server while remediating RWMCP3 findings. Failed requests are retained because they are part of the actual usage and expose contract usability.

## RWMCP3-006 and RWMCP3-015

### 1. `workspace-open`

**Purpose:** Open the current solution for investigation of the grouped lifecycle findings.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"rwmcp3","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-dogfood"}}`

**Outcome:** Succeeded. Opened Workspace `b2fab06a-cdef-4ab2-a1e5-f3e15e51c737` at epoch 1 and snapshot `890de464-310c-41f2-9315-b4ffdab62899`, with 30 projects and 1,571 documents. The server warned about one unresolved generated analyser reference and WSL access through the Windows filesystem.

### 2. `get-symbol-members`

**Purpose:** Inspect `CommitRecoveryStore` by documentation-comment identifier while holding the opened snapshot.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore"},"includeInherited":false,"membersLimit":100,"expectedSnapshot":{"workspaceId":"b2fab06a-cdef-4ab2-a1e5-f3e15e51c737","snapshotId":"890de464-310c-41f2-9315-b4ffdab62899"}}`

**Outcome:** Failed with `InvalidRequest` because `workspaceEpoch` and `transactionRevision` were required members of `SnapshotPrecondition`.

### 3. `get-symbol-members`

**Purpose:** Retry the `CommitRecoveryStore` lookup with the complete snapshot precondition.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore"},"includeInherited":false,"membersLimit":100,"expectedSnapshot":{"workspaceId":"b2fab06a-cdef-4ab2-a1e5-f3e15e51c737","workspaceEpoch":1,"snapshotId":"890de464-310c-41f2-9315-b4ffdab62899","transactionRevision":null}}`

**Outcome:** Failed with `SymbolAmbiguous` because the documentation-comment identifier matched multiple projects.

### 4. `search-symbols`

**Purpose:** Find the concrete production `CommitRecoveryStore` and obtain its document and project identity.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"CommitRecoveryStore","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with three results. The production type was identified in `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs` with document ID `f8294a8d-e4b4-4f48-a0f3-79526e416ff0` and project ID `eda2f897-0a51-481d-8f14-ff55ae095044`.

### 5. `get-symbol-members`

**Purpose:** Inspect the production type using its source location.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"location":{"document":{"documentId":"f8294a8d-e4b4-4f48-a0f3-79526e416ff0","path":"src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs","projectId":"eda2f897-0a51-481d-8f14-ff55ae095044"},"span":{"start":163,"length":19}}},"includeInherited":false,"membersLimit":100,"expectedSnapshot":{"workspaceId":"b2fab06a-cdef-4ab2-a1e5-f3e15e51c737","workspaceEpoch":1,"snapshotId":"890de464-310c-41f2-9315-b4ffdab62899","transactionRevision":null}}`

**Outcome:** Failed with `SymbolNotFound`; the location selector shape was incorrect.

### 6. `get-symbol-members`

**Purpose:** Retry the production type inspection with the documented nested span and document selector shape.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"location":{"span":{"document":{"documentId":"f8294a8d-e4b4-4f48-a0f3-79526e416ff0"},"start":163,"length":19}}},"includeInherited":false,"membersLimit":100,"expectedSnapshot":{"workspaceId":"b2fab06a-cdef-4ab2-a1e5-f3e15e51c737","workspaceEpoch":1,"snapshotId":"890de464-310c-41f2-9315-b4ffdab62899","transactionRevision":null}}`

**Outcome:** Succeeded and returned the `CommitRecoveryStore` member inventory, including the existing `GetSafeAbsolutePath` and `CreateInvalidManifest` paths relevant to RWMCP3-006.

### 7. `search-symbols`

**Purpose:** Locate the production lifecycle service and its direct test types.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"WorkspaceLifecycleService","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with six results, including the production interface and implementation and the Workspace lifecycle test class.

### 8. `search-symbols`

**Purpose:** Locate the server-owned tool execution base and its tests.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"ServerOwnedToolBase","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production generic base and its unit test class.

### 9. `find-references`

**Purpose:** Determine which execution paths preserve an immutable Workspace context by finding uses of `WorkspaceAttributedToolException`.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.ToolExecution.WorkspaceAttributedToolException"},"referencesLimit":100}`

**Outcome:** Failed with `SymbolAmbiguous` because the documentation-comment identifier matched multiple projects.

### 10. `search-symbols`

**Purpose:** Resolve the production `WorkspaceAttributedToolException` to an unambiguous source location.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"WorkspaceAttributedToolException","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production type in `src/Roslyn.Workbench.Mcp/ToolExecution/WorkspaceAttributedToolException.cs`.

### 11. `find-references`

**Purpose:** Identify every construction and consumption path for immutable Workspace-attributed failures.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"location":{"span":{"document":{"documentId":"c8b56225-eff1-4621-b722-4304b5190775"},"start":414,"length":32}}},"referencesLimit":100}`

**Outcome:** Succeeded with 25 references. Construction exists only in plugin and Code Action execution paths; server-owned lifecycle tools do not attach authoritative Workspace context when their operations throw.

### 12. `search-symbols`

**Purpose:** Resolve the concrete `WorkspaceLifecycleService.CloseAsync` method and distinguish it from interface, coordination, test-support and ScenarioRunner close methods.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"CloseAsync","kinds":["Method"],"symbolsLimit":50}`

**Outcome:** Succeeded with six results. The concrete lifecycle method was identified in `WorkspaceLifecycleService.cs` at line 226.

### 13. `get-code-context`

**Purpose:** Inspect the resolved close transition with its enclosing semantic context and diagnostics before implementing failure attribution.

**Request:** `{"workspace":{"alias":"rwmcp3"},"location":{"span":{"document":{"documentId":"90696cc3-35b9-4217-ae7e-8b3aae9142cb"},"start":9149,"length":10}},"beforeLines":8,"afterLines":55,"includeEnclosingSymbols":true,"includeDiagnostics":true,"expectedSnapshot":{"workspaceId":"b2fab06a-cdef-4ab2-a1e5-f3e15e51c737","workspaceEpoch":1,"snapshotId":"890de464-310c-41f2-9315-b4ffdab62899","transactionRevision":null}}`

**Outcome:** Succeeded. It confirmed that the session was removed before `CleanupAsync` and that the immutable operation context was not created until after cleanup completed; no diagnostics were reported for the inspected code.

### 14. `workspace-list`

**Purpose:** Check whether the RWMCP3 dogfood workspace remained available before reviewing the implementation.

**Request:** `{}`

**Outcome:** Succeeded. The `rwmcp3` workspace remained open at epoch 1.

### 15. `workspace-list`

**Purpose:** Repeat the workspace inventory after the first client projection returned no visible text content.

**Request:** `{}`

**Outcome:** Succeeded with the same single `rwmcp3` workspace. The structured response confirmed that the first call had also succeeded.

### 16. `workspace-reload`

**Purpose:** Refresh the dogfood workspace before inspecting the implementation changed on disk.

**Request:** `{"workspace":{"alias":"rwmcp3"}}`

**Outcome:** Failed with `WorkspaceReloadNotRequired`; change detection had not yet certified the workspace as stale.

### 17. `search-symbols`

**Purpose:** Find all MCP tool base types and compare their exception-attribution patterns.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"ToolBase","kinds":["NamedType"],"symbolsLimit":100}`

**Outcome:** Failed with `WorkspaceOutOfDate` and instructed the caller to invoke `workspace-reload`.

### 18. `workspace-reload`

**Purpose:** Follow the required continuation after the stale query established that a reload was necessary.

**Request:** `{"workspace":{"alias":"rwmcp3"}}`

**Outcome:** Succeeded. Reloaded the workspace at epoch 2 with 30 projects and 1,575 documents.

### 19. `search-symbols`

**Purpose:** Retry discovery of MCP tool base types after reloading.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"ToolBase","kinds":["NamedType"],"symbolsLimit":100}`

**Outcome:** Succeeded with `McpServerToolBase<TRequest>`, `ServerOwnedToolBase<TRequest, TResponse>` and their test types.

### 20. `search-symbols`

**Purpose:** Locate the top-level exception filter and its tests to trace final failure capture.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"ExceptionFilter","kinds":["NamedType"],"symbolsLimit":100}`

**Outcome:** Succeeded with `UnhandledToolExceptionFilter` and its unit and protocol integration tests.

### 21. `search-symbols`

**Purpose:** Locate the new Workspace exception used to transport failure context.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"WorkspaceOperationException","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Failed with `WorkspaceBusy` because multiple dogfood queries were issued concurrently.

### 22. `search-symbols`

**Purpose:** Locate the Host error-reporting Workspace context and its tests.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"CapturedWorkspaceContext","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production capture record and its unit tests.

### 23. `search-symbols`

**Purpose:** Retry discovery of the Workspace failure-transport exception after the concurrent request completed.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"WorkspaceOperationException","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production exception and its Workspace unit tests.

## RWMCP3-007

### 1. `workspace-list`

**Purpose:** Validate the restarted dogfood process and inspect its loaded Workspace state before beginning the next finding.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces. The client exposed no visible text content for the structured response.

### 2. `workspace-list`

**Purpose:** Repeat the Workspace inventory while explicitly inspecting the structured response after the first projection was blank.

**Request:** `{}`

**Outcome:** Succeeded and confirmed that the restarted process had no loaded Workspaces and no transaction owner.

### 3. `workspace-open`

**Purpose:** Open the current solution for investigation of the atomic-write containment finding.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"rwmcp3","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-dogfood"}}`

**Outcome:** Succeeded. Opened Workspace `7c39b922-8a03-449a-9093-3b27e5b085eb` at epoch 1 and snapshot `690489f3-8770-4b00-889c-005921d110b2`, with 30 projects and 1,577 documents. The server warned about one unresolved generated analyser reference and WSL access through the Windows filesystem.

### 4. `search-symbols`

**Purpose:** Resolve the production atomic writer and its interface and tests before tracing consumers.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"AtomicFileWriter","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production implementation and interface, the unit test class and the integration test class.

### 5. `find-references`

**Purpose:** Identify the production registration and direct test construction sites for `AtomicFileWriter`.

**Request:** `{"workspace":{"alias":"rwmcp3"},"symbol":{"location":{"span":{"document":{"documentId":"08694a16-f290-4d0e-9f58-6cb1d12ba9ca"},"start":132,"length":16}}},"includeDefinitions":true,"includeContext":true,"referencesLimit":100}`

**Outcome:** Succeeded with 17 references. Production construction is container-owned, while the remaining direct constructions are confined to test projects.

### 6. `search-symbols`

**Purpose:** Locate recovery-related production types and methods during the independent review of the startup-recovery guidance.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"Recovery","kinds":["NamedType","Method"],"symbolsLimit":50}`

**Outcome:** Failed with `WorkspaceNotOpen` because the review process had no open Workspace. The response instructed the caller to invoke `workspace-open`; the reviewer made no follow-up request.

## RWMCP3-008, RWMCP3-009 and RWMCP3-011

### 1. `workspace-list`

**Purpose:** Inspect the current dogfood process before tracing the grouped Core tool findings.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 2. `workspace-open`

**Purpose:** Open the current solution for symbol-backed investigation of the three affected bundled tools.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"rwmcp3","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-dogfood"}}`

**Outcome:** Succeeded. Opened Workspace `a30c5045-b82a-496e-810f-ece7d973d434` at epoch 1 and snapshot `79d27c64-4ac1-4091-9dbd-7a2b45cb56c7`, with 30 projects and 1,577 documents. The server warned about one unresolved generated analyser reference and WSL access through the Windows filesystem.

### 3. `search-symbols`

**Purpose:** Locate the control-flow graph tool and its unit tests before tracing location handling.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"GetControlFlowGraphTool","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production tool and its unit test type.

### 4. `search-symbols`

**Purpose:** Locate the code-context tool and its unit tests before tracing context-window arithmetic.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"GetCodeContextTool","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production tool and its unit test type.

### 5. `search-symbols`

**Purpose:** Locate the formatting tool and its unit tests before tracing the competing document selectors.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"FormatDocumentTool","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production tool and its unit test type.

### 6. `workspace-list`

**Purpose:** Recheck the dogfood Workspace state while validating the selector composition refinement.

**Request:** `{}`

**Outcome:** The request completed successfully, but the client exposed no visible response content.

### 7. `get-symbol-info`

**Purpose:** Inspect the existing `TextSpanRange` contract through the dogfood symbol model after choosing composition over inheritance.

**Request:** `{"workspace":{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Selectors.TextSpanRange"},"includeDocumentation":true}`

**Outcome:** The request completed without an MCP error, but the client exposed no visible response content.

## RWMCP3-012 and RWMCP3-013

### 1. `workspace-list`

**Purpose:** Inspect the restarted dogfood process before investigating Code Action discovery and replay.

**Request:** `{}`

**Outcome:** Succeeded, but the client projection exposed no visible text content.

### 2. `server-status`

**Purpose:** Confirm that the restarted dogfood Host had composed its Code Action catalogue before reviewing provider discovery behaviour.

**Request:** `{"detail":"Minimal"}`

**Outcome:** Succeeded. The Host reported Roslyn 5.6.0.0, 56 tools and an available Code Action subsystem composed from 81 refactoring providers and 169 code-fix providers.

### 3. `workspace-list`

**Purpose:** Recheck the Workspace inventory through the structured response before opening the current solution.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 4. `workspace-open`

**Purpose:** Open the current solution for symbol-backed investigation of Code Action discovery and replay.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"rwmcp3","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-dogfood"}}`

**Outcome:** Succeeded. Opened Workspace `3d5a02bd-754a-44ae-8741-86c131580145` at epoch 1 and snapshot `78b70c82-5b8a-414e-add1-825adead1f48`, with 30 projects and 1,579 documents. The server warned about one unresolved generated analyser reference and WSL access through the Windows filesystem.

### 5. `search-symbols`

**Purpose:** Locate Code Action discovery contracts, implementation and tests before tracing provider fault boundaries.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"CodeActionDiscoveryService","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the discovery implementation and interface plus its unit and integration test types.

### 6. `search-symbols`

**Purpose:** Locate Code Action replay implementations and tests before tracing recipe path resolution.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"CodeActionResolver","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with `CodeActionResolver`, its interface and tests, plus related resolver types.

## RWMCP3-014

### 1. `workspace-list`

**Purpose:** Inspect the restarted dogfood process before investigating strict request binding.

**Request:** `{}`

**Outcome:** The request completed without an MCP error, but the client exposed no visible response content.

### 2. `server-status`

**Purpose:** Verify that the newly published dogfood Host started before opening the current solution.

**Request:** `{"detail":"Minimal"}`

**Outcome:** The request completed without an MCP error, but the client exposed no visible response content.

### 3. `workspace-open`

**Purpose:** Open the current solution for symbol-backed inspection of request binding and Workspace selection.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"rwmcp3","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-dogfood"}}`

**Outcome:** The request completed without an MCP error, but the client exposed no visible response content.

### 4. `search-symbols`

**Purpose:** Locate `ToolRequestBinder` and its test surface before inspecting unmapped-member behaviour.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"ToolRequestBinder","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** The request completed without an MCP error, but the client exposed no visible response content.

### 5. `search-symbols`

**Purpose:** Locate `WorkspaceSelectorService` before tracing how an ignored selector property reaches implicit single-Workspace selection.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"WorkspaceSelectorService","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** The request completed without an MCP error, but the client exposed no visible response content.
