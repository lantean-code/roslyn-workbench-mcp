# Post-RWMCP3 dogfood usage

**Status:** Active until the user explicitly ends dogfood usage logging.

This log records every request sent to the configured published Roslyn Workbench dogfood server after the RWMCP3 usage analysis. It covers implementation of the [approved dogfood improvement worklist](dogfood-analysis.md), the [follow-on dogfood validation worklist](dogfood-follow-on-worklist.md) and all other repository operations. Failed requests, retries, blank client projections and abandoned approaches remain part of the evidence.

The completed historical calls that informed the worklist remain in the separate [RWMCP3 dogfood usage log](dogfood-usage.md).

## Recording format

Create a section for each work item or distinct repository activity and number its calls in execution order. Record each call as:

### 1. `tool-name`

**Purpose:** Why the dogfood request was made.

**Request:** The material request shape, with incidental machine-specific paths redacted.

**Outcome:** Whether it succeeded or failed, the useful result or error code, any continuation followed and whether the client exposed the response content.

## Usage

## DOGFOOD-001 — Structured-result text fallback

### 1. `workspace-list`

**Purpose:** Confirm the published dogfood Workspace state before inspecting the shared MCP result-construction path.

**Request:** `{}`

**Outcome:** Succeeded and reported the existing `rwmcp3` Workspace at epoch 1 with no transaction owner. The client-visible response again contained empty `content` alongside populated `structuredContent`, directly reproducing DOGFOOD-001.

### 2. `search-symbols`

**Purpose:** Locate `McpServerToolBase` and its tests through the published dogfood Workspace.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"McpServerToolBase","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Failed with `WorkspaceOutOfDate`. The continuation instructed the caller to invoke `workspace-reload` before retrying.

### 3. `workspace-reload`

**Purpose:** Follow the authoritative continuation from the failed symbol query.

**Request:** `{"workspace":{"alias":"rwmcp3"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 30 projects and 1,589 documents. The existing unresolved generated-analyser warning remained. The client-visible response again contained empty `content` alongside populated `structuredContent`.

### 4. `search-symbols`

**Purpose:** Retry discovery of the shared result-construction path after reloading the Workspace.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"McpServerToolBase","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production `McpServerToolBase<TRequest>` and `McpServerToolBaseTests` types. The client-visible response again contained empty `content` alongside populated `structuredContent`.

## DOGFOOD-002 — Agent-facing input schemas

### 5. `server-status`

**Purpose:** Verify the restarted DOGFOOD-001 build before beginning analysis of agent-facing input schema projection.

**Request:** `{"detail":"Standard"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 81 refactoring providers, 169 code-fix providers and 56 tools. The client projection exposed both the JSON text fallback and the equivalent `structuredContent`, confirming DOGFOOD-001 interoperability while also demonstrating duplicate model-visible content in this client.

### 6. `workspace-list`

**Purpose:** Determine whether the restarted dogfood process retained a Workspace before inspecting schema publication symbols.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces. Both equivalent result representations were client-visible.

### 7. `workspace-open`

**Purpose:** Open the current solution so the schema publication path could be inspected through the dogfood symbol model.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood-improvements","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-dogfood"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 30 projects and 1,590 documents. The expected WSL-on-Windows-filesystem warning and one unresolved generated-analyser warning were reported. Both equivalent result representations were client-visible.

### 8. `search-symbols`

**Purpose:** Locate the current input-schema provider, transformer, factory, tests and related contracts before designing DOGFOOD-002.

**Request:** `{"workspace":{"alias":"dogfood-improvements"},"query":"Schema","kinds":["NamedType"],"symbolsLimit":50}`

**Outcome:** Succeeded with 43 schema-related types, including `McpSdkSchemaProvider`, `InputContractSchemaTransformer`, `ToolSchemaFactory`, their integration tests and the published validation-schema acceptance tests. The broad result was much larger than needed and, because both equivalent representations were projected, materially demonstrated the context duplication risk recorded for DOGFOOD-001.

### 9. `workspace-list`

**Purpose:** Check the published dogfood Workspace state before the final independent DOGFOOD-002 review.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 10. `workspace-open`

**Purpose:** Open the current solution for the final independent review of the staged input-schema changes.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","alias":"dogfood-002-final-review","workspaceRoot":"<repo-root>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 11. `search-symbols`

**Purpose:** Locate the transformer and its tests during the final independent DOGFOOD-002 review.

**Request:** `{"workspace":{"alias":"dogfood-002-final-review"},"query":"InputContractSchemaTransformer","symbolsLimit":20}`

**Outcome:** Succeeded with the production transformer and its focused test type.

### 12. `find-references`

**Purpose:** Confirm the transformer production consumers and test surface during final independent review.

**Request:** `{"workspace":{"alias":"dogfood-002-final-review"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Protocol.InputContractSchemaTransformer"},"includeDefinitions":true,"includeContext":true,"referencesLimit":100}`

**Outcome:** Succeeded with 10 definition/reference items, confirming that `McpSdkSchemaProvider` is the sole production consumer alongside the focused tests.

### 13. `workspace-close`

**Purpose:** Close the temporary review Workspace after completing the dogfood queries.

**Request:** `{"workspace":{"alias":"dogfood-002-final-review"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 14. `workspace-list`

**Purpose:** Check the published dogfood Workspace state before reviewing the first P1 remediation.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 15. `workspace-open`

**Purpose:** Open the current solution for independent review of the ignored-member and polymorphic-schema remediation.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood002-final-review","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 16. `search-symbols`

**Purpose:** Locate the remediated transformer and focused tests during independent review.

**Request:** `{"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-final-review"}}`

**Outcome:** Succeeded with the production transformer and focused test type.

### 17. `find-references`

**Purpose:** Reconfirm the transformer consumer boundary after remediation.

**Request:** `{"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Protocol.InputContractSchemaTransformer"},"includeDefinitions":true,"includeContext":true,"referencesLimit":50,"workspace":{"alias":"dogfood002-final-review"}}`

**Outcome:** Succeeded with 10 items, again confirming `McpSdkSchemaProvider` as the sole production consumer.

### 18. `workspace-close`

**Purpose:** Close the temporary review Workspace after completing the remediation review queries.

**Request:** `{"workspace":{"alias":"dogfood002-final-review"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 19. `server-status`

**Purpose:** Confirm that Codex had started the promoted DOGFOOD-002 candidate before inspecting its actual client-projected tool declarations.

**Request:** `{"detail":"Minimal"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 81 refactoring providers, 169 code-fix providers and 56 published tools. Inspection of Codex's reloaded callable declarations then showed that snapshot and Workspace selector shapes had improved, but nested scope, symbol and location selector fragments still projected as `unknown`.

### 20. `workspace-list`

**Purpose:** Check the published dogfood Workspace state before assessing the revised DOGFOOD-002 implementation design.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 21. `workspace-open`

**Purpose:** Open the current solution to compare the revised DOGFOOD-002 design with the existing schema transformer, selector contracts and tests.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood002-design-review","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 22. `search-symbols`

**Purpose:** Locate the current transformer and focused tests while assessing the implementation design.

**Request:** `{"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-design-review"}}`

**Outcome:** Succeeded with the production transformer and focused test type.

### 23. `find-references`

**Purpose:** Confirm the transformer's current production consumer boundary before assessing the proposed input-only transformation.

**Request:** `{"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Protocol.InputContractSchemaTransformer"},"includeDefinitions":true,"includeContext":true,"referencesLimit":50,"workspace":{"alias":"dogfood002-design-review"}}`

**Outcome:** Succeeded with four definition/reference items, confirming `McpSdkSchemaProvider` as the sole production consumer and two focused test references.

### 24. `workspace-close`

**Purpose:** Close the temporary design-review Workspace after completing the Roslyn-backed inspection.

**Request:** `{"workspace":{"alias":"dogfood002-design-review"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 25. `workspace-list`

**Purpose:** Check the published dogfood Workspace state before beginning the focused DOGFOOD-002 implementation.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 26. `workspace-open`

**Purpose:** Open the current solution for Roslyn-backed inspection and later dogfood validation of the focused DOGFOOD-002 implementation.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood002-implementation","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 27. `search-symbols`

**Purpose:** Reconfirm the transformer and focused contract-test locations on the implementation baseline.

**Request:** `{"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded with the production transformer and focused test type.

### 28. `workspace-status`

**Purpose:** Check whether the open implementation Workspace had observed the source and test edits before dogfood validation.

**Request:** `{"detail":"Full","workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded and reported `WorkspaceOutOfDate` because the transformer source metadata had changed, with reload required.

### 29. `workspace-reload`

**Purpose:** Reload the implementation Workspace so Roslyn queries use the changed DOGFOOD-002 source and tests.

**Request:** `{"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded at Workspace epoch 3 with 30 projects and 1,634 documents.

### 30. `get-diagnostics`

**Purpose:** Check the reloaded solution for compiler or analyzer errors and warnings after implementing the complete-alternative transformer and focused tests.

**Request:** `{"diagnosticsLimit":200,"severities":["Warning","Error"],"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded with no compiler or analyzer diagnostics.

### 31. `get-document-outline`

**Purpose:** Confirm that the dogfood Workspace could resolve and inspect the changed transformer document after reload.

**Request:** `{"document":{"path":"src/Roslyn.Workbench.Mcp/Protocol/InputContractSchemaTransformer.cs"},"includeMembers":true,"maxDepth":2,"nodesLimit":200,"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded and resolved the changed `InputContractSchemaTransformer` type from the source document at Workspace epoch 3. The requested shallow outline was truncated before member details.

### 32. `workspace-status`

**Purpose:** Check the dogfood Workspace after final formatting, builds and tests before the final Roslyn validation pass.

**Request:** `{"detail":"Standard","workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded and reported `WorkspaceOutOfDate` after build-generated editorconfig metadata changed under the configured temporary artifacts path.

### 33. `workspace-reload`

**Purpose:** Reload the implementation Workspace against the final formatted source and test baseline.

**Request:** `{"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded at Workspace epoch 4 with 30 projects and 1,634 documents.

### 34. `get-diagnostics`

**Purpose:** Perform the final dogfood compiler and analyzer diagnostic check against the reloaded implementation baseline.

**Request:** `{"diagnosticsLimit":200,"severities":["Warning","Error"],"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded with no compiler or analyzer diagnostics.

### 35. `workspace-close`

**Purpose:** Close the temporary implementation Workspace after completing dogfood validation.

**Request:** `{"workspace":{"alias":"dogfood002-implementation"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 36. Published candidate startup smoke test

**Purpose:** Confirm that the pre-commit DOGFOOD-002 candidate could start and shut down cleanly before promoting it to the configured `current` target.

**Request:** Started `<dogfood-candidate>/Roslyn.Workbench.Mcp` over stdio with no MCP request and then closed the input stream.

**Outcome:** Succeeded. The Host entered Production, opened its stdio transport and shut down cleanly with exit code 0.

### 37. Fresh Codex projection attempt through the configured launcher

**Purpose:** Load the promoted pre-commit candidate in a fresh ephemeral Codex client and inspect the actual callable TypeScript declarations.

**Request:** Started an ephemeral Codex CLI task using the configured `roslyn_workbench_dogfood` Windows-to-WSL launcher and requested declaration-only inspection without tool calls.

**Outcome:** Failed before declaration inspection. The nested WSL CLI reported that the MCP connection closed while awaiting the `initialize` response and exposed no dogfood tools. This launcher failure was retained as test-harness evidence and was not treated as a schema result.

### 38. Fresh Codex projection retry with direct candidate launch

**Purpose:** Remove the nested Windows-to-WSL launcher from the previous failed projection attempt.

**Request:** Started an ephemeral Codex CLI task with `roslyn_workbench_dogfood` overridden to execute `<dogfood-candidate>/Roslyn.Workbench.Mcp` directly and requested declaration-only inspection without tool calls.

**Outcome:** Failed before declaration inspection with the same closed-connection report and no exposed dogfood tools. Subsequent protocol-level checks proved that the candidate itself could initialise and list tools, so this remained a fresh-CLI harness failure rather than evidence about the projected declarations.

### 39. `initialize`

**Purpose:** Isolate candidate startup from the failed nested Codex projection harness.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-smoke","version":"1.0"}}`

**Outcome:** Succeeded. The candidate returned MCP protocol version `2025-06-18`, its server identity and tool capability.

### 40. `tools/list`

**Purpose:** Confirm that the promoted pre-commit candidate publishes its complete catalogue and revised schemas before restarting the desktop MCP connection for the decisive Codex projection check.

**Request:** `{}`

**Outcome:** Succeeded with 56 published tools. Representative raw schemas exposed complete `ScopeSelector` alternatives, concrete nested project and document selectors, and all four `SnapshotPrecondition` fields. Raw JSON Schema success does not by itself close the Codex TypeScript projection acceptance criterion.

### 41. Desktop Codex `tools/list` projection after restart

**Purpose:** Perform the decisive DOGFOOD-002 acceptance check using Codex's actual callable TypeScript projection after restarting the desktop MCP connection against the promoted pre-commit candidate.

**Request:** Automatic MCP initialisation and `tools/list` performed by the restarted Codex desktop connection.

**Outcome:** The candidate published 56 callable tools, but 35 projected declarations still contained literal `unknown`. `WorkspaceSelector` and `SnapshotPrecondition` were concrete, and the outer `ScopeSelector` kind alternatives were visible, but nested project, document, location, span, selection and symbol-selector members still collapsed to `unknown`. No `unknown & unknown` intersections remained. DOGFOOD-002 therefore did not meet its published projection acceptance criteria.

### 42. `server-status`

**Purpose:** Confirm that the restarted desktop connection was using the promoted pre-commit candidate when the failed projection result was observed.

**Request:** `{"detail":"Minimal"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 81 refactoring providers, 169 code-fix providers and 56 published tools.

### 43. `workspace-list`

**Purpose:** Check the published dogfood Workspace state before inspecting the projection-compatibility correction.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 44. `workspace-open`

**Purpose:** Open the current solution for Roslyn-backed inspection of the simplified DOGFOOD-002 transformer and tests.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood002-simplification","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 45. `search-symbols`

**Purpose:** Locate the transformer and focused tests on the simplified implementation baseline.

**Request:** `{"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-simplification"}}`

**Outcome:** Succeeded with the production transformer and focused test type.

### 46. `get-document-outline`

**Purpose:** Inspect the simplified transformer's member structure through the published Roslyn-backed server before final local validation.

**Request:** `{"document":{"path":"src/Roslyn.Workbench.Mcp/Protocol/InputContractSchemaTransformer.cs"},"includeMembers":true,"maxDepth":3,"nodesLimit":300,"workspace":{"alias":"dogfood002-simplification"}}`

**Outcome:** Succeeded and mapped the transformer members. The response was truncated after reaching the requested node budget.

### 47. `workspace-close`

**Purpose:** Close the temporary simplification Workspace after completing Roslyn-backed inspection.

**Request:** `{"workspace":{"alias":"dogfood002-simplification"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 48. Candidate `initialize`

**Purpose:** Begin the isolated protocol smoke test for the staged projection-compatible candidate before promotion.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-smoke","version":"1.0"}}`

**Outcome:** The candidate handled the request, but the first smoke harness sent the following `tools/list` request without waiting for initialization to complete and could not summarise the interleaved responses. This was retained as a harness-ordering failure.

### 49. Candidate `tools/list`

**Purpose:** List the isolated candidate catalogue during the first protocol smoke attempt.

**Request:** `{}`

**Outcome:** The candidate handled the request, but it arrived before the asynchronous initialization exchange had completed. The harness found no catalogue at its expected response position and was replaced with a properly sequenced client.

### 50. Candidate `initialize`

**Purpose:** Retry candidate initialization with a client that waits for the initialization response before sending later protocol messages.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18` and server version `1.0.0.0`.

### 51. Candidate `tools/list`

**Purpose:** Confirm the staged candidate publishes its complete catalogue after successful initialization.

**Request:** `{}`

**Outcome:** Succeeded with 56 published tools. The isolated candidate was then promoted atomically to the configured dogfood `current` target for the decisive desktop Codex projection check.

### 52. Desktop Codex `tools/list` projection after second candidate restart

**Purpose:** Perform the decisive DOGFOOD-002 acceptance check against the simplified second candidate using Codex's actual callable TypeScript projection.

**Request:** Automatic MCP initialization and `tools/list` performed by the restarted Codex desktop connection.

**Outcome:** The candidate published 56 callable tools. Literal `unknown` projections fell from 35 declarations to 26, and no `unknown & unknown` intersections appeared. `workspace-list` retained its harmless open argument map, but the other 25 affected tools still contained material opaque fields. The common failure was `DocumentSelector.project` inside selection and document-scope shapes; the most composition-heavy `find-callees` and `get-control-flow-graph` declarations also reduced complete location alternatives to `unknown`. Equivalent selectors were concrete in simpler declarations such as `transaction-preview` and `search-symbols`. The candidate therefore failed the published projection acceptance criteria.

### 53. `server-status`

**Purpose:** Confirm the live server state after inspecting the second candidate's desktop Codex projection.

**Request:** `{"detail":"Minimal"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 81 refactoring providers, 169 code-fix providers and 56 published tools. Together with the changed projection signature, this confirmed that the restarted connection had loaded the newly promoted candidate.

### 54. Candidate `initialize`

**Purpose:** Start a direct raw-schema comparison against the retained failed second candidate while diagnosing the remaining Codex projections.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-analysis","version":"1.0"}}`

**Outcome:** Succeeded with the candidate Host.

### 55. Candidate `tools/list`

**Purpose:** Measure raw schema size, depth, composition and reference counts for representative passing and failing declarations.

**Request:** `{}`

**Outcome:** Succeeded with 56 tools. The selected failing schemas contained local references but no root `$defs`, contradicting the earlier assumption that repeated selectors were always represented through `$defs`.

### 56. Candidate `initialize`

**Purpose:** Start a second direct candidate session for full raw-schema inspection of representative tools.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-analysis","version":"1.0"}}`

**Outcome:** Succeeded with the candidate Host.

### 57. Candidate `tools/list`

**Purpose:** Compare complete raw input schemas for `search-symbols`, `find-references` and `get-control-flow-graph`.

**Request:** `{}`

**Outcome:** Succeeded and exposed the first concrete root cause. Inlined selectors were complete, while repeated selectors used absolute local references beginning `#/properties/request/...` even though the published schema root no longer contained the synthetic `request` property.

### 58. `workspace-list`

**Purpose:** Check the stable published dogfood Workspace state before tracing the schema extraction boundary through current source.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 59. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed tracing of input-schema creation and normalization.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood002-ref-analysis","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 60. `search-symbols`

**Purpose:** Locate `ToolSchemaFactory`, its interface and focused tests while tracing the publication boundary.

**Request:** `{"query":"ToolSchemaFactory","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-ref-analysis"}}`

**Outcome:** Succeeded with the production interface and implementation plus unit and integration test types.

### 61. `search-symbols`

**Purpose:** Locate `McpSdkSchemaProvider` and its focused integration tests.

**Request:** `{"query":"McpSdkSchemaProvider","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-ref-analysis"}}`

**Outcome:** Succeeded with the production interface and implementation plus the integration test type.

### 62. `search-symbols`

**Purpose:** Trace input-schema creation methods and confirm where the probe-generated request fragment is extracted.

**Request:** `{"query":"CreateInputSchema","kinds":["Method"],"symbolsLimit":30,"workspace":{"alias":"dogfood002-ref-analysis"}}`

**Outcome:** Succeeded with six methods, including `McpSdkSchemaProvider.CreateInputSchemaCore` and both `ToolSchemaFactory` entry points.

### 63. `search-symbols`

**Purpose:** Locate the normalization method used after extracting the probe's `request` property.

**Request:** `{"query":"NormalizeExportedSchema","kinds":["Method"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-ref-analysis"}}`

**Outcome:** Succeeded with `ToolSchemaBuilder.NormalizeExportedSchema`. Source inspection confirmed that it normalizes top-level object nullability and copies `$defs` but does not rebase local JSON Pointers.

### 64. Candidate `initialize`

**Purpose:** Start a catalogue-wide local-reference integrity analysis against the retained failed candidate.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-ref-analysis","version":"1.0"}}`

**Outcome:** Succeeded with the candidate Host.

### 65. Candidate `tools/list`

**Purpose:** Resolve every published same-document `$ref` from its published input-schema root and simulate removal of the discarded wrapper prefix.

**Request:** `{}`

**Outcome:** Succeeded with 56 tools. All 38 local references were unresolved across exactly the 25 non-parameterless tools whose Codex declarations contained material `unknown`. Removing `#/properties/request` repaired 36 references; two unresolved references remained in `analyze-nullability`.

### 66. Candidate `initialize`

**Purpose:** Start a focused raw-schema inspection for the two references not repaired by wrapper-prefix rebasing.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-ref-analysis","version":"1.0"}}`

**Outcome:** Succeeded with the candidate Host.

### 67. Candidate `tools/list`

**Purpose:** Inspect the complete `analyze-nullability` input schema and the locations of its two unresolved references.

**Request:** `{}`

**Outcome:** Succeeded. Both location selectors referenced the SDK's original canonical path `scope.properties.document.properties.project`; the discriminator transformation had replaced `scope.properties` with `oneOf` branches and therefore removed that target. This established the second root cause.

### 68. `workspace-close`

**Purpose:** Close the temporary reference-analysis Workspace after completing the Roslyn-backed trace.

**Request:** `{"workspace":{"alias":"dogfood002-ref-analysis"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 69. `workspace-list`

**Purpose:** Check the stable published dogfood Workspace state before implementing the approved third candidate.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces.

### 70. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed inspection of the approved reference-safe extraction boundary.

**Request:** `{"path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","alias":"dogfood002-third-candidate","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 30 projects and 1,634 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 71. `search-symbols`

**Purpose:** Locate `McpSdkSchemaProvider` before changing input-schema extraction.

**Request:** `{"query":"McpSdkSchemaProvider","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Failed with `WorkspaceBusy` because several read-only symbol searches were submitted concurrently. The server requested an identical retry.

### 72. `search-symbols`

**Purpose:** Locate `ToolSchemaBuilder` and its focused tests before separating input extraction from generic exported-schema normalisation.

**Request:** `{"query":"ToolSchemaBuilder","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Succeeded with the production type and unit test type.

### 73. `search-symbols`

**Purpose:** Locate `InputContractSchemaTransformer` and its focused tests before simplifying it to property-local metadata and configuration validation.

**Request:** `{"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Succeeded with the production type and contract test type.

### 74. `search-symbols`

**Purpose:** Locate `CreateInputSchemaCore` and confirm the exact request-fragment extraction method.

**Request:** `{"query":"CreateInputSchemaCore","kinds":["Method"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Failed with `WorkspaceBusy` during the concurrent symbol-search attempt. The server requested an identical retry.

### 75. `search-symbols`

**Purpose:** Retry the `McpSdkSchemaProvider` lookup after the concurrent request was rejected.

**Request:** `{"query":"McpSdkSchemaProvider","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Succeeded with the provider interface and implementation plus its integration test type.

### 76. `search-symbols`

**Purpose:** Retry the input-schema creation lookup after the concurrent request was rejected.

**Request:** `{"query":"CreateInputSchemaCore","kinds":["Method"],"symbolsLimit":20,"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Succeeded with `McpSdkSchemaProvider.CreateInputSchemaCore(Type)` at the probe-generated request extraction boundary.

### 77. `workspace-close`

**Purpose:** Close the implementation-inspection Workspace after the approved third-candidate production and test changes were complete.

**Request:** `{"workspace":{"alias":"dogfood002-third-candidate"}}`

**Outcome:** Succeeded and closed the current solution Workspace.

### 78. Candidate `initialize`

**Purpose:** Start the isolated protocol smoke test for the staged third-candidate publication before any promotion of the configured dogfood target.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18` and server version `1.0.0.0`.

### 79. Candidate `tools/list`

**Purpose:** Confirm the isolated third candidate publishes the complete catalogue with reference-safe input schemas.

**Request:** `{}`

**Outcome:** Succeeded with 56 published tools and 38 same-document input-schema references. All 38 references resolved from their published schema roots, and none retained the discarded `#/properties/request` wrapper path. The candidate remained isolated and was not promoted to the configured `current` target.

### 80. Desktop Codex `initialize` after third-candidate promotion

**Purpose:** Reconnect the desktop MCP client after promoting the prepared third candidate to the configured dogfood `current` target.

**Request:** Automatic MCP initialization performed by the restarted Codex desktop connection.

**Outcome:** Succeeded. The restarted connection exposed the third candidate's revised callable declarations.

### 81. Desktop Codex `tools/list` projection after third-candidate restart

**Purpose:** Perform the decisive DOGFOOD-002 acceptance check against the third candidate using Codex's actual callable TypeScript projection.

**Request:** Automatic `tools/list` performed by the restarted Codex desktop connection.

**Outcome:** Succeeded with 56 callable tools. Only `workspace-list` retained a literal `unknown`, representing the remaining open argument map for its empty request contract. No declaration contained `unknown & unknown`. Previously failing reference-heavy declarations including `find-references`, `get-control-flow-graph`, `analyze-nullability` and `find-callees` exposed complete nested project, document, location, scope, snapshot and symbol-selector members. The candidate met the principal DOGFOOD-002 published projection acceptance criteria, with the empty-request projection retained for follow-up.

### 82. Candidate `initialize`

**Purpose:** Start an isolated raw-schema inspection to explain the remaining `workspace-list` open-map projection.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-empty-schema-analysis","version":"1.0"}}`

**Outcome:** Succeeded with the promoted third-candidate Host.

### 83. Candidate `tools/list`

**Purpose:** Inspect the raw `workspace-list` input schema behind Codex's final literal `unknown` projection.

**Request:** `{}`

**Outcome:** Succeeded. `workspace-list` published only `{"type":"object"}`: the empty request declared no named properties but also did not explicitly prohibit additional properties. Codex therefore projected it as the open map `{ [key: string]: unknown }`. This established that closing only otherwise-unconstrained empty request objects should produce an empty callable shape without affecting material request contracts.

### 84. Empty-request candidate `initialize`

**Purpose:** Start an isolated protocol smoke test for the candidate that closes otherwise-unconstrained empty request objects.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-002-empty-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18` and server version `1.0.0.0`.

### 85. Empty-request candidate `tools/list`

**Purpose:** Verify the published catalogue and raw `workspace-list` schema before promoting the empty-request candidate.

**Request:** `{}`

**Outcome:** Succeeded with 56 published tools. `workspace-list` published `{"type":"object","additionalProperties":false}`, explicitly representing an empty request rather than an open argument map.

### 86. Desktop Codex `initialize` after empty-request candidate restart

**Purpose:** Reconnect the desktop MCP client after promoting the candidate that closes empty request objects.

**Request:** Automatic MCP initialisation performed by the restarted Codex desktop connection.

**Outcome:** Succeeded. The connection later exposed the complete 56-tool dogfood catalogue.

### 87. Desktop Codex `tools/list` projection after empty-request candidate restart

**Purpose:** Perform the decisive DOGFOOD-002 callable-projection check for the closed empty request.

**Request:** Automatic `tools/list` performed by the restarted Codex desktop connection.

**Outcome:** Succeeded with 56 callable tools. `workspace-list` projected as `args: {}` and no declaration contained a literal `unknown` or an `unknown & unknown` intersection.

### 88. Diagnostic Codex CLI `initialize`

**Purpose:** Isolate the apparent absence of dogfood tools while the restarted desktop catalogue was still deferred.

**Request:** Automatic MCP initialisation performed by an ephemeral Codex CLI session with the configured MCP servers.

**Outcome:** The CLI session completed, but a shutdown warning reported that one configured MCP client had failed its initialisation handshake without identifying the server. No dogfood tool call was attempted in this session.

### 89. Diagnostic Codex CLI catalogue load

**Purpose:** Observe whether the configured dogfood catalogue became available during the first ephemeral diagnostic session.

**Request:** Automatic MCP catalogue loading performed by Codex.

**Outcome:** The dogfood catalogue was not exposed to the session. Because the client did not identify which configured server produced its shutdown warning, this attempt was retained as inconclusive evidence.

### 90. Isolated diagnostic Codex CLI `initialize`

**Purpose:** Retry with other configured MCP servers disabled so the dogfood connection could be assessed independently.

**Request:** Automatic MCP initialisation performed by an ephemeral Codex CLI session.

**Outcome:** Completed without an MCP startup warning.

### 91. Isolated diagnostic Codex CLI catalogue load

**Purpose:** Ask the isolated Codex session to locate `workspace-list` while MCP tools were configured for deferred discovery.

**Request:** Automatic MCP catalogue loading performed by Codex.

**Outcome:** The agent-facing session did not expose the deferred tool, so no `workspace-list` request reached the Host.

### 92. Eager diagnostic Codex CLI `initialize`

**Purpose:** Retry the isolated check while requesting eager MCP tool exposure from the client.

**Request:** Automatic MCP initialisation performed by an ephemeral Codex CLI session.

**Outcome:** Completed without an MCP startup warning.

### 93. Eager diagnostic Codex CLI catalogue load

**Purpose:** Force the complete dogfood catalogue through Codex's callable-schema conversion.

**Request:** Automatic MCP catalogue loading performed by Codex.

**Outcome:** The ephemeral session still reported the tool as unavailable because this Codex build treats the MCP deferral feature as permanently enabled. The desktop session's delayed catalogue load completed immediately afterwards and provided the decisive projection evidence recorded above.

### 94. Desktop Codex `workspace-list`

**Purpose:** Confirm the closed empty request is callable end-to-end after its TypeScript projection became available.

**Request:** `{}`

**Outcome:** Succeeded with an empty `workspaces` collection and no transaction owner. This confirmed that `workspace-list` accepts the exact empty callable shape without an open-map `unknown`.

### 95. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed inspection before correcting the stale published-schema acceptance test identified by the final review.

**Request:** `{"alias":"dogfood002-acceptance-review-fix","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>"}`

**Outcome:** Opened the solution with 30 projects and 1,592 documents. Workspace loading also reported unresolved package-reference errors because the published dog's NuGet cache did not contain several restored packages, so no semantic query was attempted against the incomplete compilation.

### 96. `workspace-close`

**Purpose:** Close the acceptance-correction inspection Workspace after the incomplete package cache made semantic queries unsuitable.

**Request:** `{"workspace":{"alias":"dogfood002-acceptance-review-fix"}}`

**Outcome:** Succeeded and closed the solution Workspace.

### 97. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed DOGFOOD-003 design discovery around server-instruction construction, registration and coverage.

**Request:** `{"alias":"dogfood003-design-discovery","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,636 documents. The explicit WSL artefacts path allowed a complete load with only the expected advisory warning about accessing a Windows-hosted repository through WSL.

### 98. `search-symbols`

**Purpose:** Locate the single production type that constructs and configures MCP server instructions.

**Request:** `{"query":"RoslynWorkbenchMcpServerOptionsConfiguration","kinds":["NamedType"],"symbolsLimit":20,"workspace":{"alias":"dogfood003-design-discovery"}}`

**Outcome:** Succeeded with the Host options configurator definition.

### 99. `find-references`

**Purpose:** Confirm the configurator's production registration and ensure no additional typed production consumer rewrites its instructions.

**Request:** `{"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Hosting.RoslynWorkbenchMcpServerOptionsConfiguration"},"includeDefinitions":true,"includeContext":true,"referencesLimit":100,"workspace":{"alias":"dogfood003-design-discovery"}}`

**Outcome:** Succeeded with the configurator definition, constructor, static instruction creation and its single dependency-injection registration. No second production instruction-construction path was found.

### 100. `workspace-close`

**Purpose:** Close the DOGFOOD-003 design-discovery Workspace after completing semantic inspection.

**Request:** `{"workspace":{"alias":"dogfood003-design-discovery"}}`

**Outcome:** Succeeded and closed the solution Workspace.

### 101. `workspace-list`

**Purpose:** Confirm the published dogfood process has no loaded Workspace or transaction owner before repeating the DOGFOOD-004 selector workflows.

**Request:** `{}`

**Outcome:** Succeeded with an empty `workspaces` collection and no transaction owner.

### 102. `workspace-open`

**Purpose:** Open the solution on the published DOGFOOD-002 build for repeated selector-workflow evidence before deciding whether DOGFOOD-004 needs a contract change.

**Request:** `{"alias":"dogfood004-design-discovery","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,636 documents. The explicit WSL artefacts path produced a complete load with only the expected Windows-filesystem performance warning.

### 103. `get-symbol-members`

**Purpose:** Repeat the original documentation-comment workflow against a type present in multiple projects and assess whether an ambiguity response is sufficient to recover without a separate search.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore"},"includeInherited":false,"membersLimit":100,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null}}`

**Outcome:** Failed with `SymbolAmbiguous`. The response said only that multiple results matched and instructed the caller to resolve the target and replace the selector; it supplied no bounded candidates or project identities. A separate discovery query was still necessary.

### 104. `search-symbols`

**Purpose:** Recover from the documentation-comment ambiguity and obtain the production type's project, document, span and snapshot identity for the returned-location reuse workflow.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"query":"CommitRecoveryStore","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with three search results. The production `CommitRecoveryStore` result included a source location containing a document selector, flat `span.start` and `span.length`, line and column, plus the complete snapshot identity.

### 105. `get-symbol-members`

**Purpose:** Test whether the source `location` returned by `search-symbols` can be passed unchanged as the next tool's symbol-location selector.

**Request:** The production search result's complete `location` object was passed unchanged as `symbol.location`, alongside its returned snapshot as `expectedSnapshot`.

**Outcome:** Failed with `InvalidRequest` because the returned location's top-level `document` property does not map to `LocationSelector`. The output uses `location.document` plus flat `location.span.start` and `location.span.length`; the input requires `location.span.document` plus `location.span.range`. The returned location is therefore not directly reusable despite the corrected callable declaration.

### 106. `get-symbol-members`

**Purpose:** Retry with the returned location manually reshaped into `LocationSelector.span`, retaining its document ID, path and project ID to preserve all supplied identity.

**Request:** `symbol.location` was rebuilt as `{"span":{"document":{"documentId":"<document-id>","path":"src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs","project":{"projectId":"<project-id>"}},"range":{"start":163,"length":19}}}` with the returned snapshot precondition.

**Outcome:** Failed with `InvalidRequest` because `DocumentSelector` requires exactly one of `path` or `documentId`. Reusing the output requires the caller not only to restructure the location but also to discard one of the two document identities returned by the server.

### 107. `get-symbol-members`

**Purpose:** Complete the returned-location workflow after converting the output into the input contract and retaining only the document ID plus project ID.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"symbol":{"location":{"span":{"document":{"documentId":"<document-id>","project":{"projectId":"<project-id>"}},"range":{"start":163,"length":19}}}},"includeInherited":false,"membersLimit":100,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null}}`

**Outcome:** Succeeded and returned the production `CommitRecoveryStore` member inventory. The workflow is possible, but it required knowledge of two different location shapes, manual field movement, renaming `span` to `range`, transferring the snapshot into a separate precondition and discarding the returned path.

### 108. `search-symbols`

**Purpose:** Locate the public input-side `LocationSelector` contract and its acceptance helper before comparing selector and result-location ownership.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"query":"LocationSelector","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the public abstractions contract and the acceptance-test selector factory.

### 109. `search-symbols`

**Purpose:** Locate the existing selector-projection service and tests after the replay showed that callers must manually convert `ResolvedLocation` output into `LocationSelector` input.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"query":"WorkspaceSelectorFactory","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the public factory interface, internal implementation and focused unit-test type. The implementation already contains the canonical conversion from `ResolvedLocation` to `LocationSelector` and deliberately chooses document ID over path.

### 110. `search-symbols`

**Purpose:** Identify the precise public and internal `CreateLocationSelector` methods before tracing whether result projection already exposes the canonical conversion broadly.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"query":"CreateLocationSelector","kinds":["Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the public interface method, internal implementation and test-support helper.

### 111. `find-references`

**Purpose:** Determine how broadly the existing `ResolvedLocation`-to-`LocationSelector` conversion is used before choosing the DOGFOOD-004 publication boundary.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"},"symbol":{"documentationCommentId":"M:Roslyn.Workbench.Mcp.Workspace.Selectors.IWorkspaceSelectorFactory.CreateLocationSelector(Roslyn.Workbench.Mcp.Workspace.Selectors.ResolvedLocation)","project":{"projectId":"<abstractions-project-id>"}},"includeDefinitions":true,"includeContext":true,"referencesLimit":100,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null}}`

**Outcome:** Succeeded with the interface and implementation definitions, the internal reuse by `CreateSymbolSelector`, the API-surface assertion and focused factory tests. Result models do not automatically expose the conversion; production currently uses the factory selectively, including the dedicated `resolve-symbol` workflow.

### 112. `workspace-close`

**Purpose:** Close the DOGFOOD-004 design-discovery Workspace after completing the repeated selector workflows and semantic inspection.

**Request:** `{"workspace":{"alias":"dogfood004-design-discovery"}}`

**Outcome:** Succeeded and closed the solution Workspace.

### 113. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed inspection while adding the newly approved published-Host acceptance boundary for DOGFOOD-004.

**Request:** `{"alias":"dogfood-004-acceptance-design","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>"}`

**Outcome:** Succeeded with a usable Workspace, but reported incomplete-package load diagnostics because this request omitted the WSL artefacts path used by the repository restore. The affected acceptance test project remained available for semantic queries.

### 114. `search-symbols`

**Purpose:** Locate the acceptance test class that already owns published inspection-response pointer assertions before selecting the focused round-trip test boundary.

**Request:** `{"workspace":{"workspaceId":"<workspace-id>"},"query":"PublishedInspectionResponseIntegrationTests","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the single acceptance test type and its resolved source location. The published DOGFOOD-002 result retained the pre-DOGFOOD-004 location shape, as expected.

### 115. `get-symbol-members`

**Purpose:** Inspect the semantic structure of the existing published inspection-response acceptance class and confirm that its shared pointer assertion does not own selector reuse.

**Request:** `{"workspace":{"workspaceId":"<workspace-id>"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.AcceptanceTest.PublishedInspectionResponseIntegrationTests"},"membersLimit":30,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null}}`

**Outcome:** Succeeded and showed that `AssertPointer` is limited to output pointer fields, while `WorkspaceWorkflowIntegrationTests` is the clearer owner for a focused search-to-members published workflow.

### 116. `workspace-close`

**Purpose:** Close the temporary DOGFOOD-004 acceptance-design Workspace after semantic inspection completed.

**Request:** `{"workspace":{"workspaceId":"<workspace-id>"}}`

**Outcome:** Succeeded and closed the solution Workspace.

### 117. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed ownership and call-site inspection while reviewing whether canonical result-selector creation belongs in `IWorkspaceSelectorFactory`.

**Request:** `{"alias":"dogfood004-factory-review","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents. The explicit WSL artefacts path produced a complete load with only the expected Windows-filesystem performance warning.

### 118. `search-symbols`

**Purpose:** Locate every `CreateLocationSelector` declaration before assessing whether a separate canonical-result factory method would clarify ownership.

**Request:** `{"workspace":{"workspaceId":"<workspace-id>"},"query":"CreateLocationSelector","kinds":["Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the public interface method, internal implementation and unrelated test-support helper.

### 119. `find-references`

**Purpose:** Determine the production and public-contract impact of adding a separate canonical-result selector factory method.

**Request:** `{"workspace":{"workspaceId":"<workspace-id>"},"symbol":{"documentationCommentId":"M:Roslyn.Workbench.Mcp.Workspace.Selectors.IWorkspaceSelectorFactory.CreateLocationSelector(Roslyn.Workbench.Mcp.Workspace.Selectors.ResolvedLocation)","project":{"projectId":"<abstractions-project-id>"}},"includeDefinitions":true,"includeContext":false,"referencesLimit":100,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null}}`

**Outcome:** Succeeded with 12 definitions and references. Production use is limited to the factory's symbol-selector composition and the new `WorkspaceResolver` projection; the interface is also deliberately locked as public plugin API, so another method would be an additive public capability rather than a purely internal extraction.

### 120. `workspace-close`

**Purpose:** Close the temporary selector-factory review Workspace after semantic inspection completed.

**Request:** `{"workspace":{"workspaceId":"<workspace-id>"}}`

**Outcome:** Succeeded and closed the solution Workspace.

### 121. `workspace-list`

**Purpose:** Confirm that no published dogfood Workspace remained open when beginning the independent staged review of DOGFOOD-004.

**Request:** `{}`

**Outcome:** Succeeded with an empty `workspaces` collection and no transaction owner.

### 122. `server-status`

**Purpose:** Confirm the restarted published dogfood server is healthy before validating the staged DOGFOOD-004 selector-composability change.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 56 published tools and no startup warnings. An operating-system process check separately confirmed that the live executable resolved to the new `dogfood-004-precommit-OKJuTo` publish.

### 123. `workspace-open`

**Purpose:** Open the solution on the new DOGFOOD-004 publish so a returned canonical selector can be reused through the live Codex MCP client.

**Request:** `{"alias":"dogfood-004-validation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<dogfood-artifacts-path>"}}`

**Outcome:** Succeeded with 30 projects and 1,593 documents. The load reported the expected WSL Windows-filesystem performance warning and one unresolved analyser reference in a Host query-plugin fixture; the production projects required for selector validation loaded successfully.

### 124. `search-symbols`

**Purpose:** Obtain a live resolved source location for `WorkspaceResolver` and verify that DOGFOOD-004 publishes a canonical selector alongside the descriptive location fields.

**Request:** `{"workspace":{"alias":"dogfood-004-validation"},"query":"WorkspaceResolver","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with 10 of 15 matching types. The production `WorkspaceResolver` result contained its descriptive document, flat span, line, column and snapshot fields plus `location.selector.span`, using only the document ID and project ID with `range.start` `78` and `range.length` `17`.

### 125. `get-code-context`

**Purpose:** Validate the DOGFOOD-004 agent workflow by passing the `WorkspaceResolver` result's canonical selector unchanged into a second published tool.

**Request:** `{"workspace":{"alias":"dogfood-004-validation"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"project":{"projectId":"<project-id>","name":null,"path":null,"targetFramework":null},"path":null,"documentId":"<document-id>"},"range":{"start":78,"length":17}}},"beforeLines":2,"afterLines":8,"includeEnclosingSymbols":true,"includeDiagnostics":false}`

**Outcome:** Succeeded without reshaping or discarding selector fields. The tool resolved `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs` at the same span, returned the expected class context and republished an identical canonical selector, confirming direct unchanged-selector reuse through the live Codex client.

### 126. `workspace-close`

**Purpose:** Close the DOGFOOD-004 validation Workspace after the unchanged-selector workflow completed.

**Request:** `{"workspace":{"alias":"dogfood-004-validation"}}`

**Outcome:** Succeeded and closed the solution Workspace.

### 127. `workspace-open`

**Purpose:** Open the solution for Roslyn-backed inspection of whether the reported query-response contract warnings belong in plugin status diagnostics, application logging or compile-time analyser output.

**Request:** `{"alias":"dogfood-response-diagnostic-review","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<repository-artifacts-path>"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents. The explicit WSL artefacts path produced a complete load with only the expected Windows-filesystem performance warning.

### 128. `search-symbols`

**Purpose:** Locate the runtime inspector that produces `QueryResponseContract` warnings and its focused tests.

**Request:** `{"workspace":{"alias":"dogfood-response-diagnostic-review"},"query":"QueryResponseContractInspector","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production `QueryResponseContractInspector` in the Host plugin-loading layer and its unit-test class.

### 129. `search-symbols`

**Purpose:** Check whether the compile-time counterpart used the expected response-contract-analyser type name before inspecting the analyser project directly.

**Request:** `{"workspace":{"alias":"dogfood-response-diagnostic-review"},"query":"ResponseContractAnalyzer","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with no matching type. Subsequent source inspection established that `RWMCP014` is implemented within the broader `PluginInvocationAnalyzer` rather than a dedicated response-contract analyser class.

### 130. `find-references`

**Purpose:** Establish where runtime query-response warnings enter plugin status and whether they have any separate logging or reporting path.

**Request:** `{"workspace":{"alias":"dogfood-response-diagnostic-review"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.PluginLoading.QueryResponseContractInspector","project":{"projectId":"<host-project-id>"}},"includeDefinitions":true,"includeContext":true,"referencesLimit":50,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null}}`

**Outcome:** Succeeded with the single production call in `PluginCatalogEntryMaterializer.CreateDiagnostics`, where inspector warnings are appended to the enabled plugin's catalogue diagnostics. All other references were the definition and focused tests; no application-logging path was present.

### 131. `workspace-close`

**Purpose:** Close the response-diagnostic review Workspace after completing semantic inspection.

**Request:** `{"workspace":{"alias":"dogfood-response-diagnostic-review"}}`

**Outcome:** Succeeded and closed the solution Workspace.

## DOGFOOD-005 — Controlled mutation dogfooding

### 132. `workspace-open`

**Purpose:** Open the committed clean solution for the approved semantic-rename preview-and-rollback workflow.

**Request:** `{"alias":"dogfood-005-controlled-mutation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<repository-artifacts-path>"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents. The explicit WSL artefacts path produced a complete load with only the expected Windows-filesystem performance warning.

### 133. `workspace-status`

**Purpose:** Enforce the approved mutation gate by confirming lifecycle, transaction and cross-instance state before resolving or mutating the target.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, no transaction, no reload requirement and an empty `instances` collection. The Workspace was safe to use for the controlled mutation.

### 134. `search-symbols`

**Purpose:** Resolve the private `_target` field within the approved `WorkspaceSelectorFactoryTests.cs` document before starting a transaction.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"},"query":"_target","kinds":["Field"],"scope":{"kind":"Document","document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Selectors/WorkspaceSelectorFactoryTests.cs"}},"symbolsLimit":10}`

**Outcome:** Succeeded but did not honour the document scope. It returned 10 of 43 solution-wide `_target` or `_targets` fields from unrelated documents and did not include the requested document within the first page. This is separate dogfood evidence of a scope-enforcement defect; no transaction had started.

### 135. `search-symbols`

**Purpose:** Recover safely from the ineffective document scope by combining project and namespace constraints to resolve exactly the approved field.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"},"query":"_target","namespace":"Roslyn.Workbench.Mcp.Workspace.Test.Selectors","kinds":["Field"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}},"symbolsLimit":20}`

**Outcome:** Succeeded with the single `WorkspaceSelectorFactoryTests._target` field. Its resolved location included the canonical span selector later passed unchanged to `rename-symbol`.

### 136. `transaction-start`

**Purpose:** Open the controlled in-memory transaction only after the Workspace and exact mutation target were verified.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"}}`

**Outcome:** Succeeded at revision `0` with no staged revisions, 20 remaining revisions, mutation and rollback enabled, and commit disabled while the transaction was empty.

### 137. `rename-symbol`

**Purpose:** Exercise one supported semantic mutation by renaming the private test field without expanding to files, overloads, comments or strings.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<base-snapshot-id>","transactionRevision":0},"symbol":{"location":{"span":{"document":{"project":{"projectId":"<workspace-test-project-id>","name":null,"path":null,"targetFramework":null},"path":null,"documentId":"<document-id>"},"range":{"start":160,"length":7}}}},"newName":"_selectorFactoryUnderTest","renameFile":false,"renameInComments":false,"renameInStrings":false,"renameOverloads":false}`

**Outcome:** Succeeded and staged `Rename '_target' to '_selectorFactoryUnderTest'.` at transaction revision `1` with a new staged snapshot.

### 138. `transaction-preview`

**Purpose:** Inspect the complete staged semantic rename before discarding it and confirm the transaction remained eligible for rollback.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"},"document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Selectors/WorkspaceSelectorFactoryTests.cs"},"includeDiff":true,"contextLines":2}`

**Outcome:** Succeeded with one modified document, 11 changed lines, four complete diff hunks and no truncation. The transaction reported revision `1`, one revision in the journal, and rollback enabled. Independent SHA-256 and Git checks confirmed the physical file and worktree were unchanged while this preview existed.

### 139. `transaction-rollback`

**Purpose:** Discard the staged semantic rename without writing any source file.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"}}`

**Outcome:** Succeeded with state `Ready`, no transaction revision and the original base snapshot restored. No `transaction-commit` request was sent.

### 140. `workspace-status`

**Purpose:** Verify the Workspace returned fully to its pre-transaction lifecycle and ownership state after rollback.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, `transaction: null`, no reload requirement and no other instances. The snapshot matched the original pre-transaction snapshot; independent checks also confirmed the original file hash and a clean Git worktree.

### 141. `workspace-close`

**Purpose:** Close the controlled-mutation Workspace after rollback and baseline verification completed.

**Request:** `{"workspace":{"alias":"dogfood-005-controlled-mutation"}}`

**Outcome:** Succeeded and closed the solution Workspace with the original snapshot identity and no transaction.

### 142. `workspace-open`

**Purpose:** Open the committed solution for the approved multi-revision, multi-document extension of DOGFOOD-005.

**Request:** `{"alias":"dogfood-005-multi-revision","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<repository-artifacts-path>"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents. The complete load reported only the expected WSL Windows-filesystem performance warning.

### 143. `workspace-status`

**Purpose:** Repeat the mutation gate before opening the expanded transaction.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, no transaction, no reload requirement and no other instances.

### 144. `search-symbols`

**Purpose:** Resolve the first approved private field through the known-safe project and namespace constraints.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"query":"_target","namespace":"Roslyn.Workbench.Mcp.Workspace.Test.Selectors","kinds":["Field"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}},"symbolsLimit":20}`

**Outcome:** Succeeded with the single `WorkspaceSelectorFactoryTests._target` field and its canonical location selector.

### 145. `search-symbols`

**Purpose:** Resolve the second approved private field in a different test document before starting the transaction.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"query":"_selectorFactory","namespace":"Roslyn.Workbench.Mcp.Workspace.Test.Resolution","kinds":["Field"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}},"symbolsLimit":20}`

**Outcome:** Succeeded with the resolver-factory and resolver-test fields. The exact `WorkspaceResolverTests._selectorFactory` result and its canonical selector were retained for the second mutation.

### 146. `transaction-start`

**Purpose:** Start the expanded transaction only after both targets and the Workspace state were verified.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"}}`

**Outcome:** Succeeded at revision `0` with 20 remaining revisions, mutation and rollback enabled, and commit disabled for the empty transaction.

### 147. `rename-symbol`

**Purpose:** Stage the first semantic rename using the first search result's canonical selector unchanged.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":4,"snapshotId":"<base-snapshot-id>","transactionRevision":0},"symbol":{"location":{"span":{"document":{"project":{"projectId":"<workspace-test-project-id>","name":null,"path":null,"targetFramework":null},"path":null,"documentId":"<selector-factory-test-document-id>"},"range":{"start":160,"length":7}}}},"newName":"_selectorFactoryUnderTest","renameFile":false,"renameInComments":false,"renameInStrings":false,"renameOverloads":false}`

**Outcome:** Succeeded and staged the `_target` to `_selectorFactoryUnderTest` rename at transaction revision `1`.

### 148. `rename-symbol`

**Purpose:** Validate revision chaining by staging a second semantic rename against the revision-1 snapshot in another document.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":4,"snapshotId":"<revision-1-snapshot-id>","transactionRevision":1},"symbol":{"location":{"span":{"document":{"project":{"projectId":"<workspace-test-project-id>","name":null,"path":null,"targetFramework":null},"path":null,"documentId":"<resolver-test-document-id>"},"range":{"start":201,"length":16}}}},"newName":"_workspaceSelectorFactory","renameFile":false,"renameInComments":false,"renameInStrings":false,"renameOverloads":false}`

**Outcome:** Succeeded and staged the `_selectorFactory` to `_workspaceSelectorFactory` rename at transaction revision `2` with a new snapshot.

### 149. `transaction-preview`

**Purpose:** Inspect the accumulated two-revision transaction summary before requesting either document's detailed diff.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"includeDiff":false,"contextLines":2}`

**Outcome:** Succeeded at revision `2` with two journal revisions and two modified documents. The resolver tests reported 9 changed lines and the selector-factory tests reported 11; no lines were added or removed.

### 150. `transaction-preview`

**Purpose:** Inspect the complete detailed diff for the first staged rename within the two-document transaction.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Selectors/WorkspaceSelectorFactoryTests.cs"},"includeDiff":true,"contextLines":1}`

**Outcome:** Succeeded with five complete, untruncated hunks covering the field declaration, construction and all uses of `_target` in the test class.

### 151. `transaction-preview`

**Purpose:** Inspect the complete detailed diff for the second staged rename within the two-document transaction.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Resolution/WorkspaceResolverTests.cs"},"includeDiff":true,"contextLines":1}`

**Outcome:** Succeeded with nine complete, untruncated hunks covering the mock field declaration, construction, setup, verification and all uses. Independent file hashes and target-specific Git checks confirmed both staged documents remained unchanged on disk.

### 152. `transaction-history`

**Purpose:** Undo the second revision and verify the journal can move backward without discarding the first staged mutation.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"direction":"Undo","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":4,"snapshotId":"<revision-2-snapshot-id>","transactionRevision":2}}`

**Outcome:** Succeeded at transaction revision `1`, retained both revisions in the journal, and reported both undo and redo as available. The snapshot returned to the exact revision-1 identity.

### 153. `transaction-preview`

**Purpose:** Confirm undo removed only the second document's staged change from the effective transaction.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"includeDiff":false,"contextLines":1}`

**Outcome:** Succeeded at revision `1` with only `WorkspaceSelectorFactoryTests.cs` in the modified-document summary and 11 changed lines.

### 154. `transaction-history`

**Purpose:** Redo the second revision using the revision-1 snapshot returned by undo.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"direction":"Redo","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":4,"snapshotId":"<revision-1-snapshot-id>","transactionRevision":1}}`

**Outcome:** Succeeded at revision `2`, restored the exact revision-2 snapshot and disabled further redo while retaining undo and rollback.

### 155. `transaction-preview`

**Purpose:** Confirm redo restored both staged documents before the final rollback.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"includeDiff":false,"contextLines":1}`

**Outcome:** Succeeded at revision `2` with both documents and their original 9- and 11-line change summaries restored.

### 156. `transaction-rollback`

**Purpose:** Discard both semantic mutations and the complete two-revision journal without writing either source file.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"}}`

**Outcome:** Succeeded with state `Ready`, no transaction revision and the original base snapshot restored. No `transaction-commit` request was sent.

### 157. `workspace-status`

**Purpose:** Verify the expanded transaction left no lifecycle, ownership or reload state after rollback.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, `transaction: null`, no reload requirement and no other instances. Both physical file hashes matched their pre-transaction values and target-specific Git diffs remained empty.

### 158. `workspace-close`

**Purpose:** Close the expanded DOGFOOD-005 Workspace after all rollback and filesystem checks passed.

**Request:** `{"workspace":{"alias":"dogfood-005-multi-revision"}}`

**Outcome:** Succeeded and closed the solution Workspace at the original base snapshot with no transaction.

### 159. `workspace-open`

**Activity:** DOGFOOD-006 design discovery.

**Purpose:** Open the trusted repository solution for semantic discovery of the bundled query-response contracts and their existing analyser and runtime-warning paths.

**Request:** `{"alias":"dogfood-006-design-discovery","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents at Workspace epoch 5. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 160. `search-symbols`

**Activity:** DOGFOOD-006 design discovery.

**Purpose:** Semantically inventory the response DTOs in the bundled core plugin whose names end in `Data`, including the seven types reported by the runtime query-response inspector.

**Request:** `{"workspace":{"alias":"dogfood-006-design-discovery"},"query":"Data","kinds":["NamedType"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"symbolsLimit":100}`

**Outcome:** Succeeded with all 39 matching named types. The results located `CodeContextData`, `ResolveSymbolData`, `SymbolInfoData`, `DefinitionData`, `ControlFlowAnalysisData`, `DataFlowAnalysisData` and `ControlFlowGraphData` in the bundled inspection contracts and confirmed their associated handler locations.

### 161. `workspace-close`

**Activity:** DOGFOOD-006 design discovery.

**Purpose:** Close the read-only discovery Workspace after the response contracts, handlers and runtime warning path had been traced.

**Request:** `{"workspace":{"alias":"dogfood-006-design-discovery"}}`

**Outcome:** Succeeded and closed the solution Workspace at the unchanged epoch-5 snapshot with no transaction.

### 162. Candidate `initialize`

**Activity:** DOGFOOD-006 committed-candidate publication.

**Purpose:** Start an isolated protocol smoke test against the Release candidate published from committed `HEAD` before promoting the configured dogfood target.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-006-smoke","version":"1.0"}}`

**Outcome:** Succeeded against the isolated candidate. Host logs confirmed that the `initialize` handler completed for client `dogfood-006-smoke`; the process later shut down cleanly on end of input.

### 163. Candidate `tools/list`

**Activity:** DOGFOOD-006 committed-candidate publication.

**Purpose:** Confirm the isolated committed candidate can materialise and publish its complete MCP tool catalogue before promotion.

**Request:** `{}`

**Outcome:** Succeeded. Host logs confirmed that the candidate's `tools/list` handler completed without an authoring-warning failure or catalogue materialisation error.

### 164. Candidate `initialize`

**Activity:** DOGFOOD-006 committed-candidate publication retry.

**Purpose:** Repeat the isolated handshake while retaining input briefly after each protocol message to rule out immediate end-of-input as the reason the shell capture did not display response JSON.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-006-smoke","version":"1.0"}}`

**Outcome:** Succeeded again. The candidate completed the `initialize` handler and retained the same Host and client identity; the shell projection continued to expose lifecycle logs rather than protocol response JSON.

### 165. Candidate `tools/list`

**Activity:** DOGFOOD-006 committed-candidate publication retry.

**Purpose:** Repeat catalogue materialisation during the delayed-input smoke session before promoting the candidate.

**Request:** `{}`

**Outcome:** Succeeded again. The candidate completed `tools/list` without a catalogue failure and shut down cleanly after input closed. The configured `current` symlink was then atomically promoted to this candidate; desktop validation remains pending a Codex restart.

### 166. `server-status`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Confirm the restarted desktop connection is running the exact committed candidate and that query-response authoring warnings are absent from agent-facing plugin diagnostics.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with core plugin version `1.0.0+b49e58030c5ffae5b0b211ab2015e0edffd34177`, 56 published tools, no startup warnings and an empty core-plugin diagnostics collection. This confirmed both the exact committed build and the corrected runtime warning channel.

### 167. `workspace-open`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Open the trusted committed repository for representative low-limit calls across every remediated response family.

**Request:** `{"alias":"dogfood-006-validation","path":"<repo-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo-root>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents at Workspace epoch 1. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 168. `get-code-context`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Verify independently bounded enclosing-symbol and diagnostic branches with an explicit one-item and zero-item limit.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs"},"selectedText":"var maxResults = request.EffectiveSymbolsPerCategoryLimit;"}},"beforeLines":1,"afterLines":1,"includeEnclosingSymbols":true,"includeDiagnostics":true,"enclosingSymbolsLimit":1,"diagnosticsLimit":0}`

**Outcome:** Succeeded. `enclosingSymbols` returned one item with `hasMore: true` and `totalCount: 10`; `diagnostics` returned an empty complete collection with `hasMore: false` and `totalCount: 0`.

### 169. `analyze-data-flow`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Prove the single per-category effective limit is applied independently to all six data-flow collections.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs"},"selectedText":"var maxResults = request.EffectiveSymbolsPerCategoryLimit;"}},"symbolsPerCategoryLimit":0}`

**Outcome:** Succeeded. `variablesDeclared`, `readInside`, `writtenInside`, `dataFlowsIn` and `dataFlowsOut` each returned zero items with `hasMore: true` and `totalCount: 1`; `captured` independently returned an empty complete collection with `totalCount: 0`.

### 170. `get-control-flow-graph`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Verify nullable defaulted CFG limits accept explicit low values through their effective-value path and publish bounded block, operation and region shapes.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs"},"selectedText":"var maxResults = request.EffectiveSymbolsPerCategoryLimit;"}},"maxBlocks":1,"maxRegions":1,"maxOperationsPerBlock":0}`

**Outcome:** Succeeded. `blocks` returned one of 13 items with `hasMore: true` and `totalCount: 13`; its operations collection was empty and complete. `regions` returned one item with `hasMore: true` and intentionally omitted `totalCount`.

### 171. `get-symbol-info`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Verify parameters and declarations are independently bounded while the fixed, language-defined modifier set remains an ordinary list.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"symbol":{"documentationCommentId":"M:Roslyn.Workbench.Mcp.Plugins.Core.Inspection.AnalyzeDataFlowTool.ExecuteCoreAsync(Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.AnalyzeDataFlowRequest,Roslyn.Workbench.Mcp.Plugins.IQueryContext,System.Threading.CancellationToken)","project":{"projectId":"<core-project-id>"}},"includeDocumentation":false,"parametersLimit":1,"declarationsLimit":0}`

**Outcome:** Succeeded. `modifiers` was the plain list `["async","override"]`; `parameters` returned one of three items with `hasMore: true` and `totalCount: 3`; `declarations` returned zero items with `hasMore: true` and intentionally omitted `totalCount`.

### 172. `go-to-definition`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Verify an explicit zero definition limit publishes the bounded source-definition shape without unnecessary projection.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"symbol":{"documentationCommentId":"M:Roslyn.Workbench.Mcp.Plugins.Core.Inspection.AnalyzeDataFlowTool.ExecuteCoreAsync(Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.AnalyzeDataFlowRequest,Roslyn.Workbench.Mcp.Plugins.IQueryContext,System.Threading.CancellationToken)","project":{"projectId":"<core-project-id>"}},"definitionsLimit":0}`

**Outcome:** Succeeded with zero definition items, `hasMore: true` and intentionally omitted `totalCount`.

### 173. `resolve-symbol`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Verify an explicit zero declaration limit while resolving a source symbol and its canonical selector.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs"},"selectedText":"ExecuteCoreAsync"}},"declarationsLimit":0}`

**Outcome:** Succeeded with the expected method and canonical source selector; `declarations` returned zero items with `hasMore: true` and intentionally omitted `totalCount`.

### 174. `analyze-control-flow`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Verify explicit zero exit and return limits preserve their cheap complete counts without projecting result locations.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs"},"selectedText":"return PluginExecutionResult.Success(data);"}},"exitsLimit":0,"returnsLimit":0}`

**Outcome:** Succeeded. Both `exits` and `returns` returned zero items with `hasMore: true` and `totalCount: 1`.

### 175. `workspace-close`

**Activity:** DOGFOOD-006 published validation.

**Purpose:** Close the read-only validation Workspace after every remediated response family passed its published low-limit check.

**Request:** `{"workspace":{"alias":"dogfood-006-validation"}}`

**Outcome:** Succeeded and closed the Workspace at its unchanged epoch-1 snapshot with no transaction.

### 176. `workspace-open`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Open the trusted repository solution against the newly published Host to revalidate symbol-search scope behaviour and trace the implementation semantically.

**Request:** `{"alias":"dogfood-007-design-discovery","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,637 documents. A subsequent minimal status request confirmed the Workspace was ready at epoch 2.

### 177. `workspace-status`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Confirm the discovery Workspace completed loading after the initial client projection was truncated.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"detail":"Minimal"}`

**Outcome:** Succeeded with state `Ready`, 30 projects, 1,637 documents, no transaction and no reload requirement.

### 178. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Reproduce the reported document-scope failure with a low result bound.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Document","document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Selectors/WorkspaceSelectorFactoryTests.cs"}}}`

**Outcome:** Succeeded incorrectly with the first 10 of 43 fields from other documents in `Roslyn.Workbench.Mcp.Workspace.Test`; the selected document was not enforced before bounding.

### 179. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Check whether the same query is correctly constrained by a single project scope.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}}}`

**Outcome:** Returned `WorkspaceBusy` with a retry continuation because this request was intentionally issued concurrently with the other scope probes.

### 180. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Check whether the same query is correctly constrained by a multi-project scope.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.Workspace.Test"},{"name":"Roslyn.Workbench.Mcp.Plugins.Core.Test"}]}}`

**Outcome:** Returned `WorkspaceBusy` with a retry continuation because this request was intentionally issued concurrently with the other scope probes.

### 181. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Establish the solution-wide baseline for the document, project and projects probes.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Solution"}}`

**Outcome:** Succeeded with the deterministic first 10 of 91 solution-wide matching fields.

### 182. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Retry the single-project probe after the concurrent `WorkspaceBusy` response.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}}}`

**Outcome:** Succeeded with the first 10 of 43 matching fields, all from the selected Workspace test project. Project scope is already enforced before bounding.

### 183. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Retry the multi-project probe after the concurrent `WorkspaceBusy` response.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.Workspace.Test"},{"name":"Roslyn.Workbench.Mcp.Plugins.Core.Test"}]}}`

**Outcome:** Succeeded with the first 10 of 43 matching fields, all from the selected project set; the second selected project had no matching field. Multi-project scope is already enforced before bounding.

### 184. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Locate the `search-symbols` request, handler and current test ownership through semantic discovery.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"SearchSymbols","kinds":["NamedType","Method"],"symbolsLimit":30,"scope":{"kind":"Solution"}}`

**Outcome:** Succeeded with the production `SearchSymbolsRequest` and `SearchSymbolsTool`, the unit-test owner, the existing Plugins.Core integration call and published-Host helper methods.

### 185. `search-symbols`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Locate existing document-resolution services before choosing an implementation boundary.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"},"query":"ResolveDocument","kinds":["Method"],"symbolsLimit":50,"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.Plugins"},{"name":"Roslyn.Workbench.Mcp.Plugins.Core"},{"name":"Roslyn.Workbench.Mcp.Workspace"}]}}`

**Outcome:** Succeeded with `IToolRequestResolver.ResolveDocument` and `ResolveDocuments`, their `ToolRequestResolver` implementation, and the underlying Workspace resolver methods. No new public resolver API is required.

### 186. `workspace-close`

**Activity:** DOGFOOD-007 design discovery.

**Purpose:** Close the read-only discovery Workspace after reproducing the defect and tracing the implementation boundary.

**Request:** `{"workspace":{"alias":"dogfood-007-design-discovery"}}`

**Outcome:** Succeeded and closed the Workspace at its unchanged epoch-2 snapshot with no transaction.

### 187. Candidate `initialize`

**Activity:** DOGFOOD-007 committed-candidate publication.

**Purpose:** Start an isolated protocol smoke test against the Release candidate published from committed `HEAD` before promoting the configured dogfood target.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-007-smoke","version":"1.0"}}`

**Outcome:** Succeeded against the isolated candidate published from commit `e2f9bf5fa462dd5e4a1239b0e3de50822aa0b430`. Host logs confirmed that the `initialize` handler completed for client `dogfood-007-smoke`; the candidate later shut down cleanly on end of input.

### 188. Candidate `tools/list`

**Activity:** DOGFOOD-007 committed-candidate publication.

**Purpose:** Confirm the isolated committed candidate can materialise and publish its complete MCP tool catalogue before promotion.

**Request:** `{}`

**Outcome:** Succeeded. Host logs confirmed that `tools/list` completed without catalogue or schema failure and the process exited cleanly. The Core plugin binary exposed informational version `1.0.0+e2f9bf5fa462dd5e4a1239b0e3de50822aa0b430`; the configured `current` symlink was then atomically promoted to this candidate for desktop validation after restart.

### 189. `server-status`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Confirm the restarted desktop connection is running the exact committed candidate before validating symbol-search scopes.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with the Core plugin version `1.0.0+e2f9bf5fa462dd5e4a1239b0e3de50822aa0b430`, 56 published tools, no startup warnings and no plugin diagnostics.

### 190. `workspace-open`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Open the trusted committed repository for live document, project, projects and solution scope checks.

**Request:** `{"alias":"dogfood-007-validation","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,638 documents at Workspace epoch 1. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 191. `search-symbols`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Repeat the originally failing document-scoped `_target` search against `WorkspaceSelectorFactoryTests.cs`.

**Request:** `{"workspace":{"alias":"dogfood-007-validation"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Document","document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Selectors/WorkspaceSelectorFactoryTests.cs"}}}`

**Outcome:** Succeeded with exactly `WorkspaceSelectorFactoryTests._target`, located in the selected document. The bounded result reported one item, `hasMore: false` and `totalCount: 1`; unrelated declarations no longer consume the bound or count.

### 192. `search-symbols`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Confirm the retained single-project search path still constrains results before bounding.

**Request:** `{"workspace":{"alias":"dogfood-007-validation"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}}}`

**Outcome:** Succeeded with the deterministic first 10 of 43 matching fields, all from `Roslyn.Workbench.Mcp.Workspace.Test`, and reported `hasMore: true` with `totalCount: 43`.

### 193. `search-symbols`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Confirm the retained multi-project path searches the union of selected projects before bounding.

**Request:** `{"workspace":{"alias":"dogfood-007-validation"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.Workspace.Test"},{"name":"Roslyn.Workbench.Mcp.CodeActions.Test"}]}}`

**Outcome:** Succeeded with the deterministic first 10 of 61 matching fields from the selected Workspace and CodeActions test projects, and reported `hasMore: true` with `totalCount: 61`.

### 194. `search-symbols`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Confirm solution scope retains the complete solution-wide search baseline after the document correction.

**Request:** `{"workspace":{"alias":"dogfood-007-validation"},"query":"_target","kinds":["Field"],"symbolsLimit":10,"scope":{"kind":"Solution"}}`

**Outcome:** Succeeded with the deterministic first 10 of 91 solution-wide matching fields and reported `hasMore: true` with `totalCount: 91`, distinct from both restricted project totals.

### 195. `workspace-close`

**Activity:** DOGFOOD-007 published validation.

**Purpose:** Close the read-only validation Workspace after all four scope kinds passed their published checks.

**Request:** `{"workspace":{"alias":"dogfood-007-validation"}}`

**Outcome:** Succeeded and closed the Workspace at its unchanged epoch-1 snapshot with no transaction.

## DOGFOOD-008 — Controlled transaction commit

### 196. `workspace-list`

**Activity:** DOGFOOD-008 design discovery.

**Purpose:** Confirm that the published Host has no existing Workspace or transaction owner before inspecting the transaction-commit workflow.

**Request:** `{}`

**Outcome:** Succeeded and reported no open Workspaces and no transaction owner.

### 197. `workspace-open`

**Activity:** DOGFOOD-008 design discovery.

**Purpose:** Open the trusted repository read-only so the published semantic tools can locate the transaction-commit adapter, mutation contract and existing disposable integration fixtures.

**Request:** `{"alias":"dogfood-008-design-discovery","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,638 documents at Workspace epoch 2. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning; no transaction was started.

### 198. `search-symbols`

**Activity:** DOGFOOD-008 design discovery.

**Purpose:** Locate the transaction-commit protocol adapter, its contracts, commit service and existing end-to-end test coverage before choosing the disposable published-Host workflow.

**Request:** `{"workspace":{"alias":"dogfood-008-design-discovery"},"query":"TransactionCommit","kinds":["NamedType","Method"],"symbolsLimit":30,"scope":{"kind":"Solution"}}`

**Outcome:** Succeeded with 21 results. Material results included `TransactionCommitTool`, `TransactionCommitRequest`, `TransactionCommitService`, the disk-persistence integration test and an existing published-Host acceptance test that renames and commits against a disposable fixture.

### 199. `get-code-context`

**Activity:** DOGFOOD-008 design discovery.

**Purpose:** Inspect the existing published-Host acceptance workflow around its controlled rename and physical transaction commit.

**Request:** `{"workspace":{"alias":"dogfood-008-design-discovery"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.AcceptanceTest/WorkspaceWorkflowIntegrationTests.cs"},"range":{"start":8216,"length":96}}},"beforeLines":10,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"diagnosticsLimit":1}`

**Outcome:** Succeeded and showed the complete workflow: open a copied `Sample.csproj`, start at revision 0, rename `T:Sample.Class1` to `RenamedClass`, verify the one-document preview, commit with the rename result's snapshot, assert the exact file content and query the promoted Workspace state for `Sample.RenamedClass`.

### 200. `workspace-close`

**Activity:** DOGFOOD-008 design discovery.

**Purpose:** Close the read-only repository Workspace after the controlled-commit workflow and its existing coverage had been traced.

**Request:** `{"workspace":{"alias":"dogfood-008-design-discovery"}}`

**Outcome:** Succeeded and closed the repository at its unchanged epoch-2 snapshot with no transaction.

### 201. `workspace-open`

**Activity:** DOGFOOD-008 revised design discovery.

**Purpose:** Reopen the trusted repository read-only to assess the user's proposed real-repository and Scenario Runner alternatives.

**Request:** `{"alias":"dogfood-008-scenario-discovery","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,638 documents at Workspace epoch 3. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning; no transaction was started.

### 202. `search-symbols`

**Activity:** DOGFOOD-008 revised design discovery.

**Purpose:** Locate the Scenario Runner's durable-commit orchestration and reporting types before assessing whether a new large-project scenario is needed.

**Request:** `{"workspace":{"alias":"dogfood-008-scenario-discovery"},"query":"DurableCommit","kinds":["NamedType","Method"],"symbolsLimit":40,"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.ScenarioRunner"}]}}`

**Outcome:** Succeeded with 12 results, including `MeasureDurableCommitsAsync`, `RunDurableCommitIterationAsync`, `DurableCommitRunner` and the commit measurement models. This confirmed that durable commits are already a first-class Scenario Runner workflow rather than a missing scenario family.

### 203. `get-code-context`

**Activity:** DOGFOOD-008 revised design discovery.

**Purpose:** Inspect the complete durable-commit iteration and its cleanup boundary.

**Request:** `{"workspace":{"alias":"dogfood-008-scenario-discovery"},"location":{"span":{"document":{"path":"tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs"},"range":{"start":41443,"length":30}}},"beforeLines":15,"afterLines":150,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3,"diagnosticsLimit":1}`

**Outcome:** Failed with `InvalidRequest` because `afterLines: 150` exceeded the published bound. No Workspace or transaction state changed; the request was corrected for retry.

### 204. `get-code-context`

**Activity:** DOGFOOD-008 revised design discovery.

**Purpose:** Retry inspection of the durable-commit iteration with the supported context bound.

**Request:** `{"workspace":{"alias":"dogfood-008-scenario-discovery"},"location":{"span":{"document":{"path":"tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs"},"range":{"start":41443,"length":30}}},"beforeLines":15,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3,"diagnosticsLimit":1}`

**Outcome:** Succeeded and confirmed that each durable-commit iteration starts a published Host, opens the pinned repository, stages and previews the mutation, commits it, attempts rollback on a pre-commit failure, closes the Workspace and Host, captures actual Git changes, restores them with cancellation disabled, and combines workload, cleanup, restoration and validation failures rather than skipping later cleanup.

### 205. `workspace-close`

**Activity:** DOGFOOD-008 revised design discovery.

**Purpose:** Close the read-only repository Workspace after assessing the existing large-repository durable-commit workflow.

**Request:** `{"workspace":{"alias":"dogfood-008-scenario-discovery"}}`

**Outcome:** Succeeded and closed the repository at its unchanged epoch-3 snapshot with no transaction.

## DOGFOOD-009 — Existing-coverage validation

### 206. `workspace-list`

**Activity:** DOGFOOD-009 issue validation.

**Purpose:** Confirm that no published dogfood Workspace remained loaded before auditing existing Code Action and Fix All coverage.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 207. `workspace-open`

**Activity:** DOGFOOD-009 issue validation.

**Purpose:** Open the trusted repository read-only in the published dogfood Host so existing Code Action and Fix All tests, scenarios and orchestration can be traced with Roslyn-backed queries.

**Request:** `{"alias":"dogfood-009-coverage-audit","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,638 documents at Workspace epoch 4. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning; no transaction was started.

### 208. `search-symbols`

**Activity:** DOGFOOD-009 issue validation.

**Purpose:** Locate existing Fix All production paths and test coverage before deciding whether the workflow lacks repository validation.

**Request:** `{"workspace":{"alias":"dogfood-009-coverage-audit"},"query":"FixAll","kinds":["NamedType","Method"],"symbolsLimit":100,"scope":{"kind":"Solution"}}`

**Outcome:** Succeeded with 80 results. The results included extensive `PrepareFixAllToolTests`, integration support, schema tests, production creation and replay components, and the published-Host acceptance test `GIVEN_BuiltInIdeCodeFix_WHEN_PreparingAndStagingFixAll_THEN_ShouldRemainReadOnlyUntilStandardStaging`.

### 209. `get-code-context`

**Activity:** DOGFOOD-009 issue validation.

**Purpose:** Inspect the published-Host acceptance workflow that combines Code Fix discovery, Fix All preparation and standard staging.

**Request:** `{"workspace":{"alias":"dogfood-009-coverage-audit"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.AcceptanceTest/CodeActionWorkflowIntegrationTests.cs"},"range":{"start":17756,"length":100}}},"beforeLines":20,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3,"diagnosticsLimit":1}`

**Outcome:** Succeeded and showed the acceptance test starting a published Host and transaction, discovering a real built-in IDE0003 Code Fix with an advertised document Fix All scope, preparing the bounded Fix All without changing disk or the transaction preview, and then beginning standard staging of the prepared action.

### 210. `workspace-close`

**Activity:** DOGFOOD-009 issue validation.

**Purpose:** Close the read-only repository Workspace after the existing Code Action and Fix All coverage audit.

**Request:** `{"workspace":{"alias":"dogfood-009-coverage-audit"}}`

**Outcome:** Succeeded and closed the repository at its unchanged epoch-4 snapshot with no transaction.

### 211. `workspace-open`

**Activity:** DOGFOOD-009 fixture discovery.

**Purpose:** Open the checked-in InspectionSample project directly from this repository to verify that it can provide the deterministic IDE0003 Fix All target used by acceptance coverage.

**Request:** `{"alias":"dogfood-009-fixture-discovery","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp/dogfood-009"}}`

**Outcome:** Failed with `WorkspaceMsBuildPropertiesInvalid` because the isolated absolute artifacts directory did not yet exist. No Workspace was opened; the directory was created outside the repository before retrying.

### 212. `workspace-open`

**Activity:** DOGFOOD-009 fixture discovery.

**Purpose:** Retry opening the checked-in InspectionSample project after creating its isolated artifacts directory outside the repository.

**Request:** `{"alias":"dogfood-009-fixture-discovery","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp/dogfood-009"}}`

**Outcome:** Succeeded with one project and 29 documents at Workspace epoch 5. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning; no transaction was started.

### 213. `list-code-actions`

**Activity:** DOGFOOD-009 fixture discovery.

**Purpose:** Verify read-only that the checked-in InspectionSample provides a deterministic real Code Fix with Fix All support before proposing it for live client validation.

**Request:** `{"workspace":{"alias":"dogfood-009-fixture-discovery"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":5,"snapshotId":"<snapshot-id>","transactionRevision":null},"kinds":1,"diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Succeeded with exactly one action, `Remove 'this' qualification`, for IDE0003 at line 9. The action advertised document, project and solution Fix All scopes (`0`, `1`, `2`), confirming that the checked-in repository asset is a deterministic target for both single-action and Fix All dogfood validation.

### 214. `workspace-close`

**Activity:** DOGFOOD-009 fixture discovery.

**Purpose:** Close the checked-in InspectionSample Workspace after confirming its deterministic Code Fix and Fix All target.

**Request:** `{"workspace":{"alias":"dogfood-009-fixture-discovery"}}`

**Outcome:** Succeeded and closed the project at its unchanged epoch-5 snapshot with no transaction.

## DOGFOOD-009 — Live dogfood-client validation

### 215. `workspace-open`

**Activity:** DOGFOOD-009 checked-in-project validation.

**Purpose:** Open the checked-in InspectionSample project directly for the approved rollback-only Code Action and Fix All workflows.

**Request:** `{"alias":"dogfood-009-live-validation","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp/dogfood-009"}}`

**Outcome:** Succeeded with one project and 29 documents at Workspace epoch 6. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 216. `transaction-start`

**Activity:** DOGFOOD-009 direct Code Action validation.

**Purpose:** Start the first rollback-only transaction for direct Code Action staging.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"}}`

**Outcome:** Succeeded at revision 0 with mutation and rollback enabled and commit disabled until a change was staged.

### 217. `list-code-actions`

**Activity:** DOGFOOD-009 direct Code Action validation.

**Purpose:** Rediscover the deterministic IDE0003 action inside the active transaction.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":6,"snapshotId":"<snapshot-id>","transactionRevision":0},"kinds":1,"diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Succeeded with exactly one `Remove 'this' qualification` action and document, project and solution Fix All scopes.

### 218. `stage-code-action`

**Activity:** DOGFOOD-009 direct Code Action validation.

**Purpose:** Stage the selected single Code Fix through its opaque action reference.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"actionId":"<action-id>","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":6,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with summary `Remove 'this' qualification`, transaction revision 1 and a new staged snapshot.

### 219. `transaction-preview`

**Activity:** DOGFOOD-009 direct Code Action validation.

**Purpose:** Inspect the staged single-action change before rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"includeDiff":true,"contextLines":3,"document":{"path":"SimplifyThisOrMe.cs"}}`

**Outcome:** Succeeded with one modified document and one changed line, replacing `var value = this.x;` with `var value = x;`. The diff was not truncated; an external hash check confirmed that disk remained unchanged.

### 220. `transaction-rollback`

**Activity:** DOGFOOD-009 direct Code Action validation.

**Purpose:** Discard the staged single Code Fix without writing the repository.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"}}`

**Outcome:** Succeeded, returned lifecycle state `Ready`, cleared the transaction and restored the original committed snapshot identity.

### 221. `transaction-start`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Start a fresh rollback-only transaction so the Fix All workflow uses newly discovered opaque references.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"}}`

**Outcome:** Succeeded at revision 0 against the same original committed snapshot.

### 222. `list-code-actions`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Rediscover the IDE0003 Code Fix and obtain a fresh originating action reference.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":6,"snapshotId":"<snapshot-id>","transactionRevision":0},"kinds":1,"diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Succeeded with the same single action and Fix All scopes under a new opaque action reference.

### 223. `prepare-fix-all`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Prepare a bounded document-scope Fix All without staging it.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"actionId":"<action-id>","scope":0,"maxChanges":10,"affectedDocumentsLimit":10,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":6,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with a prepared action reference and exactly one affected document, `SimplifyThisOrMe.cs`. The Workspace remained at transaction revision 0 and the file hash remained unchanged.

### 224. `transaction-preview`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Confirm that Fix All preparation had not staged a change.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"includeDiff":true,"contextLines":3}`

**Outcome:** Failed with `InvalidRequest` because `includeDiff: true` requires a document selector. No state changed; the request was corrected for retry.

### 225. `transaction-preview`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Retry the read-only post-preparation preview without requesting a document diff.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"includeDiff":false,"contextLines":3}`

**Outcome:** Succeeded at revision 0 with no changed documents, no diff and commit still disabled, proving that preparation had remained read-only.

### 226. `stage-code-action`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Stage the prepared Fix All through its new opaque reference.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"actionId":"<prepared-action-id>","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":6,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with summary `Fix all: Remove 'this' qualification`, transaction revision 1 and a new staged snapshot.

### 227. `transaction-preview`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Inspect the staged prepared Fix All before rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"includeDiff":true,"contextLines":3,"document":{"path":"SimplifyThisOrMe.cs"}}`

**Outcome:** Succeeded with the same precise one-document, one-line IDE0003 change as direct staging; the diff was not truncated.

### 228. `transaction-rollback`

**Activity:** DOGFOOD-009 prepared Fix All validation.

**Purpose:** Discard the staged prepared Fix All without writing the repository.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"}}`

**Outcome:** Succeeded, returned lifecycle state `Ready`, cleared the transaction and restored the original committed snapshot identity.

### 229. `workspace-status`

**Activity:** DOGFOOD-009 checked-in-project validation.

**Purpose:** Verify final lifecycle, transaction and snapshot state after both rollbacks.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, `transaction: null`, no reload requirement and the original committed snapshot. External checks confirmed the original SHA-256 file hash and unchanged Git status apart from the intentional worklist and usage-log edits.

### 230. `workspace-close`

**Activity:** DOGFOOD-009 checked-in-project validation.

**Purpose:** Close the InspectionSample Workspace after both rollback-only workflows completed.

**Request:** `{"workspace":{"alias":"dogfood-009-live-validation"}}`

**Outcome:** Succeeded and closed the project at its unchanged epoch-6 committed snapshot with no transaction.

## DOGFOOD-009 — Main-codebase validation

### 231. `workspace-open`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Open the main solution to find and exercise a real refactoring directly against production or tool code after the checked-in-project workflows passed.

**Request:** `{"alias":"dogfood-009-main-codebase","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"/tmp/artifacts/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,638 documents at Workspace epoch 7. The only load diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 232. `list-code-actions`

**Activity:** DOGFOOD-009 main-codebase candidate discovery.

**Purpose:** Find a deterministic refactoring at `ScenarioHost.GetSnapshot` before starting a mutation transaction.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"},"document":{"path":"tools/Roslyn.Workbench.Mcp.ScenarioRunner/Hosting/ScenarioHost.cs"},"range":{"start":1448,"length":0},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":7,"snapshotId":"<snapshot-id>","transactionRevision":null},"kinds":3,"limit":20}`

**Outcome:** Succeeded with exactly one action, `Use expression body for method`, at line 41.

### 233. `transaction-start`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Start a rollback-only transaction for the selected main-codebase refactoring.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"}}`

**Outcome:** Succeeded at revision 0 against the main solution's committed snapshot.

### 234. `list-code-actions`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Rediscover the selected refactoring inside the active transaction and obtain a current opaque reference.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"},"document":{"path":"tools/Roslyn.Workbench.Mcp.ScenarioRunner/Hosting/ScenarioHost.cs"},"range":{"start":1448,"length":0},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":7,"snapshotId":"<snapshot-id>","transactionRevision":0},"kinds":3,"limit":20}`

**Outcome:** Succeeded with the same single `Use expression body for method` refactoring under a new action reference.

### 235. `stage-code-action`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Stage the selected main-codebase refactoring through its opaque reference.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"},"actionId":"<action-id>","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":7,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with summary `Use expression body for method`, transaction revision 1 and a new staged snapshot.

### 236. `transaction-preview`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Inspect the selected refactoring without writing it to the main repository.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"},"includeDiff":true,"contextLines":3,"document":{"path":"tools/Roslyn.Workbench.Mcp.ScenarioRunner/Hosting/ScenarioHost.cs"}}`

**Outcome:** Succeeded with one modified document. The preview replaced the four-line block-bodied `GetSnapshot` method with its single-line expression-bodied equivalent; the diff was not truncated and an external hash check confirmed that disk remained unchanged.

### 237. `transaction-rollback`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Discard the staged main-codebase refactoring without writing it.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"}}`

**Outcome:** Succeeded, returned state `Ready`, cleared the transaction and restored the original committed snapshot identity.

### 238. `workspace-status`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Verify final main-solution lifecycle and transaction state after rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, `transaction: null`, no reload requirement and the original committed snapshot.

### 239. `workspace-close`

**Activity:** DOGFOOD-009 main-codebase validation.

**Purpose:** Close the main solution after successful rollback-only validation.

**Request:** `{"workspace":{"alias":"dogfood-009-main-codebase"}}`

**Outcome:** Succeeded and closed the solution at its unchanged epoch-7 committed snapshot with no transaction.

### 240. `workspace-list`

**Activity:** DOGFOOD-009 final validation.

**Purpose:** Confirm that all Workspaces used by the checked-in-project and main-codebase validation runs were closed and no transaction owner remained.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

## DOGFOOD-009 — Published string-enum validation

Immediately after restart, the configured dogfood processes were running but the dogfood namespace was absent from the task's callable tool registry. The exact promoted `current` executable was exercised directly through stdio MCP before the registry was rechecked. That run is retained as diagnostic transport evidence only and is not accepted as the required Codex dogfood confirmation. The normal Codex MCP tools appeared later in the same task, so the complete workflow was repeated through them below.

### 241. `initialize`

**Activity:** DOGFOOD-009 published string-enum validation, first attempt.

**Purpose:** Initialise an MCP session against the newly promoted published executable.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-published-probe","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18`, the expected server identity and tool-list capability.

### 242. `tools/list`

**Activity:** DOGFOOD-009 published string-enum validation, first attempt.

**Purpose:** Inspect the newly published Code Action input schemas before exercising them.

**Request:** `{}`

**Outcome:** Succeeded. `list-code-actions.kinds` was a string enum with `CodeFixes`, `Refactorings` and `All`; `prepare-fix-all.scope` was a string enum with `Document`, `Project` and `Solution`.

### 243. `workspace-list`

**Activity:** DOGFOOD-009 published string-enum validation, first attempt.

**Purpose:** Confirm the published probe began without a loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 244. `workspace-open`

**Activity:** DOGFOOD-009 published string-enum validation, first attempt.

**Purpose:** Open the checked-in InspectionSample project for a rollback-only Code Action workflow.

**Request:** `{"alias":"dogfood-009-string-enum-validation","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"<temp-artifacts>/dogfood-009-string-enum"}}`

**Outcome:** Succeeded with one project and 29 documents. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 245. `transaction-start`

**Activity:** DOGFOOD-009 published string-enum validation, first attempt.

**Purpose:** Start the rollback-only transaction used to test the published Code Action binding.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"}}`

**Outcome:** Succeeded at transaction revision 0.

### 246. `list-code-actions`

**Activity:** DOGFOOD-009 published string-enum validation, first attempt.

**Purpose:** Exercise the new string-valued Code Action kind input using the initially assumed singular name.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0},"kinds":"CodeFix","diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Failed with `InvalidRequest` because `CodeFix` is not a member of `CodeActionKindSelection`; the published schema correctly advertises the plural value `CodeFixes`. This was a caller naming error, not numeric fallback.

### 247. `transaction-rollback`

**Activity:** DOGFOOD-009 published string-enum validation, first-attempt cleanup.

**Purpose:** Discard the empty transaction after the rejected request.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"}}`

**Outcome:** Issued by failure cleanup. The next independent session began with no loaded Workspace or transaction owner, and the checked-in file hash remained unchanged.

### 248. `workspace-close`

**Activity:** DOGFOOD-009 published string-enum validation, first-attempt cleanup.

**Purpose:** Close the InspectionSample Workspace after the rejected request.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"}}`

**Outcome:** Issued by failure cleanup. The following session confirmed that no Workspace remained loaded.

### 249. `initialize`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Initialise a fresh session for the corrected end-to-end workflow.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-published-probe","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18` and the expected published server identity.

### 250. `tools/list`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Reconfirm the Code Action string-enum schemas on the fresh session.

**Request:** `{}`

**Outcome:** Succeeded with the same exact `CodeFixes`/`Refactorings`/`All` and `Document`/`Project`/`Solution` string enums.

### 251. `workspace-list`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Verify that first-attempt cleanup left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 252. `workspace-open`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Open the checked-in InspectionSample project for the corrected rollback-only workflow.

**Request:** `{"alias":"dogfood-009-string-enum-validation","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"<temp-artifacts>/dogfood-009-string-enum"}}`

**Outcome:** Succeeded with one project and 29 documents at Workspace epoch 1. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 253. `transaction-start`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Start a fresh rollback-only transaction for prepared Fix All.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"}}`

**Outcome:** Succeeded at revision 0 with mutation and rollback enabled.

### 254. `list-code-actions`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Discover the deterministic IDE0003 Code Fix using the published string enum value.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0},"kinds":"CodeFixes","diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Succeeded with the single `Remove 'this' qualification` action. Its response kind was the string `CodeFix`, and its Fix All scopes were the strings `Document`, `Project` and `Solution`.

### 255. `prepare-fix-all`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Prepare a bounded document Fix All using the published string scope.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"actionId":"<action-id>","scope":"Document","maxChanges":10,"affectedDocumentsLimit":10,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with response scope `Document`, one affected document and no truncation. The response remained at transaction revision 0.

### 256. `transaction-preview`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Confirm that Fix All preparation had not staged a mutation.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"includeDiff":false,"contextLines":3}`

**Outcome:** Succeeded at revision 0 with no changed documents and commit disabled.

### 257. `stage-code-action`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Stage the prepared Fix All through its opaque action reference.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"actionId":"<prepared-action-id>","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with summary `Fix all: Remove 'this' qualification` and transaction revision 1.

### 258. `transaction-preview`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Inspect the staged Fix All before rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"includeDiff":true,"contextLines":3,"document":{"path":"SimplifyThisOrMe.cs"}}`

**Outcome:** Succeeded with one modified document and one changed line, replacing `var value = this.x;` with `var value = x;`. The diff was not truncated.

### 259. `transaction-rollback`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Discard the staged prepared Fix All without writing the repository.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"}}`

**Outcome:** Succeeded, returned state `Ready`, cleared the transaction and restored the original snapshot identity.

### 260. `workspace-status`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Verify final lifecycle and transaction state after rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, `transaction: null` and no reload requirement.

### 261. `workspace-close`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Close the InspectionSample Workspace after successful validation.

**Request:** `{"workspace":{"alias":"dogfood-009-string-enum-validation"}}`

**Outcome:** Succeeded and closed the project at its unchanged committed snapshot.

### 262. `workspace-list`

**Activity:** DOGFOOD-009 published string-enum validation, corrected run.

**Purpose:** Confirm that the validation left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner. An external SHA-256 comparison confirmed that `SimplifyThisOrMe.cs` remained byte-for-byte unchanged.

## DOGFOOD-009 — Normal Codex MCP string-enum validation

Before invoking a tool, Codex's actual generated declarations were inspected from the task's callable registry. `list-code-actions.kinds` projected as `"CodeFixes" | "Refactorings" | "All"`, and `prepare-fix-all.scope` projected as `"Document" | "Project" | "Solution"`. The following requests were then sent through the configured `mcp__roslyn_workbench_dogfood__*` tools rather than a separately launched client.

### 263. `workspace-list`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Confirm that the configured dogfood server had no loaded Workspace before the normal-client workflow.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 264. `workspace-open`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Open the checked-in InspectionSample project through the configured dogfood tool.

**Request:** `{"alias":"dogfood-009-codex-enum-validation","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"<temp-artifacts>/dogfood-009-codex-enum"}}`

**Outcome:** Failed with `WorkspaceMsBuildPropertiesInvalid` because the isolated absolute artifacts directory did not yet exist. The directory was created outside the repository before retrying.

### 265. `workspace-open`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Retry after creating the isolated artifacts directory.

**Request:** `{"alias":"dogfood-009-codex-enum-validation","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"<temp-artifacts>/dogfood-009-codex-enum"}}`

**Outcome:** Succeeded with one project and 29 documents at Workspace epoch 1. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 266. `transaction-start`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Start a rollback-only transaction for the prepared Fix All workflow.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"}}`

**Outcome:** Succeeded at revision 0 with mutation and rollback enabled.

### 267. `list-code-actions`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Exercise the generated `kinds` string union and discover the deterministic IDE0003 Code Fix.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0},"kinds":"CodeFixes","diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Succeeded with the single `Remove 'this' qualification` action. Codex received response kind `CodeFix` and Fix All scopes `Document`, `Project` and `Solution` as strings.

### 268. `prepare-fix-all`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Exercise the generated `scope` string union by preparing a bounded document Fix All.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"},"actionId":"<action-id>","scope":"Document","maxChanges":10,"affectedDocumentsLimit":10,"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with response scope `Document`, exactly one affected document and no truncation. Preparation retained transaction revision 0.

### 269. `transaction-preview`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Confirm that preparation remained read-only before staging.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"},"includeDiff":false,"contextLines":3}`

**Outcome:** Succeeded at revision 0 with no changed documents and commit disabled.

### 270. `stage-code-action`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Stage the prepared Fix All through its opaque reference.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"},"actionId":"<prepared-action-id>","expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":0}}`

**Outcome:** Succeeded with summary `Fix all: Remove 'this' qualification` and transaction revision 1.

### 271. `transaction-preview`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Inspect the staged Fix All through the configured dogfood tool before rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"},"includeDiff":true,"contextLines":3,"document":{"path":"SimplifyThisOrMe.cs"}}`

**Outcome:** Succeeded with one modified document and one changed line, replacing `var value = this.x;` with `var value = x;`. The diff was not truncated.

### 272. `transaction-rollback`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Discard the staged Fix All without writing the repository.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"}}`

**Outcome:** Succeeded, returned state `Ready`, cleared the transaction and restored the original snapshot identity.

### 273. `workspace-status`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Verify lifecycle and transaction state after rollback.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"},"detail":"Full"}`

**Outcome:** Succeeded with state `Ready`, `transaction: null` and no reload requirement.

### 274. `workspace-close`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Close the checked-in project after the normal Codex workflow.

**Request:** `{"workspace":{"alias":"dogfood-009-codex-enum-validation"}}`

**Outcome:** Succeeded and closed the project at its unchanged committed snapshot.

### 275. `workspace-list`

**Activity:** DOGFOOD-009 normal Codex MCP validation.

**Purpose:** Confirm that the configured dogfood server retained no Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner. The original SHA-256 hash of `SimplifyThisOrMe.cs` was unchanged and Git reported no change to the file.

## DOGFOOD-009 — Committed-build confirmation

The committed `HEAD` (`12aa4ad`) was published to a fresh versioned candidate and promoted to `current`. After restart, both configured Host processes resolved to that candidate. Codex's normal callable declarations continued to project `list-code-actions.kinds` as `"CodeFixes" | "Refactorings" | "All"` and `prepare-fix-all.scope` as `"Document" | "Project" | "Solution"`.

### 276. `server-status`

**Activity:** DOGFOOD-009 committed-build confirmation.

**Purpose:** Smoke-test the restarted committed Host and its Code Action composition through the normal Codex MCP tool.

**Request:** `{"detail":"Minimal"}`

**Outcome:** Succeeded with server version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 81 refactoring providers, 169 code-fix providers and 56 published tools.

### 277. `workspace-list`

**Activity:** DOGFOOD-009 committed-build confirmation.

**Purpose:** Confirm that the restarted committed Host had no loaded Workspace before the read-only smoke test.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 278. `workspace-open`

**Activity:** DOGFOOD-009 committed-build confirmation.

**Purpose:** Open the checked-in InspectionSample project read-only through the normal Codex MCP tool.

**Request:** `{"alias":"dogfood-009-committed-smoke","path":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj","workspaceRoot":"<repo>/test/TestAssets/Workspaces/InspectionSample/Base","msBuildProperties":{"artifactsPath":"<temp-artifacts>/dogfood-009-committed-smoke"}}`

**Outcome:** Succeeded with one project and 29 documents at Workspace epoch 1. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 279. `list-code-actions`

**Activity:** DOGFOOD-009 committed-build confirmation.

**Purpose:** Confirm that the committed Host accepts the string-valued Code Action kind and returns string-valued response enums through Codex's normal generated declaration.

**Request:** `{"workspace":{"alias":"dogfood-009-committed-smoke"},"document":{"path":"SimplifyThisOrMe.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"kinds":"CodeFixes","diagnosticIds":["IDE0003"],"limit":10}`

**Outcome:** Succeeded with the single `Remove 'this' qualification` action. Its response kind was `CodeFix`, and its Fix All scopes were `Document`, `Project` and `Solution`, all represented as strings.

### 280. `workspace-close`

**Activity:** DOGFOOD-009 committed-build confirmation.

**Purpose:** Close the read-only InspectionSample Workspace after the committed-build smoke test.

**Request:** `{"workspace":{"alias":"dogfood-009-committed-smoke"}}`

**Outcome:** Succeeded and closed the project at its unchanged snapshot with no transaction.

### 281. `workspace-list`

**Activity:** DOGFOOD-009 committed-build confirmation.

**Purpose:** Confirm that the smoke test left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

## DOGFOOD-010 — Existing-coverage validation and sweep design

### 282. `workspace-open`

**Activity:** DOGFOOD-010 existing-coverage validation.

**Purpose:** Open the main solution read-only so the existing bundled-query integration coverage could be inspected through the published dogfood Host.

**Request:** `{"alias":"dogfood-010-coverage-audit","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"<temp-artifacts>/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,639 documents at Workspace epoch 2. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 283. `search-symbols`

**Activity:** DOGFOOD-010 existing-coverage validation.

**Purpose:** Locate the real-workspace semantic integration suite used to validate representative bundled query tools.

**Request:** `{"workspace":{"alias":"dogfood-010-coverage-audit"},"query":"SemanticInspectionIntegrationTests","kinds":["NamedType","Method"],"symbolsLimit":20,"scope":{"kind":"Solution"}}`

**Outcome:** Succeeded with the single `SemanticInspectionIntegrationTests` type and its canonical source selector.

### 284. `get-code-context`

**Activity:** DOGFOOD-010 existing-coverage validation.

**Purpose:** Inspect the semantic integration suite around its bundled-query executions.

**Request:** `{"workspace":{"alias":"dogfood-010-coverage-audit"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest/SemanticInspectionIntegrationTests.cs"},"range":{"start":73,"length":34}}},"beforeLines":2,"afterLines":180,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3,"diagnosticsLimit":1}`

**Outcome:** Failed with `InvalidRequest` because `afterLines: 180` exceeds the published bound. No state changed; the request was corrected to the maximum of 100.

### 285. `get-code-context`

**Activity:** DOGFOOD-010 existing-coverage validation.

**Purpose:** Retry the semantic-integration inspection within the published context bound.

**Request:** `{"workspace":{"alias":"dogfood-010-coverage-audit"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest/SemanticInspectionIntegrationTests.cs"},"range":{"start":73,"length":34}}},"beforeLines":2,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3,"diagnosticsLimit":1}`

**Outcome:** Succeeded and showed real-Workspace execution of `get-diagnostics`, `analyze-async`, `get-operation-tree`, control-flow analysis, data-flow analysis and control-flow-graph projection. The response was bounded and reported additional enclosing symbols through `hasMore`.

### 286. `workspace-close`

**Activity:** DOGFOOD-010 existing-coverage validation.

**Purpose:** Close the read-only main-solution Workspace after validating the surviving DOGFOOD-010 gap.

**Request:** `{"workspace":{"alias":"dogfood-010-coverage-audit"}}`

**Outcome:** Succeeded and closed the solution at its unchanged epoch-2 snapshot with no transaction.

## DOGFOOD-010 — Live representative query-surface sweep

### 287. `workspace-open`

**Activity:** DOGFOOD-010 live client-usability sweep.

**Purpose:** Open the main solution once for the approved read-only query sweep.

**Request:** `{"alias":"dogfood-010-live-sweep","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"<temp-artifacts>/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,639 documents at Workspace epoch 3. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 288. `get-solution-structure`

**Activity:** DOGFOOD-010 Workspace and project shape.

**Purpose:** Exercise bounded solution, project, reference and document projection.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"includeDocuments":true,"projectsLimit":2,"documentsPerProjectLimit":2,"projectReferencesPerProjectLimit":2,"foldersLimit":2}`

**Outcome:** Succeeded with two of five folders and two of 30 projects. Both selected projects returned generated `obj` documents as their first two documents, so the low limit hid ordinary source documents.

### 289. `get-project-details`

**Activity:** DOGFOOD-010 Workspace and project shape.

**Purpose:** Inspect one real project's bounded documents, references, metadata and analyzers.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"project":{"name":"Roslyn.Workbench.Mcp.Workspace"},"includeDocuments":true,"documentsLimit":2,"projectReferencesLimit":2,"metadataReferencesLimit":2,"analyzersLimit":2}`

**Outcome:** Succeeded with compilation options, one project reference, two of 225 metadata references and two of 12 analyzers. The two bounded documents were generated `obj` files rather than source files.

### 290. `get-document-options`

**Activity:** DOGFOOD-010 Workspace and project shape.

**Purpose:** Inspect language, parse and analyzer-config options for a real source document.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"document":{"path":"src/Roslyn.Workbench.Mcp/Tools/ToolRequestBinder.cs"}}`

**Outcome:** Succeeded with C# 14, enabled nullable analysis and the effective parse and editor-config options. The complete analyzer-config option map made the otherwise accurate response large.

### 291. `get-api-surface`

**Activity:** DOGFOOD-010 Workspace and project shape.

**Purpose:** Confirm bounded public API projection for the abstractions project.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Abstractions"}},"minimumAccessibility":"Public","includeObsolete":false,"symbolsLimit":2}`

**Outcome:** Succeeded with the first two of 219 public symbols and `hasMore: true`.

### 292. `analyze-async`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Exercise bounded async diagnostics over one production document.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Document","document":{"path":"src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs"}},"findingsLimit":2}`

**Outcome:** Succeeded with no findings.

### 293. `analyze-disposables`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Exercise bounded disposable analysis over one production document.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Document","document":{"path":"src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs"}},"findingsLimit":2}`

**Outcome:** Succeeded with no findings.

### 294. `analyze-nullability`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Exercise bounded nullable-flow diagnostics over one production document.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Document","document":{"path":"src/Roslyn.Workbench.Mcp/Tools/ToolRequestBinder.cs"}},"findingsLimit":2}`

**Outcome:** Succeeded with no findings.

### 295. `find-dependency-cycles`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Probe the dependency-cycle contract with deliberately low graph limits.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"granularity":"Project","nodesLimit":2,"edgesLimit":2,"cyclesLimit":2}`

**Outcome:** Failed with actionable `AnalysisLimitExceeded`: the node limit is an analysis guard rather than an output-only bound. The response advised narrowing scope or increasing the limit, and the request was retried with two projects.

### 296. `find-duplicate-code`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Exercise bounded duplicate-code analysis over one production document.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Document","document":{"path":"src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs"}},"minimumStatements":3,"groupsLimit":2,"occurrencesPerGroupLimit":2}`

**Outcome:** Succeeded with no duplicate groups.

### 297. `find-unused-symbols`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Exercise bounded unused-symbol analysis on the Host project while excluding generated code.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp"}},"includeInternal":true,"excludeGenerated":true,"candidatesLimit":2}`

**Outcome:** Succeeded with no candidates.

### 298. `get-dependency-graph`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Exercise a bounded type-level dependency graph over the Host project.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp"}},"granularity":"Type","nodesLimit":2,"edgesLimit":2}`

**Outcome:** Succeeded with two `StartupConfiguration` nodes, one edge and additional nodes reported through `hasMore`.

### 299. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate the generic MCP tool base for hierarchy and override queries.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"McpServerToolBase","kinds":["NamedType","Method"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production generic base and its test type.

### 300. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate `CommitRecoveryStore`, its interface and tests for dependency and impact queries.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"CommitRecoveryStore","symbolsLimit":10}`

**Outcome:** Succeeded with the implementation, interface and test types.

### 301. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate a partial production type.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"FileStreamWorkspaceFileLockProvider","symbolsLimit":10}`

**Outcome:** Succeeded with the partial production class.

### 302. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate a method with several direct callees.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"SerializeJson","symbolsLimit":10}`

**Outcome:** Succeeded with the private generic `CommitRecoveryStore.SerializeJson` method and its canonical selector.

### 303. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate concrete overrides of the generic MCP tool base method.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"ExecuteCoreAsync","kinds":["Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with 20 of 54 matching methods and `hasMore: true`.

### 304. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate overload candidates for `WorkspaceResolver.ResolveDocument`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"ResolveDocument","symbolsLimit":10}`

**Outcome:** Succeeded with seven of 45 substring matches, including the public and private `WorkspaceResolver` overloads.

### 305. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate a source attribute with inspectable metadata.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"RequiresExactlyOneAttribute","symbolsLimit":10}`

**Outcome:** Succeeded with the production attribute and its test type.

### 306. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Locate the generic MCP base method and its overrides.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"InvokeBoundRequestAsync","kinds":["Method"],"symbolsLimit":10}`

**Outcome:** Succeeded with nine declarations, including the generic base and concrete overrides.

### 307. `search-symbols`

**Activity:** DOGFOOD-010 symbol-target discovery.

**Purpose:** Narrow the overload lookup to `WorkspaceResolver.ResolveDocument`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"WorkspaceResolver.ResolveDocument","kinds":["Method"],"symbolsLimit":10}`

**Outcome:** Succeeded with seven matches, including the exact public and private overloads.

### 308. `find-callees`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return bounded direct callees for `CommitRecoveryStore.SerializeJson`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"<SerializeJson documentation ID>","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"includeIndirect":false,"maxDepth":1,"calleesLimit":2}`

**Outcome:** Succeeded with two of three direct callees, `InvalidDataException` construction and `Encoding.GetByteCount`, and `hasMore: true`.

### 309. `find-callers`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return bounded callers and source context for `CommitRecoveryStore.SerializeJson`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"<SerializeJson documentation ID>","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"includeContext":true,"callersLimit":2,"callSitesPerCallerLimit":2}`

**Outcome:** Succeeded with the two callers, `WriteManifestAsync` and `WriteStatusAsync`, including bounded call-site context.

### 310. `find-derived-types`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Find direct concrete tools derived from the generic `McpServerToolBase<TRequest>`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Tools.McpServerToolBase\u00601","project":{"name":"Roslyn.Workbench.Mcp"}},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp"}},"maxDepth":1,"derivedTypesLimit":2}`

**Outcome:** Succeeded but returned no derived types, despite the repository containing concrete derived tools.

### 311. `find-implementations`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Find implementations of `ICommitRecoveryStore`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.ICommitRecoveryStore","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"scope":{"kind":"Solution"},"implementationsLimit":2}`

**Outcome:** Succeeded with the single `CommitRecoveryStore` implementation.

### 312. `find-overloads`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return bounded overloads for `WorkspaceResolver.ResolveDocument`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"<public ResolveDocument documentation ID>","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"overloadsLimit":2}`

**Outcome:** Succeeded with the public and private overloads and no additional results.

### 313. `find-overrides`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return concrete overrides of `McpServerToolBase<TRequest>.InvokeBoundRequestAsync`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"<generic InvokeBoundRequestAsync documentation ID>","project":{"name":"Roslyn.Workbench.Mcp"}},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp"}},"overridesLimit":2}`

**Outcome:** Succeeded with two of five concrete overrides and `hasMore: true`.

### 314. `get-partial-declarations`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return bounded declarations for the partial file-lock provider.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Locking.FileStreamWorkspaceFileLockProvider","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"declarationsLimit":2}`

**Outcome:** Succeeded with its generated `LibraryImports.g.cs` declaration followed by its source declaration.

### 315. `get-symbol-attributes`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Inspect bounded attribute metadata for `RequiresExactlyOneAttribute`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Contracts.Validation.RequiresExactlyOneAttribute","project":{"name":"Roslyn.Workbench.Mcp"}},"includeInherited":false,"attributesLimit":2}`

**Outcome:** Succeeded with the expected `AttributeUsage` metadata.

### 316. `get-symbol-dependencies`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return bounded source dependencies of `CommitRecoveryStore`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"includeAssemblies":false,"dependenciesLimit":2}`

**Outcome:** Succeeded with `ICommitRecoveryStore` and `object`.

### 317. `get-symbol-dependents`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Return bounded source dependents of `CommitRecoveryStore`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"scope":{"kind":"Solution"},"dependentsLimit":2}`

**Outcome:** Succeeded with two of eight dependents, including service registration and an integration-test reference, and `hasMore: true`.

### 318. `get-type-hierarchy`

**Activity:** DOGFOOD-010 symbol relationships.

**Purpose:** Project the base and derived hierarchy for generic `McpServerToolBase<TRequest>`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Tools.McpServerToolBase\u00601","project":{"name":"Roslyn.Workbench.Mcp"}},"includeDerived":true,"maxDepth":1,"baseTypesLimit":2,"interfacesLimit":2,"derivedTypesLimit":2}`

**Outcome:** Succeeded with the expected SDK base type and interface but no derived tools, matching the unexpected empty result from `find-derived-types`.

### 319. `find-dependency-cycles`

**Activity:** DOGFOOD-010 scope analysis and graphs.

**Purpose:** Retry cycle detection with a scope small enough for the deliberately low analysis limits.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp"},{"name":"Roslyn.Workbench.Mcp.Workspace"}]},"granularity":"Project","nodesLimit":2,"edgesLimit":2,"cyclesLimit":2}`

**Outcome:** Succeeded with no cycles.

### 320. `search-symbols`

**Activity:** DOGFOOD-010 hierarchy control discovery.

**Purpose:** Locate a non-generic base type to distinguish a generic hierarchy problem from a general tool failure.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"ResolvedFlowRegion","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the base type and two concrete derived types.

### 321. `find-derived-types`

**Activity:** DOGFOOD-010 hierarchy control.

**Purpose:** Retry derived-type discovery with the non-generic `ResolvedFlowRegion` base.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"<ResolvedFlowRegion documentation ID returned by call 320>","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"maxDepth":1,"derivedTypesLimit":2}`

**Outcome:** Succeeded with both concrete derived types.

### 322. `get-type-hierarchy`

**Activity:** DOGFOOD-010 hierarchy control.

**Purpose:** Retry complete hierarchy projection with the non-generic `ResolvedFlowRegion` base.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"<ResolvedFlowRegion documentation ID returned by call 320>","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"includeDerived":true,"maxDepth":1,"baseTypesLimit":2,"interfacesLimit":2,"derivedTypesLimit":2}`

**Outcome:** Succeeded with `object` as the base and both concrete derived types, isolating the earlier empty results to the generic-base case.

### 323. `get-operation-tree`

**Activity:** DOGFOOD-010 semantic impact.

**Purpose:** Inspect a bounded operation tree for a real serialization invocation.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs"},"selectedText":"JsonSerializer.Serialize(owner, _serializerOptions)"}},"maxDepth":1,"nodesLimit":2}`

**Outcome:** Succeeded with an invocation root, one argument child and `truncated: true`.

### 324. `get-change-impact`

**Activity:** DOGFOOD-010 semantic impact.

**Purpose:** Return bounded solution-wide references for `CommitRecoveryStore`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"scope":{"kind":"Solution"},"locationsLimit":2}`

**Outcome:** Succeeded with a reference count of nine, the first two locations and `hasMore: true`.

### 325. `get-test-impact`

**Activity:** DOGFOOD-010 semantic impact.

**Purpose:** Find tests likely affected by changes to `CommitRecoveryStore`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"testScope":{"kind":"Solution"},"includeReasons":true,"testsLimit":2}`

**Outcome:** Succeeded but returned no tests, despite the direct test references visible through `get-change-impact`.

### 326. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate the hierarchy service and its tests after the generic-base result.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"TypeHierarchyService","kinds":["NamedType","Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the interface, implementation and test type.

### 327. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate the test-impact tool and its tests after the empty result.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"GetTestImpactTool","kinds":["NamedType","Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the tool and its test type.

### 328. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Inspect the hierarchy service's derived-type and depth calculation.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Workspace/Hierarchy/TypeHierarchyService.cs"},"range":{"start":116,"length":20}}},"beforeLines":2,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5}`

**Outcome:** Succeeded and showed Roslyn's derived-class discovery followed by a parent-chain distance calculation using direct symbol equality. Existing tests inspected separately cover ordinary class and interface hierarchies but not generic bases.

### 329. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Inspect the test-impact tool's request routing.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetTestImpactTool.cs"},"range":{"start":189,"length":17}}},"beforeLines":2,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5}`

**Outcome:** Succeeded and confirmed that the resolved target and scoped documents are delegated directly to `DependencyAnalysisService.FindTestImpactsAsync`.

### 330. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate the dependency analysis implementation and tests.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"DependencyAnalysisService","kinds":["NamedType","Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the implementation, interface and test type.

### 331. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate the exact test-impact analysis method.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"FindTestImpactsAsync","kinds":["Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the interface and implementation methods.

### 332. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Inspect test candidate discovery and impact selection.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":1814,"length":20}}},"beforeLines":10,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed that source method declarations in test-named types are collected, then checked for a direct dependency on the normalized target or owning type.

### 333. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate the direct-dependency predicate used by test-impact analysis.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"HasTargetDependencyAsync","kinds":["Method"],"symbolsLimit":10}`

**Outcome:** Succeeded with the single private implementation method.

### 334. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Inspect how test method operations are compared with the target.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":8012,"length":24}}},"beforeLines":5,"afterLines":85,"includeEnclosingSymbols":false,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed return types, parameter types and descendant operations being compared through the service's symbol-matching helpers.

### 335. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate owning-type normalization used by the comparison.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"GetOwningTypeSymbol","kinds":["Method"],"symbolsLimit":10}`

**Outcome:** Succeeded with the single private helper.

### 336. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Inspect owning-type normalization for named types and members.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":40660,"length":19}}},"beforeLines":15,"afterLines":25,"includeEnclosingSymbols":false,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed named types and containing types normalized before comparison.

### 337. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Inspect the existing dependency-analysis unit fixture.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Test/Analysis/DependencyAnalysisServiceTests.cs"},"range":{"start":110,"length":30}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":false,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed the service tests; the focused test-impact cases use target and test code in the same in-memory compilation.

### 338. `search-symbols`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Locate the real `CommitRecoveryStoreTests` fixture.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"CommitRecoveryStoreTests","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the single Workspace test class.

### 339. `get-code-context`

**Activity:** DOGFOOD-010 finding investigation.

**Purpose:** Confirm that the real test fixture depends directly on `CommitRecoveryStore`.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Recovery/CommitRecoveryStoreTests.cs"},"range":{"start":306,"length":24}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":false,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed a `CommitRecoveryStore` field initialized by the test-class constructor and used by the fixture's tests.

### 340. `search-symbols`

**Activity:** DOGFOOD-010 response-shaping investigation.

**Purpose:** Locate the solution-structure projection after generated documents dominated its low-limit response.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"scope":{"kind":"Solution"},"query":"GetSolutionStructureTool","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production tool and its test type.

### 341. `get-code-context`

**Activity:** DOGFOOD-010 response-shaping investigation.

**Purpose:** Inspect project and document selection in the solution-structure tool.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":3,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetSolutionStructureTool.cs"},"range":{"start":284,"length":24}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":false,"includeDiagnostics":false}`

**Outcome:** Succeeded. Follow-on source inspection confirmed that document candidates are sorted only by normalized path before the low limit is applied, with no source-versus-generated preference.

### 342. `get-test-impact`

**Activity:** DOGFOOD-010 finding control.

**Purpose:** Retry the missing test-impact result with the exact test project rather than solution scope.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"name":"Roslyn.Workbench.Mcp.Workspace"}},"testScope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Workspace.Test"}},"includeReasons":true,"testsLimit":5}`

**Outcome:** Succeeded but again returned no tests, confirming that broad solution scope was not the cause of the missing result.

### 343. `workspace-close`

**Activity:** DOGFOOD-010 live client-usability sweep.

**Purpose:** Close the main-solution Workspace after all read-only query and investigation calls.

**Request:** `{"workspace":{"alias":"dogfood-010-live-sweep"}}`

**Outcome:** Succeeded and closed the unchanged epoch-3 Workspace with no transaction.

### 344. `workspace-list`

**Activity:** DOGFOOD-010 live client-usability sweep.

**Purpose:** Confirm that the completed sweep left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

## DOGFOOD-011 — Generic-base hierarchy design discovery

### 345. `workspace-open`

**Activity:** DOGFOOD-011 issue validation and design discovery.

**Purpose:** Open the main solution read-only to trace the hierarchy implementation, coverage and consumers.

**Request:** `{"alias":"dogfood-011-design","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"<temp-artifacts>/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,639 documents at Workspace epoch 4. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 346. `search-symbols`

**Activity:** DOGFOOD-011 implementation discovery.

**Purpose:** Locate the hierarchy service contract, implementation and focused tests.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"scope":{"kind":"Solution"},"query":"TypeHierarchyService","kinds":["NamedType","Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with `ITypeHierarchyService`, `TypeHierarchyService` and `TypeHierarchyServiceTests`.

### 347. `get-code-context`

**Activity:** DOGFOOD-011 implementation discovery.

**Purpose:** Inspect derived-type discovery, deduplication and hierarchy-depth calculation.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":4,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Workspace/Hierarchy/TypeHierarchyService.cs"},"range":{"start":116,"length":20}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded with the complete implementation. `SymbolFinder` discovers transitive types, after which `GetDistance` walks constructed direct parents and compares them directly with the selected root through `SymbolEqualityComparer.Default`; unreachable matches are discarded.

### 348. `get-code-context`

**Activity:** DOGFOOD-011 coverage discovery.

**Purpose:** Inspect the focused hierarchy-service tests for generic-root coverage.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":4,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Hierarchy/TypeHierarchyServiceTests.cs"},"range":{"start":131,"length":25}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed one non-generic class-hierarchy test and one non-generic interface-hierarchy test, with no generic base fixture.

### 349. `search-symbols`

**Activity:** DOGFOOD-011 consumer discovery.

**Purpose:** Resolve the shared service method before tracing every caller.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"scope":{"kind":"Solution"},"query":"FindDerivedTypesAsync","kinds":["Method"],"symbolsLimit":20}`

**Outcome:** Succeeded with the interface and implementation declarations.

### 350. `find-callers`

**Activity:** DOGFOOD-011 consumer discovery.

**Purpose:** Identify production and test consumers of the shared hierarchy method.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"symbol":{"documentationCommentId":"M:Roslyn.Workbench.Mcp.Workspace.Hierarchy.ITypeHierarchyService.FindDerivedTypesAsync(Microsoft.CodeAnalysis.INamedTypeSymbol,Microsoft.CodeAnalysis.Solution,System.Collections.Generic.IReadOnlyCollection{Microsoft.CodeAnalysis.Project},System.Threading.CancellationToken)","project":{"name":"Roslyn.Workbench.Mcp.Abstractions"}},"scope":{"kind":"Solution"},"includeContext":true,"callersLimit":10,"callSitesPerCallerLimit":5}`

**Outcome:** Succeeded with six callers. The two production consumers are `FindDerivedTypesTool` and `GetTypeHierarchyTool`; the remaining callers are test wiring and the two existing service tests.

### 351. `search-symbols`

**Activity:** DOGFOOD-011 generic-interface control discovery.

**Purpose:** Resolve the generic `IQueryToolHandler<TRequest, TResponse>` declaration for the interface branch.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"scope":{"kind":"Solution"},"query":"IQueryToolHandler","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the non-generic marker, generic two-argument plugin interface and the separate Code Action interface.

### 352. `find-derived-types`

**Activity:** DOGFOOD-011 generic-interface issue validation.

**Purpose:** Determine whether the generic-root defect also affects the hierarchy service's interface branch.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler\u00602","project":{"name":"Roslyn.Workbench.Mcp.Plugins"}},"scope":{"kind":"Solution"},"maxDepth":1,"derivedTypesLimit":2}`

**Outcome:** Succeeded but returned no derived types.

### 353. `find-implementations`

**Activity:** DOGFOOD-011 generic-interface control.

**Purpose:** Prove that implementations of the same generic interface are present and discoverable by Roslyn.

**Request:** `{"workspace":{"alias":"dogfood-011-design"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler\u00602","project":{"name":"Roslyn.Workbench.Mcp.Plugins"}},"scope":{"kind":"Solution"},"implementationsLimit":2}`

**Outcome:** Succeeded with two of 75 implementations and `hasMore: true`, confirming that `find-derived-types` had discarded valid generic-interface discoveries rather than accurately reporting an empty hierarchy.

### 354. `workspace-close`

**Activity:** DOGFOOD-011 issue validation and design discovery.

**Purpose:** Close the read-only main-solution Workspace after design evidence was complete.

**Request:** `{"workspace":{"alias":"dogfood-011-design"}}`

**Outcome:** Succeeded and closed the unchanged epoch-4 Workspace with no transaction.

### 355. `workspace-list`

**Activity:** DOGFOOD-011 issue validation and design discovery.

**Purpose:** Confirm that design discovery left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

## DOGFOOD-011 — Published pre-commit validation

### 356. `server-status`

**Activity:** DOGFOOD-011 published pre-commit validation.

**Purpose:** Confirm the restarted configured dogfood Host is healthy before exercising the generic hierarchy correction.

**Request:** `{"detail":"Minimal"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, 56 published tools and available Code Actions. An operating-system process check separately confirmed that both configured Host processes resolved to the new `dogfood-011-precommit-10a5b05-9FzT9t` candidate.

### 357. `workspace-list`

**Activity:** DOGFOOD-011 published pre-commit validation.

**Purpose:** Confirm the restarted Host has no loaded Workspace or transaction owner before opening the main solution.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 358. `workspace-open`

**Activity:** DOGFOOD-011 published pre-commit validation.

**Purpose:** Open the main solution with an isolated artifacts directory for the hierarchy checks.

**Request:** `{"alias":"dogfood-011-validation","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"<non-existent-temp-artifacts>"}}`

**Outcome:** Failed accurately with `WorkspaceMsBuildPropertiesInvalid` because the requested absolute artifacts directory did not yet exist.

### 359. `workspace-open`

**Activity:** DOGFOOD-011 published pre-commit validation.

**Purpose:** Retry the main-solution open after creating the isolated artifacts directory.

**Request:** `{"alias":"dogfood-011-validation","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"<temp-artifacts>/roslyn-workbench-mcp-dogfood-011"}}`

**Outcome:** Succeeded with 30 projects and 1,595 documents at Workspace epoch 1. Diagnostics comprised the expected WSL mounted-Windows-filesystem warning and two skipped analyser references because the isolated artifacts directory intentionally contained no prebuilt analyser output.

### 360. `search-symbols`

**Activity:** DOGFOOD-011 generic-class validation.

**Purpose:** Resolve the generic `McpServerToolBase<TRequest>` declaration through the configured dogfood client.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"},"query":"McpServerToolBase","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with the generic production declaration and its test type; the production result published a reusable documentation-comment selector and canonical location selector.

### 361. `find-derived-types`

**Activity:** DOGFOOD-011 generic-class validation.

**Purpose:** Verify that the corrected hierarchy service retains descendants whose direct parents are constructed forms of `McpServerToolBase<TRequest>`.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.ToolExecution.McpServerToolBase\u00601","project":{"projectId":"<project-id>"}},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"scope":{"kind":"Solution"},"maxDepth":5,"derivedTypesLimit":100}`

**Outcome:** Succeeded with all 19 descendants and no continuation: five generic intermediate bases at depth 1 and fourteen concrete tools at depth 2. Before the correction this generic root returned an empty collection.

### 362. `get-type-hierarchy`

**Activity:** DOGFOOD-011 generic-class validation.

**Purpose:** Exercise the second production consumer of the corrected hierarchy service for `McpServerToolBase<TRequest>`.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.ToolExecution.McpServerToolBase\u00601","project":{"projectId":"<project-id>"}},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"includeDerived":true,"maxDepth":5,"baseTypesLimit":20,"interfacesLimit":20,"derivedTypesLimit":100}`

**Outcome:** Succeeded with the same complete 19-descendant hierarchy and correct depth values, with no continuation.

### 363. `search-symbols`

**Activity:** DOGFOOD-011 generic-interface validation.

**Purpose:** Resolve the generic `IQueryToolHandler<TRequest, TResponse>` interface for the interface branch of hierarchy calculation.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"},"query":"IQueryToolHandler","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with the non-generic marker, the generic two-argument plugin interface and the separate Code Action interface.

### 364. `find-derived-types`

**Activity:** DOGFOOD-011 generic-interface validation.

**Purpose:** Verify that the corrected hierarchy service retains implementations whose direct interfaces are constructed forms of `IQueryToolHandler<TRequest, TResponse>`.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler\u00602","project":{"projectId":"<project-id>"}},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"scope":{"kind":"Solution"},"maxDepth":5,"derivedTypesLimit":100}`

**Outcome:** Succeeded with 38 descendants and no continuation: the generic `QueryToolHandler<TRequest, TResponse>` at depth 1 and 37 concrete inspection tools at depth 2. Before the correction this generic interface returned an empty collection.

### 365. `get-type-hierarchy`

**Activity:** DOGFOOD-011 generic-interface validation.

**Purpose:** Confirm the second hierarchy consumer produces the same corrected generic-interface tree.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Plugins.IQueryToolHandler\u00602","project":{"projectId":"<project-id>"}},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"includeDerived":true,"maxDepth":5,"baseTypesLimit":20,"interfacesLimit":20,"derivedTypesLimit":100}`

**Outcome:** Succeeded with the same 38 descendants, correct depth-1 and depth-2 grouping and no continuation.

### 366. `workspace-close`

**Activity:** DOGFOOD-011 published pre-commit validation.

**Purpose:** Close the read-only main-solution Workspace after hierarchy validation completed.

**Request:** `{"workspace":{"alias":"dogfood-011-validation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-1 Workspace with no transaction.

### 367. `workspace-list`

**Activity:** DOGFOOD-011 published pre-commit validation.

**Purpose:** Confirm the validation run left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

## DOGFOOD-012 — Cross-project test-impact design discovery

### 368. `workspace-list`

**Activity:** DOGFOOD-012 design discovery.

**Purpose:** Confirm the published dogfood Host has no loaded Workspace or transaction owner before beginning discovery.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 369. `workspace-open`

**Activity:** DOGFOOD-012 design discovery.

**Purpose:** Open the main solution read-only to inspect the test-impact implementation and existing coverage.

**Request:** `{"alias":"dogfood-012-design","path":"<repo>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repo>","msBuildProperties":{"artifactsPath":"<temp-artifacts>/roslyn-workbench-mcp"}}`

**Outcome:** Succeeded with 30 projects and 1,639 documents at Workspace epoch 2. The only diagnostic was the expected WSL mounted-Windows-filesystem performance warning.

### 370. `search-symbols`

**Activity:** DOGFOOD-012 implementation discovery.

**Purpose:** Locate the dependency-analysis contract, implementation and focused unit tests.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"query":"DependencyAnalysisService","kinds":["NamedType","Method"],"scope":{"kind":"Solution"},"symbolsLimit":30}`

**Outcome:** Succeeded with `DependencyAnalysisService`, `IDependencyAnalysisService` and `DependencyAnalysisServiceTests`.

### 371. `search-symbols`

**Activity:** DOGFOOD-012 consumer and coverage discovery.

**Purpose:** Locate the test-impact service method, tool, contracts and tests.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"query":"TestImpact","kinds":["NamedType","Method"],"scope":{"kind":"Solution"},"symbolsLimit":50}`

**Outcome:** Succeeded with `FindTestImpactsAsync`, `GetTestImpactTool`, its request and response contracts, the tool tests and two service test methods that exercise test-impact behaviour.

### 372. `get-code-context`

**Activity:** DOGFOOD-012 implementation discovery.

**Purpose:** Inspect the complete `FindTestImpactsAsync` implementation in one request.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":1814,"length":20}}},"beforeLines":20,"afterLines":220,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Failed accurately with `InvalidRequest` because `afterLines` exceeded the published maximum.

### 373. `get-code-context`

**Activity:** DOGFOOD-012 implementation discovery.

**Purpose:** Retry inspection of `FindTestImpactsAsync` within the published line bound.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":1814,"length":20}}},"beforeLines":20,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed that the selected target is normalised once, candidate test methods are resolved from each test document's semantic model, and every method is compared with the unchanged production-compilation target.

### 374. `search-symbols`

**Activity:** DOGFOOD-012 matching-path discovery.

**Purpose:** Locate the helper that compares candidate method dependencies with the selected target.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"query":"HasTargetDependencyAsync","kinds":["Method"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins"}},"symbolsLimit":10}`

**Outcome:** Succeeded with the single private implementation in `DependencyAnalysisService`.

### 375. `get-code-context`

**Activity:** DOGFOOD-012 matching-path discovery.

**Purpose:** Inspect signature, operation-tree, owning-type and composite-type matching.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":8012,"length":24}}},"beforeLines":10,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed that method return types, parameter types and all descendant operations are checked. A field access is already eligible through its operation type, so class-held `_target` usage is intended to count.

### 376. `search-symbols`

**Activity:** DOGFOOD-012 identity discovery.

**Purpose:** Locate the final symbol equality helper used by test-impact matching.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"query":"SymbolsMatch","kinds":["Method"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins"}},"symbolsLimit":10}`

**Outcome:** Succeeded with the private `SymbolsMatch` method.

### 377. `get-code-context`

**Activity:** DOGFOOD-012 identity discovery.

**Purpose:** Inspect the precise normalisation and equality rule at the failed cross-compilation boundary.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"range":{"start":44272,"length":12}}},"beforeLines":20,"afterLines":30,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed that named types and members are reduced to `OriginalDefinition` and then compared only with `SymbolEqualityComparer.Default`; no symbol is rebound into the candidate test compilation.

### 378. `get-document-outline`

**Activity:** DOGFOOD-012 service-coverage discovery.

**Purpose:** Inventory every `DependencyAnalysisServiceTests` method and identify all existing test-impact scenarios.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Test/Analysis/DependencyAnalysisServiceTests.cs"},"includeMembers":true,"maxDepth":3,"nodesLimit":200}`

**Outcome:** Succeeded with the complete outline. Only the limit-plus-one test and the composite-type test invoke `FindTestImpactsAsync`.

### 379. `get-code-context`

**Activity:** DOGFOOD-012 service-coverage discovery.

**Purpose:** Inspect the existing limit and `hasMore` test-impact fixture.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Test/Analysis/DependencyAnalysisServiceTests.cs"},"range":{"start":4985,"length":108}}},"beforeLines":5,"afterLines":70,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":4,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed the production target and all candidate tests in one in-memory document and compilation.

### 380. `get-code-context`

**Activity:** DOGFOOD-012 service-coverage discovery.

**Purpose:** Inspect the existing composite-type test-impact fixture.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Test/Analysis/DependencyAnalysisServiceTests.cs"},"range":{"start":22119,"length":104}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":4,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed that arrays and generic types are covered, again with production and test code in one project and compilation.

### 381. `get-code-context`

**Activity:** DOGFOOD-012 tool-boundary discovery.

**Purpose:** Inspect the complete `GetTestImpactTool` execution path and responsibility split.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetTestImpactTool.cs"},"range":{"start":189,"length":17}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":4,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed that the tool resolves the symbol and test documents, delegates all matching to `IDependencyAnalysisService`, and projects the returned values into a bounded response.

### 382. `get-code-context`

**Activity:** DOGFOOD-012 tool-coverage discovery.

**Purpose:** Inspect the successful tool unit test to determine whether it crosses the real service boundary.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetTestImpactToolTests.cs"},"range":{"start":3648,"length":95}}},"beforeLines":10,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":4,"includeDiagnostics":false}`

**Outcome:** Succeeded and showed that `IDependencyAnalysisService` is mocked. The test covers request delegation and bounded projection but cannot exercise cross-project symbol identity.

### 383. `search-symbols`

**Activity:** DOGFOOD-012 live issue revalidation.

**Purpose:** Resolve the production `CommitRecoveryStore` type and its test class on the current committed build.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"query":"CommitRecoveryStore","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with the production type, its interface and `CommitRecoveryStoreTests` in the referencing test project.

### 384. `get-test-impact`

**Activity:** DOGFOOD-012 live issue revalidation.

**Purpose:** Reproduce the cross-project empty result against the explicit Workspace test project before proposing a correction.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"projectId":"<production-project-id>"}},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"testScope":{"kind":"Project","project":{"projectId":"<test-project-id>"}},"includeReasons":true,"testsLimit":10}`

**Outcome:** Succeeded but returned zero tests, reproducing the defect on the committed DOGFOOD-011 build.

### 385. `get-code-context`

**Activity:** DOGFOOD-012 live fixture validation.

**Purpose:** Confirm how the real test class holds and accesses the selected production type.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Workspace.Test/Recovery/CommitRecoveryStoreTests.cs"},"range":{"start":306,"length":24}}},"beforeLines":8,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":4,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed a strongly typed `CommitRecoveryStore _target` field created in the test constructor and accessed by individual test methods.

### 386. `search-symbols`

**Activity:** DOGFOOD-012 test-design discovery.

**Purpose:** Locate the in-memory Roslyn project-definition helper before deciding whether realistic project-reference unit coverage requires new infrastructure.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"query":"InMemoryRoslynProjectDefinition","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with the shared TestSupport definition.

### 387. `get-code-context`

**Activity:** DOGFOOD-012 test-design discovery.

**Purpose:** Inspect the shared project-definition capabilities relevant to cross-project service coverage.

**Request:** `{"workspace":{"alias":"dogfood-012-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.TestSupport/InMemoryRoslynProjectDefinition.cs"},"range":{"start":191,"length":31}}},"beforeLines":5,"afterLines":80,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":4,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed that the existing helper already supports named project references and distinct assembly names; no unit-test infrastructure change is required.

### 388. `workspace-close`

**Activity:** DOGFOOD-012 design discovery.

**Purpose:** Close the read-only main-solution Workspace after the implementation, coverage and live failure paths were established.

**Request:** `{"workspace":{"alias":"dogfood-012-design"}}`

**Outcome:** Succeeded and closed the unchanged epoch-2 Workspace with no transaction.

### 389. `workspace-list`

**Activity:** DOGFOOD-012 design discovery.

**Purpose:** Confirm that design discovery left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 390. `workspace-list`

**Activity:** DOGFOOD-012 implementation preparation.

**Purpose:** Confirm that no published dogfood Workspace or transaction remained before loading the approved implementation target.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 391. `workspace-open`

**Activity:** DOGFOOD-012 implementation inspection.

**Purpose:** Load the trusted main solution for Roslyn-backed inspection of the approved test-impact change.

**Request:** `{"alias":"dogfood-012-implementation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 3 with 30 projects and 1,639 documents. The expected WSL-on-Windows-filesystem performance warning was reported.

### 392. `get-code-context`

**Activity:** DOGFOOD-012 implementation inspection.

**Purpose:** Inspect the complete current `FindTestImpactsAsync` implementation before applying the approved change.

**Request:** `{"workspace":{"alias":"dogfood-012-implementation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"selectedText":"FindTestImpactsAsync"}},"beforeLines":5,"afterLines":120,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Rejected as `InvalidRequest` because `afterLines` exceeded the supported maximum.

### 393. `get-code-context`

**Activity:** DOGFOOD-012 implementation inspection.

**Purpose:** Retry the implementation inspection with the supported code-window limit.

**Request:** `{"workspace":{"alias":"dogfood-012-implementation"},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs"},"selectedText":"FindTestImpactsAsync"}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed the single-compilation target normalisation, candidate discovery, direct dependency checks, result limiting and bounded projection path before modification.

### 394. `workspace-close`

**Activity:** DOGFOOD-012 implementation inspection.

**Purpose:** Close the read-only implementation Workspace after the approved change and local validation were complete.

**Request:** `{"workspace":{"alias":"dogfood-012-implementation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-3 Workspace with no transaction.

### 395. `workspace-list`

**Activity:** DOGFOOD-012 implementation inspection.

**Purpose:** Confirm that implementation inspection left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 396. Candidate `initialize`

**Activity:** DOGFOOD-012 pre-commit candidate publication.

**Purpose:** Start an isolated protocol smoke test against the Release candidate published from the reviewed staged baseline before promoting the configured dogfood target.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-012-smoke","version":"1.0"}}`

**Outcome:** Succeeded against `dogfood-012-precommit-fd28958-QTguJi`. Host logs confirmed that the `initialize` handler completed for client `dogfood-012-smoke`; the candidate later shut down cleanly on end of input.

### 397. Candidate `tools/list`

**Activity:** DOGFOOD-012 pre-commit candidate publication.

**Purpose:** Confirm the isolated candidate can materialise and publish its complete MCP tool catalogue before promotion.

**Request:** `{}`

**Outcome:** Succeeded. Host logs confirmed that the candidate's `tools/list` handler completed without a catalogue materialisation error. The isolated candidate was then promoted atomically to the configured dogfood `current` target.

### 398. `server-status`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Confirm the restarted configured dogfood namespace is healthy before repeating the cross-project test-impact regression.

**Request:** `{}`

**Outcome:** Succeeded with Host version `1.0.0.0`, Roslyn `5.6.0.0`, MSBuild `10.0.102`, available Code Actions and 56 published tools. An operating-system process check separately confirmed that both configured Host processes resolved to `dogfood-012-precommit-fd28958-QTguJi`.

### 399. `workspace-list`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Confirm the restarted published Host has no loaded Workspace or transaction owner before validation.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 400. `workspace-open`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Open the main solution read-only through Codex's configured dogfood tools so the previously failing real cross-project test-impact request can be repeated.

**Request:** `{"alias":"dogfood-012-validation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 30 projects and 1,641 documents. The expected WSL-on-Windows-filesystem performance warning was reported.

### 401. `search-symbols`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Resolve the production `CommitRecoveryStore` type and its test class using the newly loaded Workspace identities.

**Request:** `{"workspace":{"alias":"dogfood-012-validation"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"query":"CommitRecoveryStore","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Rejected as `InvalidRequest` because `SearchSymbolsRequest` does not contain an `expectedSnapshot` member.

### 402. `search-symbols`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Retry symbol resolution using the actual `search-symbols` contract.

**Request:** `{"workspace":{"alias":"dogfood-012-validation"},"query":"CommitRecoveryStore","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with the production type, its interface and `CommitRecoveryStoreTests`. The production type belonged to the Workspace production project and the test class belonged to the distinct referencing test project.

### 403. `get-test-impact`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Repeat the exact real cross-project regression that returned zero tests on the committed DOGFOOD-011 build.

**Request:** `{"workspace":{"alias":"dogfood-012-validation"},"symbol":{"documentationCommentId":"T:Roslyn.Workbench.Mcp.Workspace.Recovery.CommitRecoveryStore","project":{"projectId":"<production-project-id>"}},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"testScope":{"kind":"Project","project":{"projectId":"<test-project-id>"}},"includeReasons":true,"testsLimit":10}`

**Outcome:** Succeeded with 10 results from `CommitRecoveryStoreTests.cs` and `hasMore: true`. Every returned result carried the reason `Direct reference to the target symbol or its owning type.`, confirming that the production symbol was rebound successfully into the distinct test-project compilation instead of producing the previous empty result.

### 404. `workspace-close`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Close the read-only main-solution Workspace after the decisive regression passed.

**Request:** `{"workspace":{"alias":"dogfood-012-validation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-1 Workspace with no transaction.

### 405. `workspace-list`

**Activity:** DOGFOOD-012 published validation.

**Purpose:** Confirm the completed dogfood validation left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 406. `workspace-list`

**Activity:** DOGFOOD-013 existing-coverage discovery.

**Purpose:** Confirm the published dogfood Host has no loaded Workspace or transaction owner before inspecting the error-reporting workflow.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 407. `workspace-open`

**Activity:** DOGFOOD-013 existing-coverage discovery.

**Purpose:** Open the main solution read-only so the error-reporting contracts, implementation and current tests can be inspected through the published dogfood Host.

**Request:** `{"alias":"dogfood-013-design","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 30 projects and 1,641 documents. The expected WSL-on-Windows-filesystem performance warning was reported.

### 408. `search-symbols`

**Activity:** DOGFOOD-013 implementation and coverage discovery.

**Purpose:** Inventory production and test types associated with error-report preparation, consent and dispatch.

**Request:** `{"workspace":{"alias":"dogfood-013-design"},"query":"ErrorReport","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":100}`

**Outcome:** Succeeded with 35 named types spanning availability, configuration, consent, contracts, preparation, Logging and Sentry dispatchers, the two MCP tools and their focused test classes.

### 409. `get-code-context`

**Activity:** DOGFOOD-013 preparation-boundary discovery.

**Purpose:** Inspect `PrepareErrorReportTool` from a readable source selection.

**Request:** `{"workspace":{"alias":"dogfood-013-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"selection":{"document":{"path":"src/Roslyn.Workbench.Mcp/ErrorReporting/Tools/PrepareErrorReportTool.cs"},"selectedText":"PrepareErrorReportTool"}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Rejected as `LocationAmbiguous` because the selected text matched both the type and constructor.

### 410. `get-code-context`

**Activity:** DOGFOOD-013 preparation-boundary discovery.

**Purpose:** Retry `PrepareErrorReportTool` inspection with the exact type-declaration span.

**Request:** `{"workspace":{"alias":"dogfood-013-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"documentId":"<document-id>"},"range":{"start":189,"length":22}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed that preparation is read-only, performs no dispatch, projects through an allow-list, returns the exact provider preview and digest, retains an immutable opaque handle and publishes the explicit excluded-category list.

### 411. `get-code-context`

**Activity:** DOGFOOD-013 consent and submission discovery.

**Purpose:** Inspect the complete entry path of `SubmitErrorReportTool` and its consent boundary.

**Request:** `{"workspace":{"alias":"dogfood-013-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"documentId":"<document-id>"},"range":{"start":243,"length":21}}},"beforeLines":5,"afterLines":100,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed that submission accepts only the prepared handle, is marked destructive and open-world, fails closed without elicitation, and supports one-report, Workspace and session consent plus decline and session suppression.

### 412. `search-symbols`

**Activity:** DOGFOOD-013 configured-destination discovery.

**Purpose:** Locate the `server-status` request contract before querying full runtime configuration.

**Request:** `{"workspace":{"alias":"dogfood-013-design"},"query":"ServerStatusRequest","kinds":["NamedType"],"scope":{"kind":"Solution"},"symbolsLimit":10}`

**Outcome:** Succeeded with the single Host-owned request type.

### 413. `get-code-context`

**Activity:** DOGFOOD-013 configured-destination discovery.

**Purpose:** Inspect the `server-status` request and confirm how to request configuration detail.

**Request:** `{"workspace":{"alias":"dogfood-013-design"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"location":{"span":{"document":{"documentId":"<document-id>"},"range":{"start":164,"length":19}}},"beforeLines":5,"afterLines":40,"includeEnclosingSymbols":false,"includeDiagnostics":false}`

**Outcome:** Succeeded and confirmed the `detail` field defaults to `Minimal` and accepts the published status-detail enum.

### 414. `server-status`

**Activity:** DOGFOOD-013 configured-destination discovery.

**Purpose:** Identify the active dogfood provider, consent mode and session consent state before proposing any live submission workflow.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded. The published Host uses provider `Logging`, consent mode `Prompt` and session state `PromptRequired`; therefore a reviewed successful submission would write locally to stderr and still require MCP elicitation rather than contacting Sentry.

### 415. `workspace-close`

**Activity:** DOGFOOD-013 existing-coverage discovery.

**Purpose:** Close the read-only main-solution Workspace after the implementation, coverage and trust boundary were established.

**Request:** `{"workspace":{"alias":"dogfood-013-design"}}`

**Outcome:** Succeeded and closed the unchanged epoch-2 Workspace with no transaction.

### 416. `workspace-list`

**Activity:** DOGFOOD-013 existing-coverage discovery.

**Purpose:** Confirm discovery left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 417. Candidate `initialize`

**Activity:** DOGFOOD-013 Sentry candidate preparation, first smoke attempt.

**Purpose:** Initialise the isolated Sentry-enabled candidate with the controlled `HostQuery` fixture before promotion.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-smoke","version":"1.0"}}`

**Outcome:** The Host log confirmed that initialization completed successfully. The one-shot pipe ended before response bytes were retained for inspection, so a session-aware smoke test was run later.

### 418. Candidate `tools/list`

**Activity:** DOGFOOD-013 Sentry candidate preparation, first smoke attempt.

**Purpose:** Materialise the isolated candidate's catalogue and controlled failure tool.

**Request:** `{}`

**Outcome:** The Host log confirmed that catalogue materialisation completed successfully, but the one-shot pipe did not retain the response bytes needed to inspect the tool list.

### 419. Candidate `server-status`

**Activity:** DOGFOOD-013 Sentry candidate preparation, first smoke attempt.

**Purpose:** Confirm the candidate resolved the embedded provider and retained prompt consent.

**Request:** `{"detail":"Full"}`

**Outcome:** The Host log confirmed a successful non-error response, but the one-shot pipe did not retain the response bytes needed to inspect the provider fields.

### 420. Candidate `initialize`

**Activity:** DOGFOOD-013 Sentry candidate preparation, session-aware smoke retry.

**Purpose:** Establish an isolated MCP session that remains open until response bytes have been inspected.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-session-smoke","version":"1.0"}}`

**Outcome:** Succeeded and established the session used for the decisive catalogue and provider checks.

### 421. Candidate `tools/list`

**Activity:** DOGFOOD-013 Sentry candidate preparation, session-aware smoke retry.

**Purpose:** Verify the private controlled failure fixture was loaded alongside the normal catalogue.

**Request:** `{}`

**Outcome:** Succeeded with 58 tools and included `host-valid-query` from the existing acceptance fixture.

### 422. Candidate `server-status`

**Activity:** DOGFOOD-013 Sentry candidate preparation, session-aware smoke retry.

**Purpose:** Verify the user-supplied build-time DSN selected Sentry without weakening the consent policy.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with provider `Sentry`, consent mode `Prompt` and session state `PromptRequired`. No error report was prepared or submitted. The candidate was then promoted atomically to the configured dogfood `current` target.

### 423. `server-status`

**Activity:** DOGFOOD-013 Sentry client-usability validation.

**Purpose:** Confirm the restarted configured namespace is using the promoted Sentry candidate and controlled failure fixture before creating any report state.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with committed version `8264329`, provider `Sentry`, consent mode `Prompt`, session state `PromptRequired`, 58 tools and both the bundled Core and temporary `HostQuery` plugins enabled without diagnostics.

### 424. `workspace-list`

**Activity:** DOGFOOD-013 Sentry client-usability validation.

**Purpose:** Confirm the restarted Host has no loaded Workspace or transaction owner before the controlled failure workflow.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 425. `workspace-open`

**Activity:** DOGFOOD-013 Sentry client-usability validation.

**Purpose:** Open the main solution read-only to give the controlled fixture a realistic trusted Workspace context.

**Request:** `{"alias":"dogfood-013-validation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 30 projects and 1,641 documents. The expected WSL-on-Windows-filesystem performance warning was reported.

### 426. `host-valid-query`

**Activity:** DOGFOOD-013 controlled correlated failure.

**Purpose:** Use the existing deterministic acceptance fixture to create one known unexpected query failure without mutating the Workspace.

**Request:** `{"workspace":{"alias":"dogfood-013-validation"},"name":"DOGFOOD-013 controlled failure","throw":true}`

**Outcome:** Returned the expected generic `UnhandledException` with a correlation ID, local details continuation and available preparation continuation. The ordinary response did not expose the controlled exception message.

### 427. `get-error-details`

**Activity:** DOGFOOD-013 local diagnostic inspection.

**Purpose:** Confirm the correlated local-only record is distinguishable from content that may be externally submitted.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with sensitivity `LocalDiagnostic` and `safeForExternalSubmission: false`. The record contained the fixed fixture exception message, local Host source path and full Workspace context as expected; none of that local payload was copied into a reporting request or the usage log.

### 428. `prepare-error-report`

**Activity:** DOGFOOD-013 sanitised Sentry payload preparation.

**Purpose:** Create the immutable allow-listed Sentry event for complete user review without network activity.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with dispatcher `Sentry`, destination `Sentry project 4511823918661636 at o54372.ingest.us.sentry.io`, an opaque submission handle expiring at `2026-08-28T18:02:46.7730938+00:00`, event ID `1e0f55d3883046b9ae396107d0e27abb`, report ID `e6cd89e34ea44174a85947020d044571` and payload digest `610cf54b572f1405eb702de949dfdc2b24980f249d30148c6d6908b6268b5770`. An independent SHA-256 calculation over the exact UTF-8 `payloadJson` matched the returned digest. The payload contained only the documented coarse event, Workspace-count, version, platform and component fields; it excluded the controlled exception message, correlation ID and repository root. No submission or network activity occurred.

### 429. `submit-error-report`

**Activity:** DOGFOOD-013 approved Sentry submission attempt.

**Purpose:** After the user explicitly approved the displayed destination, exact payload and digest in chat, submit that immutable prepared report once without granting Workspace or session-wide consent.

**Request:** `{"submissionHandle":"<submission-handle>"}`

**Outcome:** No report was submitted. Codex completed the MCP elicitation as a decline, so the Host returned `ErrorReportDeclined` and discarded the prepared handle despite the preceding chat approval. The private Host stderr capture contained no prepared event identity or approved-report entry and recorded the tool as an error result. The safe fail-closed Host behaviour is correct; the mismatch between explicit chat approval and the client's elicitation response is the surviving client-usability outcome.

### 430. `workspace-close`

**Activity:** DOGFOOD-013 Sentry client-usability validation.

**Purpose:** Close the read-only main-solution Workspace after the declined submission attempt.

**Request:** `{"workspace":{"alias":"dogfood-013-validation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-1 Workspace with no transaction.

### 431. `workspace-list`

**Activity:** DOGFOOD-013 Sentry client-usability validation.

**Purpose:** Confirm the completed attempt left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 432. `prepare-error-report`

**Activity:** DOGFOOD-013 corrected elicitation retry.

**Purpose:** After the user clarified that conversational approval must not substitute for MCP elicitation, create a fresh immutable payload because the first declined handle had been discarded.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the same sanitised coarse content and a new immutable identity: event ID `6e28ef7325a941acb21cf80f6cbff695`, report ID `fd4dd361aa764e65897d7ba4400a0808`, digest `5eb72eec50e8f70949ab4f32354270555900a8400c3810f1a3b432b6ded2df9b` and an opaque handle expiring at `2026-08-28T18:05:38.3333151+00:00`. The complete new payload and destination were displayed before submission.

### 433. `submit-error-report`

**Activity:** DOGFOOD-013 corrected MCP elicitation attempt.

**Purpose:** Invoke submission specifically so the Host's MCP elicitation form could collect one-report consent, without treating chat approval as the elicitation result.

**Request:** `{"submissionHandle":"<submission-handle>"}`

**Outcome:** No form was surfaced to the user and no report was submitted. Codex immediately returned the elicitation action as a decline, so the Host again returned `ErrorReportDeclined` and discarded the fresh handle. The private stderr capture contained neither the second event identity nor an approved-report entry. This corrected retry isolates the remaining outcome to the Codex client's elicitation handling rather than the earlier conversational-approval mistake.

### 434. `workspace-open`

**Activity:** DOGFOOD-013 MCP elicitation validation with Codex approvals enabled.

**Purpose:** Open the main solution as a trusted realistic Workspace after confirming that the task's effective Codex approval policy was `on-request`.

**Request:** `{"alias":"dogfood-013-elicitation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>"}`

**Outcome:** Succeeded at Workspace epoch 2 with 30 projects and 1,597 documents. The load also reported missing packages from the Host process's isolated NuGet cache and the expected WSL-on-Windows-filesystem warning; these diagnostics did not prevent the controlled Host fixture from running.

### 435. `host-valid-query`

**Activity:** DOGFOOD-013 controlled correlated failure with Codex approvals enabled.

**Purpose:** Create a fresh deterministic unexpected query failure for a new elicitation attempt.

**Request:** `{"workspace":{"alias":"dogfood-013-elicitation"},"name":"DOGFOOD-013 elicitation validation","throw":true}`

**Outcome:** Returned the expected generic `UnhandledException` with a new correlation ID, local-details continuation and error-report preparation continuation. The ordinary response did not disclose the controlled exception message.

### 436. `get-error-details`

**Activity:** DOGFOOD-013 local diagnostic inspection with Codex approvals enabled.

**Purpose:** Reconfirm that the fresh correlated diagnostic was local-only and must not be used as the external payload.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with sensitivity `LocalDiagnostic` and `safeForExternalSubmission: false`. The local record contained the controlled exception and Workspace details as expected; none of its sensitive fields were copied into the reporting request or ledger.

### 437. `prepare-error-report`

**Activity:** DOGFOOD-013 sanitised Sentry payload preparation with Codex approvals enabled.

**Purpose:** Create a fresh immutable allow-listed payload for complete user review before exercising MCP form elicitation.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded without network activity. The prepared destination was `Sentry project 4511823918661636 at o54372.ingest.us.sentry.io`, event ID `47cf832883e44cb28cc57db64a33dafa`, report ID `5d0d7cd46b8449aeb7ba33435876ebee` and payload digest `09a2e4d6dd6018630fc78eb14b7524cb5c915c9c0afea12a168d75173b0faa9b`. The complete immutable payload, destination, digest and excluded categories were displayed to the user before submission.

### 438. `submit-error-report`

**Activity:** DOGFOOD-013 successful MCP form elicitation and Sentry submission.

**Purpose:** Validate that Codex surfaces the Host's MCP form when the task's effective approval policy is `on-request`, and that one-report approval submits exactly the reviewed immutable payload.

**Request:** `{"submissionHandle":"<submission-handle>"}`

**Outcome:** Codex surfaced the MCP elicitation, the user approved this one report, and submission succeeded. The Host returned dispatcher `Sentry`, report reference `47cf832883e44cb28cc57db64a33dafa` and digest `09a2e4d6dd6018630fc78eb14b7524cb5c915c9c0afea12a168d75173b0faa9b`, exactly matching the reviewed payload. This confirms that the previous immediate declines were caused by the task's effective `never` approval policy rather than the Host elicitation contract.

### 439. `workspace-close`

**Activity:** DOGFOOD-013 successful elicitation validation cleanup.

**Purpose:** Close the temporary main-solution Workspace after the successful Sentry submission.

**Request:** `{"workspace":{"alias":"dogfood-013-elicitation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-2 Workspace with no transaction.

### 440. `workspace-open`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Open the main solution as a trusted read-only Workspace to inspect the external report projection and Sentry dispatch symbols after reviewing the submitted event.

**Request:** `{"alias":"dogfood-013-report-review","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 3 with 30 projects and 1,641 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 441. `search-symbols`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Locate `ExternalErrorReportProjector` through Roslyn-backed solution inspection.

**Request:** `{"workspace":{"alias":"dogfood-013-report-review"},"query":"ExternalErrorReportProjector","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Returned `WorkspaceOutOfDate` with a `workspace-reload` continuation before producing symbol results.

### 442. `search-symbols`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Locate `SentryErrorReportDispatcher` through Roslyn-backed solution inspection.

**Request:** `{"workspace":{"alias":"dogfood-013-report-review"},"query":"SentryErrorReportDispatcher","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Returned `WorkspaceOutOfDate` with a `workspace-reload` continuation before producing symbol results.

### 443. `workspace-reload`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Follow the stale-Workspace continuation before retrying the two symbol queries.

**Request:** `{"workspace":{"alias":"dogfood-013-report-review"}}`

**Outcome:** Succeeded at Workspace epoch 4 with 30 projects, 1,641 documents and no load diagnostics.

### 444. `search-symbols`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Retry locating the external report projector after reloading the Workspace.

**Request:** `{"workspace":{"alias":"dogfood-013-report-review"},"query":"ExternalErrorReportProjector","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded and located the production projector, its interface and its unit-test type. The production type resolves to `ErrorReporting/Projection/ExternalErrorReportProjector.cs`.

### 445. `search-symbols`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Retry locating the Sentry dispatcher after reloading the Workspace.

**Request:** `{"workspace":{"alias":"dogfood-013-report-review"},"query":"SentryErrorReportDispatcher","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded and located the production dispatcher and its unit-test type. The production type resolves to `ErrorReporting/Dispatch/SentryErrorReportDispatcher.cs`.

### 446. `workspace-close`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Close the read-only inspection Workspace after tracing the report projection and dispatcher.

**Request:** `{"workspace":{"alias":"dogfood-013-report-review"}}`

**Outcome:** Succeeded and closed the unchanged epoch-4 Workspace with no transaction.

### 447. `get-error-details`

**Activity:** DOGFOOD-013 submitted-report diagnostic review.

**Purpose:** Reinspect the retained local-only record for the submitted event to determine whether useful method and source-line information existed before external projection.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with sensitivity `LocalDiagnostic` and `safeForExternalSubmission: false`. The captured record contained the concrete framework exception type, method-level frames and a Roslyn Workbench frame with its source file and line 62. The external plugin fixture frame and runtime frames had method information but no source locations. This confirms that the submitted payload lost available first-party diagnostic detail during projection; it was not absent from capture.

### 448. `workspace-list`

**Activity:** DOGFOOD-013 final review remediation.

**Purpose:** Check for an existing loaded Workspace before inspecting the reviewer-reported CodeActions assembly-classification defect.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 449. `workspace-open`

**Activity:** DOGFOOD-013 final review remediation.

**Purpose:** Open the main solution as a trusted read-only Workspace to inspect a type owned by the CodeActions assembly for first-party provenance classification.

**Request:** `{"alias":"dogfood-013-final-remediation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,648 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 450. `search-symbols`

**Activity:** DOGFOOD-013 final review remediation.

**Purpose:** Locate `CodeActionWorkspaceResultMapper` and confirm that it belongs to the dedicated CodeActions project before using it as the assembly marker and failure-frame test path.

**Request:** `{"workspace":{"alias":"dogfood-013-final-remediation"},"query":"CodeActionWorkspaceResultMapper","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Returned `WorkspaceOutOfDate` with a `workspace-reload` continuation before producing symbol results.

### 451. `workspace-reload`

**Activity:** DOGFOOD-013 final review remediation.

**Purpose:** Follow the stale-Workspace continuation before retrying the CodeActions marker query.

**Request:** `{"workspace":{"alias":"dogfood-013-final-remediation"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects, 1,648 documents and no load diagnostics.

### 452. `search-symbols`

**Activity:** DOGFOOD-013 final review remediation.

**Purpose:** Retry locating `CodeActionWorkspaceResultMapper` after reloading the Workspace.

**Request:** `{"workspace":{"alias":"dogfood-013-final-remediation"},"query":"CodeActionWorkspaceResultMapper","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded and located the production type in `Roslyn.Workbench.Mcp.CodeActions` plus its dedicated unit-test type, confirming that the production type is a valid CodeActions assembly marker.

### 453. `workspace-close`

**Activity:** DOGFOOD-013 final review remediation.

**Purpose:** Close the read-only inspection Workspace after confirming the CodeActions marker and completing the scoped correction.

**Request:** `{"workspace":{"alias":"dogfood-013-final-remediation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-2 Workspace with no transaction.

### 454. `workspace-list`

**Activity:** DOGFOOD-013 assembly-marker refinement.

**Purpose:** Check for an existing loaded Workspace before inspecting first-party assembly marker conventions.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 455. `workspace-open`

**Activity:** DOGFOOD-013 assembly-marker refinement.

**Purpose:** Open the main solution as a trusted read-only Workspace to inspect whether stable assembly-marker types already exist.

**Request:** `{"alias":"dogfood-013-assembly-markers","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 3 with 31 projects and 1,648 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 456. `search-symbols`

**Activity:** DOGFOOD-013 assembly-marker refinement.

**Purpose:** Search the solution for an existing assembly-marker convention before adding dedicated first-party provenance markers.

**Request:** `{"workspace":{"alias":"dogfood-013-assembly-markers"},"query":"AssemblyMarker","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with no matching types, confirming that the refinement introduces the repository's first explicit assembly markers.

### 457. `workspace-close`

**Activity:** DOGFOOD-013 assembly-marker refinement.

**Purpose:** Close the read-only inspection Workspace before formatting and validating the marker implementation.

**Request:** `{"workspace":{"alias":"dogfood-013-assembly-markers"}}`

**Outcome:** Succeeded and closed the unchanged epoch-3 Workspace with no transaction.

### 458. Candidate `initialize`

**Activity:** DOGFOOD-013 post-commit Sentry candidate preparation, isolated smoke setup.

**Purpose:** Initialise the newly published commit `11bc01f` candidate with the controlled `HostQuery` fixture before promotion.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-session-smoke","version":"1.0"}}`

**Outcome:** The request did not reach the MCP handler because the sandbox made the default home-state recovery directory read-only. The candidate exited before initialisation, so the smoke test was retried with an isolated writable state directory.

### 459. Candidate `initialize`

**Activity:** DOGFOOD-013 post-commit Sentry candidate preparation, isolated smoke retry.

**Purpose:** Initialise the candidate using a dedicated writable state directory.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-session-smoke","version":"1.0"}}`

**Outcome:** The request did not reach the MCP handler because the newly created state directory had inherited mode `755`; the Host correctly required mode `700`. The directory permissions were corrected before the decisive retry.

### 460. Candidate `initialize`

**Activity:** DOGFOOD-013 post-commit Sentry candidate preparation, decisive smoke retry.

**Purpose:** Establish an isolated MCP session after applying the required private state-directory permissions.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-session-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18`; the returned Agent Guide source tag identified `dogfood-013-11bc01f`.

### 461. Candidate `tools/list`

**Activity:** DOGFOOD-013 post-commit Sentry candidate preparation.

**Purpose:** Verify the private controlled failure fixture was loaded alongside the normal catalogue.

**Request:** `{}`

**Outcome:** Succeeded and included `host-valid-query` from the existing acceptance fixture.

### 462. Candidate `server-status`

**Activity:** DOGFOOD-013 post-commit Sentry candidate preparation.

**Purpose:** Verify the committed candidate retained the configured provider, prompt consent and controlled failure fixture before promotion.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with commit `11bc01f`, provider `Sentry`, consent mode `Prompt`, session state `PromptRequired`, 58 tools and both the bundled Core and temporary `HostQuery` plugins enabled without diagnostics. No error report was prepared or submitted. The candidate was then promoted atomically to the configured dogfood `current` target.

### 463. `server-status`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Confirm the restarted Codex namespace uses the promoted committed candidate before creating report state.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with commit `11bc01f`, provider `Sentry`, consent mode `Prompt`, consent state `PromptRequired`, 58 tools and both expected plugins enabled without diagnostics.

### 464. `host-valid-query`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Trigger the controlled unexpected failure used to prepare a fresh report.

**Request:** `{"name":"dogfood-013-fresh-report","throw":true}`

**Outcome:** Correctly returned `WorkspaceNotOpen`; the fixture is a Workspace query and required the trusted repository to be opened first.

### 465. `workspace-open`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Open the trusted main solution read-only for the controlled query failure.

**Request:** `{"alias":"dogfood-013-fresh-report","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Returned `WorkspaceMsBuildPropertiesInvalid` because the isolated artifacts directory did not yet exist. The directory was created before retrying.

### 466. `workspace-open`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Retry the trusted read-only Workspace load with an existing isolated artifacts directory.

**Request:** `{"alias":"dogfood-013-fresh-report","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,610 documents. It reported the expected WSL filesystem warning and unresolved analyser references under the empty isolated artifacts directory.

### 467. `host-valid-query`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Create the deterministic unexpected exception after satisfying the fixture's Workspace requirement.

**Request:** `{"workspace":{"alias":"dogfood-013-fresh-report"},"name":"dogfood-013-fresh-report","throw":true}`

**Outcome:** Correctly returned `UnhandledException` with correlation ID `<correlation-id>`, local diagnostic details available and external report preparation available.

### 468. `get-error-details`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Inspect the sensitive local diagnostic separately from the external report payload.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with `LocalDiagnostic`, `safeForExternalSubmission: false`, the controlled message `Sensitive query failure.`, the external fixture frame, .NET frames and a first-party `PluginQueryMcpServerTool.cs:62` frame carrying its absolute local path.

### 469. `prepare-error-report`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** Prepare the complete immutable Sentry payload without network activity for user review.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with a Sentry destination, opaque submission handle, expiry and SHA-256 digest. The payload includes the controlled exception message, bounded .NET frames and the first-party `PluginQueryMcpServerTool.cs:62` frame; it excludes the external fixture frame, absolute paths and the documented dedicated sensitive categories. No report was submitted.

### 470. `submit-error-report`

**Activity:** DOGFOOD-013 post-restart error-report validation.

**Purpose:** After presenting the exact immutable payload, destination and digest and receiving explicit approval to invoke submission, exercise the three-choice MCP consent form.

**Request:** `{"submissionHandle":"<opaque-submission-handle>"}`

**Outcome:** The MCP elicitation completed and Sentry accepted report reference `c1ad3c5af38a4bd7a8b41a5633866e54`. The returned digest `7d5a231aa76b56e64b11f10a3054ce9b3cbc4ce48af2a2a9fd3bca74f559c4a2` matched the reviewed complete payload, confirming that the selected form choice retained the exception message.

### 471. `workspace-close`

**Activity:** DOGFOOD-013 post-restart error-report validation cleanup.

**Purpose:** Close the trusted read-only Workspace after the approved submission completed.

**Request:** `{"workspace":{"alias":"dogfood-013-fresh-report"}}`

**Outcome:** Succeeded and closed the unchanged epoch-1 Workspace with no transaction.

### 472. `workspace-list`

**Activity:** DOGFOOD-013 post-restart error-report validation cleanup.

**Purpose:** Confirm the completed validation left no loaded Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded with no loaded Workspaces and no transaction owner.

### 473. Candidate `initialize`

**Activity:** DOGFOOD-013 first-party provenance probe preparation.

**Purpose:** Initialise the disposable candidate containing server-owned, bundled Core and Code Action failure probes before promotion.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-firstparty-probe-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18`; the returned Agent Guide source tag identified `dogfood-013-firstparty-probes-11bc01f`.

### 474. Candidate `server-status`

**Activity:** DOGFOOD-013 first-party provenance probe preparation.

**Purpose:** Verify the disposable candidate retained Sentry prompt consent and normal bundled composition without invoking any failure probe.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with provider `Sentry`, consent mode `Prompt`, consent state `PromptRequired`, 56 normal tools and the bundled Core plugin enabled without diagnostics. The three temporary probe source edits had already been removed byte-for-byte from the working tree before this smoke test. The candidate was then promoted atomically.

### 475. `server-status`

**Activity:** DOGFOOD-013 server-owned first-party provenance validation.

**Purpose:** Confirm the restarted namespace uses the disposable three-probe candidate before triggering the first failure.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with provider `Sentry`, prompt consent, 56 tools and the bundled Core plugin enabled without diagnostics.

### 476. `workspace-list`

**Activity:** DOGFOOD-013 server-owned first-party provenance validation.

**Purpose:** Trigger the deterministic server-owned Host failure without loading a Workspace.

**Request:** `{}`

**Outcome:** Correctly returned `UnhandledException` with correlation ID `<correlation-id>` and both local diagnostic and report preparation available.

### 477. `get-error-details`

**Activity:** DOGFOOD-013 server-owned first-party provenance validation.

**Purpose:** Verify the sensitive local diagnostic captured the genuine first-party throw point.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the controlled message and first-party frames for `WorkspaceListTool.ThrowDogfoodFirstPartyProbe` at the absolute local `WorkspaceListTool.cs:45`, its `ExecuteAsync` caller at line 31 and the server-owned adapter at `ServerOwnedToolBase.cs:50`.

### 478. `prepare-error-report`

**Activity:** DOGFOOD-013 server-owned first-party provenance validation.

**Purpose:** Prepare the complete immutable Sentry payload without network activity for user review.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the controlled exception message and the genuine throw frame `WorkspaceListTool.ThrowDogfoodFirstPartyProbe`, `WorkspaceListTool.cs:45`, followed by `ExecuteAsync` at line 31 and `ServerOwnedToolBase.cs:50`. Absolute paths were reduced to filenames. No report was submitted.

### 479. `submit-error-report`

**Activity:** DOGFOOD-013 server-owned first-party provenance validation.

**Purpose:** Submit the exact reviewed server-owned probe payload after explicit approval and completion of the MCP consent form.

**Request:** `{"submissionHandle":"<opaque-submission-handle>"}`

**Outcome:** Sentry accepted report reference `e115ed2be9b445b59782e30f318636cd`; the returned digest `883a002374a13c36a19c4f0e775fcf4fb9dace9e353e9234a3214d31ecd4bc11` matched the reviewed complete payload.

### 480. `workspace-open`

**Activity:** DOGFOOD-013 bundled Core and Code Action first-party provenance validation.

**Purpose:** Open the trusted main solution read-only for the remaining Workspace-bound probes.

**Request:** `{"alias":"dogfood-013-firstparty-probes","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,610 documents, with the expected WSL filesystem and unresolved isolated-artifacts analyser warnings.

### 481. `get-solution-structure`

**Activity:** DOGFOOD-013 bundled Core first-party provenance validation.

**Purpose:** Trigger the deterministic bundled Core query failure.

**Request:** `{"workspace":{"alias":"dogfood-013-firstparty-probes"},"includeDocuments":false,"foldersLimit":1,"projectsLimit":1,"documentsPerProjectLimit":1,"projectReferencesPerProjectLimit":1}`

**Outcome:** Correctly returned `UnhandledException` with correlation ID `<correlation-id>` and both local diagnostic and report preparation available.

### 482. `get-error-details`

**Activity:** DOGFOOD-013 bundled Core first-party provenance validation.

**Purpose:** Verify the sensitive local diagnostic captured the genuine bundled Core throw point.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the controlled message and first-party frames for `GetSolutionStructureTool.ThrowDogfoodFirstPartyProbe` at the absolute local `GetSolutionStructureTool.cs:160`, its `ExecuteCoreAsync` caller at line 12 and the Host plugin adapter at `PluginQueryMcpServerTool.cs:62`.

### 483. `prepare-error-report`

**Activity:** DOGFOOD-013 bundled Core first-party provenance validation.

**Purpose:** Prepare the complete immutable Sentry payload without network activity for user review.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the controlled exception message and genuine throw frame `GetSolutionStructureTool.ThrowDogfoodFirstPartyProbe`, `GetSolutionStructureTool.cs:160`, followed by `ExecuteCoreAsync` at line 12 and `PluginQueryMcpServerTool.cs:62`. Absolute paths were reduced to filenames. No report was submitted.

### 484. `submit-error-report`

**Activity:** DOGFOOD-013 bundled Core first-party provenance validation.

**Purpose:** Submit the exact reviewed bundled Core probe payload after explicit approval and completion of the MCP consent form.

**Request:** `{"submissionHandle":"<opaque-submission-handle>"}`

**Outcome:** Sentry accepted report reference `406a554a69944b01bf6f21a1fb612879`; the returned digest `9cdd8c5ace3b9c63a4077cea5b9fa3cd4b508b92f98328382803957972fd66df` matched the reviewed complete payload.

### 485. `list-code-actions`

**Activity:** DOGFOOD-013 Code Action first-party provenance validation.

**Purpose:** Trigger the deterministic Code Action workflow failure using the snapshot returned by the initial Workspace load.

**Request:** `{"workspace":{"alias":"dogfood-013-firstparty-probes"},"document":{"project":{"name":"Roslyn.Workbench.Mcp"},"path":"src/Roslyn.Workbench.Mcp/Tools/WorkspaceListTool.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":1,"snapshotId":"<snapshot-id>","transactionRevision":null},"kinds":"All","limit":1}`

**Outcome:** Correctly returned `WorkspaceOutOfDate` because the usage-ledger updates made after the initial load changed a solution document. The Workspace was reloaded before retrying.

### 486. `workspace-reload`

**Activity:** DOGFOOD-013 Code Action first-party provenance validation.

**Purpose:** Refresh the trusted Workspace after the usage-ledger update invalidated its initial snapshot.

**Request:** `{"workspace":{"alias":"dogfood-013-firstparty-probes"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,610 documents.

### 487. `list-code-actions`

**Activity:** DOGFOOD-013 Code Action first-party provenance validation.

**Purpose:** Trigger the deterministic Code Action workflow failure using the refreshed valid snapshot.

**Request:** `{"workspace":{"alias":"dogfood-013-firstparty-probes"},"document":{"project":{"name":"Roslyn.Workbench.Mcp"},"path":"src/Roslyn.Workbench.Mcp/Tools/WorkspaceListTool.cs"},"expectedSnapshot":{"workspaceId":"<workspace-id>","workspaceEpoch":2,"snapshotId":"<snapshot-id>","transactionRevision":null},"kinds":"All","limit":1}`

**Outcome:** Correctly returned `UnhandledException` with correlation ID `<correlation-id>` and both local diagnostic and report preparation available.

### 488. `get-error-details`

**Activity:** DOGFOOD-013 Code Action first-party provenance validation.

**Purpose:** Verify the sensitive local diagnostic captured the genuine Code Action throw point.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the controlled message and first-party frames for `ListCodeActionsTool.ThrowDogfoodFirstPartyProbe` at the absolute local `ListCodeActionsTool.cs:92`, its `ExecuteCoreAsync` caller at line 41 and the Host Code Action adapter at `CodeActionQueryMcpServerTool.cs:54`.

### 489. `prepare-error-report`

**Activity:** DOGFOOD-013 Code Action first-party provenance validation.

**Purpose:** Prepare the complete immutable Sentry payload without network activity for user review.

**Request:** `{"correlationId":"<correlation-id>"}`

**Outcome:** Succeeded with the controlled exception message and genuine throw frame `ListCodeActionsTool.ThrowDogfoodFirstPartyProbe`, `ListCodeActionsTool.cs:92`, followed by `ExecuteCoreAsync` at line 41 and `CodeActionQueryMcpServerTool.cs:54`. Absolute paths were reduced to filenames. No report was submitted.

### 490. `submit-error-report`

**Activity:** DOGFOOD-013 Code Action first-party provenance validation.

**Purpose:** Submit the exact reviewed Code Action probe payload after explicit approval and completion of the MCP consent form.

**Request:** `{"submissionHandle":"<opaque-submission-handle>"}`

**Outcome:** Sentry accepted report reference `764ec3a2c3de4f1e98b180444c87bc8f`; the returned digest `d0725d2f31120b2dab3532b3495fab36de33bc89aae55e700267a56b7087fec1` matched the reviewed complete payload.

### 491. `workspace-close`

**Activity:** DOGFOOD-013 first-party provenance validation cleanup.

**Purpose:** Close the trusted read-only Workspace after all three approved submissions completed.

**Request:** `{"workspace":{"alias":"dogfood-013-firstparty-probes"}}`

**Outcome:** Succeeded and closed the unchanged epoch-2 Workspace with no transaction.

### 492. Candidate `initialize`

**Activity:** DOGFOOD-013 clean candidate restoration.

**Purpose:** Initialise the clean probe-free committed candidate before restoring it as the configured dogfood target.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-013-clean-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18`; the returned Agent Guide source tag identified committed source `dogfood-013-11bc01f`.

### 493. Candidate `server-status`

**Activity:** DOGFOOD-013 clean candidate restoration.

**Purpose:** Verify the probe-free committed candidate retained Sentry prompt consent and normal bundled composition before promotion.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with provider `Sentry`, consent mode `Prompt`, consent state `PromptRequired`, 56 normal tools and only the bundled Core plugin enabled without diagnostics. The candidate contained none of the three disposable failure probes and was then promoted atomically.

### 494. `server-status`

**Activity:** DOGFOOD-013 clean candidate restoration confirmation.

**Purpose:** Confirm the restarted Codex namespace is connected to the clean probe-free committed candidate rather than the disposable provenance build.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with committed version `11bc01f`, provider `Sentry`, consent mode `Prompt`, consent state `PromptRequired`, 56 normal tools and only the bundled Core plugin enabled without diagnostics, confirming the disposable probes and external failure fixture are no longer active.

### 495. `server-status`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Confirm that the restarted task is connected to the controlled HostQuery dogfood candidate before creating a fresh report.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded for commit `11bc01f` with Sentry reporting configured in `Prompt` mode, 58 published tools, the bundled Core plugin and `host.valid.query` enabled, and no server diagnostics.

### 496. `workspace-open`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Open the main solution for a controlled external-plugin query failure whose report can exercise dispatch-time exception-message removal.

**Request:** `{"alias":"dogfood-013-message-free","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,610 documents. Expected isolated-artifacts analyser resolution warnings and the WSL-on-Windows-filesystem warning were reported.

### 497. `host-valid-query`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Trigger the controlled external HostQuery exception containing the known message `Sensitive query failure.`.

**Request:** `{"workspace":{"alias":"dogfood-013-message-free"},"name":"dogfood-013-message-free","throw":true}`

**Outcome:** Returned the expected `UnhandledException` with correlation ID `5c9a862c-89b3-4a70-be72-bc78a20b24ff`.

### 498. `get-error-details`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Inspect the controlled failure locally before preparing the external report.

**Request:** `{"correlationId":"5c9a862c-89b3-4a70-be72-bc78a20b24ff"}`

**Outcome:** Succeeded, but the client-side projection was truncated before the details could be retained for comparison; the request was repeated.

### 499. `get-error-details`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Repeat the local inspection after the first client projection was truncated.

**Request:** `{"correlationId":"5c9a862c-89b3-4a70-be72-bc78a20b24ff"}`

**Outcome:** Succeeded. The local-only details contained `System.InvalidOperationException`, the controlled message `Sensitive query failure.`, an external fixture frame classified as `Unknown`, and the first-party host boundary at `PluginQueryMcpServerTool.cs:62`.

### 500. `prepare-error-report`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Prepare the exact full-content Sentry payload without network activity so the user can review it before choosing the message-free submission option.

**Request:** `{"correlationId":"5c9a862c-89b3-4a70-be72-bc78a20b24ff"}`

**Outcome:** Succeeded with submission handle `submission_3dc6160655014a2fb7db6b398541a8de`, full-payload digest `620785820503fd3b6445c16a36b52409a3ad2da9f77554ab45f92d52d88bd5a8`, expiry `2026-08-29T11:04:13.0528819+00:00`, and the controlled exception message present in both the Sentry exception value and Roslyn Workbench context ready for dispatch-time removal.

### 501. `submit-error-report`

**Activity:** DOGFOOD-013 message-free consent validation.

**Purpose:** Submit the prepared report using the consent form's `Yes, without exception messages` choice and verify that dispatch-time redaction changes the reviewed payload.

**Request:** `{"submissionHandle":"submission_3dc6160655014a2fb7db6b398541a8de"}`

**Outcome:** Succeeded with Sentry reference `e20803dc742c462482f1c747b9b5631d` and the unchanged full-payload digest `620785820503fd3b6445c16a36b52409a3ad2da9f77554ab45f92d52d88bd5a8`. The user subsequently confirmed that the full-content consent option had been selected accidentally, so this is valid full-content evidence rather than a product failure; a fresh report was required for the message-free choice.

### 502. `host-valid-query`

**Activity:** DOGFOOD-013 message-free consent validation retry.

**Purpose:** Generate a fresh controlled failure because the first submission handle was consumed using the full-content consent option.

**Request:** `{"workspace":{"alias":"dogfood-013-message-free"},"name":"dogfood-013-message-free-retry","throw":true}`

**Outcome:** Returned `WorkspaceOutOfDate` with a `workspace-reload` continuation before executing the controlled query.

### 503. `workspace-reload`

**Activity:** DOGFOOD-013 message-free consent validation retry.

**Purpose:** Follow the stale-Workspace continuation before retrying the controlled query failure.

**Request:** `{"workspace":{"alias":"dogfood-013-message-free"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,610 documents. Expected isolated-artifacts analyser resolution warnings were reported.

### 504. `host-valid-query`

**Activity:** DOGFOOD-013 message-free consent validation retry.

**Purpose:** Retry the controlled external HostQuery exception after reloading the Workspace.

**Request:** `{"workspace":{"alias":"dogfood-013-message-free"},"name":"dogfood-013-message-free-retry","throw":true}`

**Outcome:** Returned the expected `UnhandledException` with correlation ID `ec8c1ce2-7149-4870-aa92-97acd41f1161`.

### 505. `get-error-details`

**Activity:** DOGFOOD-013 message-free consent validation retry.

**Purpose:** Inspect the fresh controlled failure locally before preparing its external report.

**Request:** `{"correlationId":"ec8c1ce2-7149-4870-aa92-97acd41f1161"}`

**Outcome:** Succeeded. The local-only details contained `System.InvalidOperationException`, the controlled message `Sensitive query failure.`, an external fixture frame classified as `Unknown`, and the first-party host boundary at `PluginQueryMcpServerTool.cs:62`.

### 506. `prepare-error-report`

**Activity:** DOGFOOD-013 message-free consent validation retry.

**Purpose:** Prepare a fresh full-content Sentry payload for user review before selecting the message-free consent option.

**Request:** `{"correlationId":"ec8c1ce2-7149-4870-aa92-97acd41f1161"}`

**Outcome:** Succeeded with submission handle `submission_81eebdfb92f34b6cba437544ec292797`, full-payload digest `a5268b85c01ec8c312303b0b3e2971ab33dfb3b4ae048789d6c38387adc2d9da`, expiry `2026-08-29T11:09:33.039232+00:00`, and the controlled exception message present in both reviewed message fields ready for dispatch-time removal.

### 507. `submit-error-report`

**Activity:** DOGFOOD-013 message-free consent validation retry.

**Purpose:** Submit the fresh report using the consent form's `Yes, without exception messages` choice and verify dispatch-time redaction.

**Request:** `{"submissionHandle":"submission_81eebdfb92f34b6cba437544ec292797"}`

**Outcome:** Succeeded with Sentry reference `aec10a3ab06744f289076fad03642019` and submitted digest `e32b2cb5ebcf2740afff2de079f7cb0ca5d46e552283ff7726b1366e70ea7009`. The submitted digest differs from the reviewed full-content digest `a5268b85c01ec8c312303b0b3e2971ab33dfb3b4ae048789d6c38387adc2d9da`, confirming that the server constructed and dispatched the message-free payload variant selected by the user.

### 508. `workspace-close`

**Activity:** DOGFOOD-013 message-free consent validation cleanup.

**Purpose:** Close the controlled validation Workspace after the message-free report was accepted.

**Request:** `{"workspace":{"alias":"dogfood-013-message-free"}}`

**Outcome:** Succeeded and closed the unchanged epoch-2 Workspace with no transaction.

### 509. `workspace-list`

**Activity:** DOGFOOD-013 message-free consent validation cleanup.

**Purpose:** Confirm that no Workspace or transaction remained after the validation run.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.


### 510. `server-status`

**Activity:** DOGFOOD-013 clean-candidate restoration verification.

**Purpose:** Confirm that the restarted task is using the restored clean dogfood candidate after removing the controlled external HostQuery fixture from the active setup.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded for commit `11bc01f` with Sentry reporting in `Prompt` mode, 56 published tools, only the bundled Core plugin enabled, no startup warnings and no plugin diagnostics. The temporary `host.valid.query` plugin was absent, confirming restoration of the clean candidate.

## DOGFOOD-014 — Bounded structure document selection

### 511. `workspace-list`

**Activity:** DOGFOOD-014 issue validation.

**Purpose:** Confirm that no Workspace or transaction remained before reproducing bounded structure ordering.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 512. `workspace-open`

**Activity:** DOGFOOD-014 issue validation.

**Purpose:** Open the main solution with an isolated artefacts path for the current committed-behaviour reproduction.

**Request:** `{"alias":"dogfood-014-validation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Failed with `WorkspaceMsBuildPropertiesInvalid` because the isolated artefacts directory did not yet exist. No Workspace was opened; the directory was created before retrying.

### 513. `workspace-open`

**Activity:** DOGFOOD-014 issue validation.

**Purpose:** Retry opening the main solution after satisfying the explicit artefacts-directory precondition.

**Request:** `{"alias":"dogfood-014-validation","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,610 documents. Expected isolated-artifacts analyser warnings and the WSL-on-Windows-filesystem warning were reported.

### 514. `get-solution-structure`

**Activity:** DOGFOOD-014 issue validation.

**Purpose:** Reproduce document selection with deliberately low per-project limits on the current committed build.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"includeDocuments":true,"projectsLimit":2,"documentsPerProjectLimit":2,"projectReferencesPerProjectLimit":2,"foldersLimit":2}`

**Outcome:** Succeeded, but both returned projects used both document slots for generated files under the isolated `obj` tree and returned no ordinary source document.

### 515. `get-project-details`

**Activity:** DOGFOOD-014 issue validation.

**Purpose:** Measure how many bounded slots generated documents consume in a representative project.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"project":{"name":"Roslyn.Workbench.Mcp.Workspace"},"includeDocuments":true,"documentsLimit":5,"projectReferencesLimit":2,"metadataReferencesLimit":2,"analyzersLimit":2}`

**Outcome:** Succeeded. The first three document slots were generated `.NETCoreApp` assembly attributes, assembly information and global-usings files; only slots four and five contained ordinary source documents.

### 516. `search-symbols`

**Activity:** DOGFOOD-014 implementation discovery.

**Purpose:** Locate the solution-structure implementation and focused unit tests.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Solution"},"query":"GetSolutionStructureTool","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production tool and `GetSolutionStructureToolTests`.

### 517. `search-symbols`

**Activity:** DOGFOOD-014 implementation discovery.

**Purpose:** Locate the project-details implementation and focused unit tests.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Solution"},"query":"GetProjectDetailsTool","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production tool and `GetProjectDetailsToolTests`.

### 518. `get-code-context`

**Activity:** DOGFOOD-014 implementation discovery.

**Purpose:** Inspect the solution-structure projection and selection stages.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetSolutionStructureTool.cs"},"range":{"start":284,"length":24}}},"beforeLines":5,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5}`

**Outcome:** Succeeded and showed bounded project selection followed by per-project document projection.

### 519. `get-code-context`

**Activity:** DOGFOOD-014 implementation discovery.

**Purpose:** Inspect project-details document projection.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetProjectDetailsTool.cs"},"range":{"start":209,"length":21}}},"beforeLines":5,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5}`

**Outcome:** Succeeded and showed that all normal Roslyn project documents are normalised, sorted only by path and then bounded.

### 520. `get-code-context`

**Activity:** DOGFOOD-014 existing-coverage validation.

**Purpose:** Inspect the solution-structure unit-test fixture and its document-ordering coverage.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetSolutionStructureToolTests.cs"},"range":{"start":84,"length":29}}},"beforeLines":5,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5}`

**Outcome:** Succeeded. The first test context showed only ordinary in-memory source documents.

### 521. `get-code-context`

**Activity:** DOGFOOD-014 existing-coverage validation.

**Purpose:** Inspect the project-details unit-test fixture and its document-ordering coverage.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetProjectDetailsToolTests.cs"},"range":{"start":124,"length":26}}},"beforeLines":5,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":5}`

**Outcome:** Succeeded. The first test context covered project resolution and ordinary source projection but no mixed generated/source ordering.

### 522. `search-symbols`

**Activity:** DOGFOOD-014 existing-coverage validation.

**Purpose:** Locate focused document-inclusion tests in both affected tool classes.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.Plugins.Core.Test"}]},"query":"IncludeDocuments","kinds":["Method"],"symbolsLimit":30}`

**Outcome:** Succeeded with the no-documents branches and the project-details bounded ordered-details test.

### 523. `search-symbols`

**Activity:** DOGFOOD-014 existing-coverage validation.

**Purpose:** Locate all document-related methods relevant to bounded ordering in the affected test project.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Projects","projects":[{"name":"Roslyn.Workbench.Mcp.Plugins.Core.Test"}]},"query":"Documents","kinds":["Method"],"symbolsLimit":50}`

**Outcome:** Succeeded with 23 methods, including the two affected tools' explicit ordered bounded-document tests.

### 524. `get-code-context`

**Activity:** DOGFOOD-014 existing-coverage validation.

**Purpose:** Inspect the solution-structure bounded-document test in full.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetSolutionStructureToolTests.cs"},"range":{"start":5770,"length":111}}},"beforeLines":5,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3}`

**Outcome:** Succeeded and showed a two-source-document fixture that locks ordinal path order and a one-item bound, but contains no generated document.

### 525. `get-code-context`

**Activity:** DOGFOOD-014 existing-coverage validation.

**Purpose:** Inspect the project-details bounded ordered-details test in full.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetProjectDetailsToolTests.cs"},"range":{"start":5780,"length":124}}},"beforeLines":5,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3}`

**Outcome:** Succeeded and showed three ordinary source documents used to lock ordinal order and limits, with no mixed generated/source case.

### 526. `search-symbols`

**Activity:** DOGFOOD-014 design discovery.

**Purpose:** Find existing repository concepts for generated-document recognition before proposing new classification logic.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Solution"},"query":"Generated","kinds":["NamedType","Method","Property"],"symbolsLimit":100}`

**Outcome:** Succeeded with six results, including `CompilerDiagnosticHelpers.IsGeneratedDocument` and the existing `find-unused-symbols` generated-document option and tests.

### 527. `get-code-context`

**Activity:** DOGFOOD-014 design discovery.

**Purpose:** Inspect the existing generated-document recognition helper.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Diagnostics/CompilerDiagnosticHelpers.cs"},"range":{"start":640,"length":19}}},"beforeLines":10,"afterLines":60,"includeDiagnostics":false,"includeEnclosingSymbols":true,"enclosingSymbolsLimit":3}`

**Outcome:** Succeeded. The helper recognises common generated filename suffixes and temporary generated files but does not classify the generated assembly-attributes or assembly-information files observed in the live response.

### 528. `search-symbols`

**Activity:** DOGFOOD-014 contract discovery.

**Purpose:** Locate the solution-structure request contract before deciding whether a new request option is justified.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"query":"GetSolutionStructureRequest","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the single request contract.

### 529. `search-symbols`

**Activity:** DOGFOOD-014 contract discovery.

**Purpose:** Locate the project-details request contract before deciding whether a new request option is justified.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"query":"GetProjectDetailsRequest","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the single request contract.

### 530. `get-code-context`

**Activity:** DOGFOOD-014 contract discovery.

**Purpose:** Inspect the complete solution-structure input shape and effective-limit pattern.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetSolutionStructureRequest.cs"},"range":{"start":181,"length":27}}},"beforeLines":10,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":false}`

**Outcome:** Succeeded. The contract exposes optional document inclusion and a per-project limit, with no generated-document filter or ordering mode.

### 531. `get-code-context`

**Activity:** DOGFOOD-014 contract discovery.

**Purpose:** Inspect the complete project-details input shape and effective-limit pattern.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"},"location":{"span":{"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetProjectDetailsRequest.cs"},"range":{"start":178,"length":24}}},"beforeLines":10,"afterLines":100,"includeDiagnostics":false,"includeEnclosingSymbols":false}`

**Outcome:** Succeeded. The contract exposes optional document inclusion and a document limit, with no generated-document filter or ordering mode.

### 532. `workspace-close`

**Activity:** DOGFOOD-014 validation cleanup.

**Purpose:** Close the unchanged read-only Workspace after issue, implementation and coverage discovery.

**Request:** `{"workspace":{"alias":"dogfood-014-validation"}}`

**Outcome:** Succeeded and closed the unchanged epoch-1 Workspace with no transaction.

### 533. `workspace-list`

**Activity:** DOGFOOD-014 validation cleanup.

**Purpose:** Confirm that validation left no Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 534. `workspace-open`

**Activity:** DOGFOOD-014 revised design discovery.

**Purpose:** Reopen the clean committed Workspace to trace the shared document-selection boundaries after broadening the requirement from response ordering to agent-facing exclusion.

**Request:** `{"alias":"dogfood-014-boundary","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,610 documents. Expected isolated-artifacts analyser warnings and the WSL-on-Windows-filesystem warning were reported.

### 535. `search-symbols`

**Activity:** DOGFOOD-014 revised design discovery.

**Purpose:** Locate the plugin helper that expands solution and project scopes into documents.

**Request:** `{"workspace":{"alias":"dogfood-014-boundary"},"scope":{"kind":"Solution"},"query":"ResolveDocuments","kinds":["Method"],"symbolsLimit":50}`

**Outcome:** Succeeded and located `ToolRequestResolver.ResolveDocuments<TResponse>` in the plugin execution layer.

### 536. `search-symbols`

**Activity:** DOGFOOD-014 revised design discovery.

**Purpose:** Locate shared direct-document resolution entry points.

**Request:** `{"workspace":{"alias":"dogfood-014-boundary"},"scope":{"kind":"Solution"},"query":"ResolveDocument","kinds":["Method"],"symbolsLimit":100}`

**Outcome:** Succeeded and identified `WorkspaceResolver`, plugin request-resolution and Code Action request-resolution paths.

### 537. `search-symbols`

**Activity:** DOGFOOD-014 revised design discovery.

**Purpose:** Locate every document-reference projection boundary that could expose an intermediate document.

**Request:** `{"workspace":{"alias":"dogfood-014-boundary"},"scope":{"kind":"Solution"},"query":"CreateDocumentReference","kinds":["Method"],"symbolsLimit":100}`

**Outcome:** Succeeded and identified the shared `IWorkspaceResolver.CreateDocumentReference` operation and its consumers in query, Code Action and transaction paths.

### 538. `search-symbols`

**Activity:** DOGFOOD-014 revised design discovery.

**Purpose:** Trace the public selector contract and all document-selector consumers.

**Request:** `{"workspace":{"alias":"dogfood-014-boundary"},"scope":{"kind":"Solution"},"query":"DocumentSelector","kinds":["NamedType","Method","Property"],"symbolsLimit":100}`

**Outcome:** Succeeded and confirmed that direct plugin and Code Action document selections converge on the Workspace resolver.

### 539. `search-symbols`

**Activity:** DOGFOOD-014 revised design discovery.

**Purpose:** Locate the Code Action execution context and scope-resolution boundary.

**Request:** `{"workspace":{"alias":"dogfood-014-boundary"},"scope":{"kind":"Solution"},"query":"CodeActionQueryContext","kinds":["NamedType","Method","Property"],"symbolsLimit":100}`

**Outcome:** Succeeded and identified the Code Action scope resolver and execution-context paths that enumerate project documents.

### 540. Candidate `initialize`

**Activity:** DOGFOOD-014 pre-commit publication.

**Purpose:** Confirm that the isolated Release-published candidate starts and negotiates the supported MCP protocol before promotion.

**Request:** MCP `initialize` using protocol version `2025-06-18` and a local dogfood smoke-test client identity.

**Outcome:** Succeeded. The candidate returned server identity `Roslyn.Workbench.Mcp` version `1.0.0.0` and protocol version `2025-06-18`.

### 541. Candidate `tools/list`

**Activity:** DOGFOOD-014 pre-commit publication.

**Purpose:** Confirm that the isolated candidate publishes its complete tool catalogue before promotion.

**Request:** MCP `tools/list` after the successful initialisation exchange.

**Outcome:** Succeeded with 56 published tools. The candidate then shut down normally and was promoted atomically to the configured dogfood `current` target.

### 542. `server-status`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Confirm that the restarted Codex task is connected to the newly published dogfood candidate and that its full tool catalogue loaded successfully.

**Request:** `{"detailLevel":"Full"}`

**Outcome:** Succeeded. The server reported version `1.0.0.0`, 56 tools, the expected `11bc01f` build identity and no startup warnings.

### 543. `workspace-list`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Confirm a clean dogfood server state before loading the validation Workspace.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 544. `workspace-open`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Load the repository with isolated build artifacts for live validation of agent-addressable document filtering.

**Request:** `{"alias":"dogfood-014-live","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Failed because the requested isolated artifacts directory did not yet exist. The directory was created before retrying.

### 545. `workspace-open`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Retry loading the repository after creating the isolated artifacts directory.

**Request:** `{"alias":"dogfood-014-live","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,614 documents. Expected isolated-artifacts analyser-reference warnings and the WSL-on-Windows-filesystem warning were reported.

### 546. `get-solution-structure`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Inspect the full agent-facing document projection for intermediate `obj` documents.

**Request:** `{"workspace":{"alias":"dogfood-014-live"},"includeDocuments":true,"documentsPerProjectLimit":100}`

**Outcome:** Succeeded, but the complete response exceeded the client model-context limit and was truncated before it could provide reliable inspection evidence. A compact projection was requested subsequently.

### 547. `get-project-details`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Verify that a large page from the main server project's agent-facing document projection excludes physical `obj`-segment documents.

**Request:** `{"workspace":{"alias":"dogfood-014-live"},"project":{"name":"Roslyn.Workbench.Mcp"},"includeDocuments":true,"documentsLimit":100}`

**Outcome:** Succeeded and returned 100 source documents with continuation available. None of the returned paths contained an `obj` segment.

### 548. `get-solution-structure`

**Activity:** DOGFOOD-014 live pre-commit validation.

**Purpose:** Verify the solution-wide first-page projection at the boundary where generated documents had previously displaced source documents.

**Request:** `{"workspace":{"alias":"dogfood-014-live"},"includeDocuments":true,"documentsPerProjectLimit":5}`

**Outcome:** Succeeded for all 31 projects. No returned document contained an `obj` path segment; the first pages contained addressable source documents.

### 549. `workspace-close`

**Activity:** DOGFOOD-014 live pre-commit validation cleanup.

**Purpose:** Close the validation Workspace after collecting live evidence.

**Request:** `{"workspace":{"alias":"dogfood-014-live"}}`

**Outcome:** Succeeded and closed the repository solution at Workspace epoch 1.

### 550. `workspace-list`

**Activity:** DOGFOOD-014 live pre-commit validation cleanup.

**Purpose:** Confirm that validation left no Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 551. `workspace-open`

**Activity:** DOGFOOD-015 issue and existing-coverage validation.

**Purpose:** Load the current committed repository to measure the live `get-document-options` response on a representative source document.

**Request:** `{"alias":"dogfood-015-discovery","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,614 documents. Expected isolated-artifacts analyser-reference warnings and the WSL-on-Windows-filesystem warning were reported.

### 552. `get-document-options`

**Activity:** DOGFOOD-015 issue validation.

**Purpose:** Measure the size and composition of the effective analyser-config projection for a representative production document.

**Request:** `{"workspace":{"alias":"dogfood-015-discovery"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"}}`

**Outcome:** Succeeded with 120 effective analyser-config options. The structured result occupied approximately 9,557 JSON characters, of which the options map occupied 8,223 characters, or 86%. The remaining language, nullable, parse and config-path information was accurate and comparatively small.

### 553. `workspace-close`

**Activity:** DOGFOOD-015 issue-validation cleanup.

**Purpose:** Close the unchanged read-only Workspace after collecting representative response-size evidence.

**Request:** `{"workspace":{"alias":"dogfood-015-discovery"}}`

**Outcome:** Succeeded and closed the repository solution at Workspace epoch 2.

### 554. Candidate `initialize`

**Activity:** DOGFOOD-015 pre-commit publication.

**Purpose:** Confirm that the isolated Release-published candidate starts and negotiates the supported MCP protocol before promotion.

**Request:** MCP `initialize` using protocol version `2025-06-18` and a local dogfood smoke-test client identity.

**Outcome:** The candidate handled the request successfully according to its Host log, but closing piped input immediately prevented the compact response filter from exposing the protocol result. The smoke exchange was retried with an interactive client.

### 555. Candidate `tools/list`

**Activity:** DOGFOOD-015 pre-commit publication.

**Purpose:** Confirm that the isolated candidate publishes its complete tool catalogue before promotion.

**Request:** MCP `tools/list` submitted in the same piped smoke exchange.

**Outcome:** The candidate handled the request successfully according to its Host log, but the compact response filter exposed no protocol result after piped input closed. The smoke exchange was retried with an interactive client.

### 556. Candidate `initialize`

**Activity:** DOGFOOD-015 pre-commit publication diagnostics.

**Purpose:** Retry the candidate initialisation without response filtering to determine why the first smoke client exposed no protocol result.

**Request:** MCP `initialize` using protocol version `2025-06-18` and the same local dogfood smoke-test identity.

**Outcome:** The candidate again handled the request successfully according to its Host log, but closing piped input immediately still prevented the protocol response from appearing in the client projection.

### 557. Candidate `tools/list`

**Activity:** DOGFOOD-015 pre-commit publication diagnostics.

**Purpose:** Retry catalogue publication without response filtering.

**Request:** MCP `tools/list` submitted in the same unfiltered piped exchange.

**Outcome:** The candidate again handled the request successfully according to its Host log, but the response was not exposed after piped input closed. The next retry kept the client input open until each response completed.

### 558. Candidate `initialize`

**Activity:** DOGFOOD-015 pre-commit publication.

**Purpose:** Perform an ordered interactive initialisation exchange that keeps the client input open until the protocol response is received.

**Request:** MCP `initialize` using protocol version `2025-06-18` and the local dogfood smoke-test identity.

**Outcome:** Succeeded. The candidate returned protocol version `2025-06-18` and server identity `Roslyn.Workbench.Mcp` version `1.0.0.0`.

### 559. Candidate `tools/list`

**Activity:** DOGFOOD-015 pre-commit publication.

**Purpose:** Confirm catalogue publication after the successful ordered initialisation exchange.

**Request:** MCP `tools/list` after the `notifications/initialized` notification.

**Outcome:** Succeeded with the complete tool catalogue, including the revised `get-document-options` input schema. The candidate then shut down normally and was promoted atomically to the configured dogfood `current` target; the active executable hash matched the smoke-tested candidate.

### 560. `server-status`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Confirm that the restarted Codex task is connected to the promoted candidate.

**Request:** `{"detailLevel":"Full"}`

**Outcome:** Rejected because the published request contract uses `detail`, not `detailLevel`. The corrected request followed immediately.

### 561. `server-status`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Confirm the promoted candidate identity and startup state after correcting the request property.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with server version `1.0.0.0`, 56 tools, bundled-plugin build identity `e80c796`, no startup warnings and output schemas configured as omitted.

### 562. `workspace-open`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Load the repository with isolated artifacts for live document-options response validation.

**Request:** `{"alias":"dogfood-015-live","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Failed because the requested isolated artifacts directory did not yet exist. The directory was created before retrying.

### 563. `workspace-open`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Retry loading the repository after creating the isolated artifacts directory.

**Request:** `{"alias":"dogfood-015-live","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,614 documents. Expected isolated-artifacts analyser-reference warnings and the WSL-on-Windows-filesystem warning were reported.

### 564. `get-document-options`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Validate the concise default response on a representative production document.

**Request:** `{"workspace":{"alias":"dogfood-015-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"}}`

**Outcome:** Succeeded with no option keys and no option values while retaining language, parse and three config-path inputs. The structured result occupied approximately 1,352 JSON characters, down from the original 9,557-character response.

### 565. `get-document-options`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Validate keys-only discovery without returning effective option values.

**Request:** `{"workspace":{"alias":"dogfood-015-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeAnalyzerConfigOptionKeys":true}`

**Outcome:** Succeeded with 120 ordered option names and no option values. The structured result occupied approximately 7,839 JSON characters: smaller than the complete map, but only about 18% smaller because the option names themselves are long.

### 566. `get-document-options`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Validate targeted value retrieval, including omission of unknown requested keys.

**Request:** `{"workspace":{"alias":"dogfood-015-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"analyzerConfigOptionKeys":["build_property.targetframework","dotnet_diagnostic.RWMCP014.severity","missing_option"]}`

**Outcome:** Succeeded with only `build_property.targetframework: net10.0`; the unset diagnostic key and unknown key were omitted. No discovery keys were returned, and the structured result occupied approximately 1,394 JSON characters.

### 567. `get-document-options`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Confirm rejection of conflicting targeted and complete-map selection.

**Request:** `{"workspace":{"alias":"dogfood-015-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"analyzerConfigOptionKeys":["build_property.targetframework"],"includeAllAnalyzerConfigOptions":true}`

**Outcome:** Rejected with `InvalidRequest` and an actionable message requiring the caller to choose named keys or the complete-map option, not both.

### 568. `get-document-options`

**Activity:** DOGFOOD-015 live pre-commit validation.

**Purpose:** Confirm that explicit complete-map retrieval remains available.

**Request:** `{"workspace":{"alias":"dogfood-015-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeAllAnalyzerConfigOptions":true}`

**Outcome:** Succeeded with 120 option values, no duplicate discovery-key list and a structured result of approximately 9,573 JSON characters.

### 569. `workspace-close`

**Activity:** DOGFOOD-015 live pre-commit validation cleanup.

**Purpose:** Close the validation Workspace after collecting all response-shape evidence.

**Request:** `{"workspace":{"alias":"dogfood-015-live"}}`

**Outcome:** Succeeded and closed the repository solution at Workspace epoch 1.

### 570. `workspace-list`

**Activity:** DOGFOOD-015 live pre-commit validation cleanup.

**Purpose:** Confirm that validation left no Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 571. `workspace-open`

**Activity:** DOGFOOD-015 full-payload inspection.

**Purpose:** Reopen the repository so the user could inspect a concrete complete document-options payload after the live size comparison showed that keys-only discovery remained large.

**Request:** `{"alias":"dogfood-015-payload","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,614 documents. Expected isolated-artifacts analyser-reference warnings and the WSL-on-Windows-filesystem warning were reported.

### 572. `get-document-options`

**Activity:** DOGFOOD-015 full-payload inspection.

**Purpose:** Retrieve the exact complete payload for a representative production document so the response composition could be assessed directly.

**Request:** `{"workspace":{"alias":"dogfood-015-payload"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeAllAnalyzerConfigOptions":true}`

**Outcome:** Succeeded with C# language and parse settings, three applied config paths and 120 effective options. Most of the payload consisted of long naming-rule and naming-style key/value groups, while a smaller portion contained build properties, formatting and C# style values.

### 573. `workspace-close`

**Activity:** DOGFOOD-015 full-payload inspection cleanup.

**Purpose:** Close the read-only Workspace after capturing the requested example.

**Request:** `{"workspace":{"alias":"dogfood-015-payload"}}`

**Outcome:** Succeeded and closed the repository solution at Workspace epoch 2.

### 574. `initialize`

**Activity:** Revised DOGFOOD-015 candidate smoke validation.

**Purpose:** Start the newly published revised candidate and confirm MCP protocol negotiation before promotion.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-015-revised-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18` and server version `1.0.0.0`.

### 575. `tools/list`

**Activity:** Revised DOGFOOD-015 candidate smoke validation.

**Purpose:** Confirm that the newly published candidate could enumerate its MCP catalogue before promotion.

**Request:** `{}`

**Outcome:** Succeeded, but the raw 56-tool response exceeded the client display limit, so a compact schema-focused retry was used for auditable verification.

### 576. `initialize`

**Activity:** Revised DOGFOOD-015 candidate schema verification.

**Purpose:** Start a clean candidate process for a compact, machine-filtered check of the revised request schema.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"dogfood-015-revised-schema-smoke","version":"1.0"}}`

**Outcome:** Succeeded with protocol version `2025-06-18`.

### 577. `tools/list`

**Activity:** Revised DOGFOOD-015 candidate schema verification and promotion.

**Purpose:** Verify the published `get-document-options` request contract without retaining the full tool catalogue in the client output.

**Request:** `{}`

**Outcome:** Succeeded with 56 tools. `get-document-options` exposed required `document`, optional `workspace`, and the two boolean fields `includeParseOptions` and `includeAnalyzerConfig`; the rejected named-key, keys-only and full-selection fields were absent. The verified candidate was then atomically promoted to `current`, and the promoted executable hash matched the candidate (`83e048004cf5aa2d7a4f6204109897a994dec2290b2358050722017e1a0c773d`).

### 578. `workspace-open`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Open the repository with isolated build artifacts before exercising the revised response modes.

**Request:** `{"alias":"dogfood-015-revised-live","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<non-existent-temporary-artifacts-root>"}}`

**Outcome:** Failed with `WorkspaceMsBuildPropertiesInvalid` because the requested absolute artifacts directory did not yet exist.

### 579. `workspace-open`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Retry the repository load with an existing isolated artifacts directory.

**Request:** `{"alias":"dogfood-015-revised-live","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<temporary-artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,614 documents. Expected isolated-artifacts analyser-reference warnings and the WSL-on-Windows-filesystem warning were reported.

### 580. `get-document-options`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Verify that the default response contains only the generally useful document context.

**Request:** `{"workspace":{"alias":"dogfood-015-revised-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"}}`

**Outcome:** Succeeded with `document`, language version `14.0` and nullable context `Enable`; both `parseOptions` and `analyzerConfig` were null.

### 581. `get-document-options`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Verify the parse-options-only response mode.

**Request:** `{"workspace":{"alias":"dogfood-015-revised-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeParseOptions":true}`

**Outcome:** Succeeded with the default document context plus C# parse options and 18 preprocessor symbols; `analyzerConfig` remained null.

### 582. `get-document-options`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Verify the analyzer-config-only response mode while the other independent response reads were in progress.

**Request:** `{"workspace":{"alias":"dogfood-015-revised-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeAnalyzerConfig":true}`

**Outcome:** Returned retryable `WorkspaceBusy` with `RetryRequest` because concurrent workspace reads were already active.

### 583. `get-document-options`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Retry the unchanged analyzer-config-only request sequentially as instructed by the continuation.

**Request:** `{"workspace":{"alias":"dogfood-015-revised-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeAnalyzerConfig":true}`

**Outcome:** Succeeded with the default document context and 120 analyzer-config options; `parseOptions` remained null.

### 584. `get-document-options`

**Activity:** Revised DOGFOOD-015 live post-restart validation.

**Purpose:** Verify that both explicit payload sections can be requested together.

**Request:** `{"workspace":{"alias":"dogfood-015-revised-live"},"document":{"path":"src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetDocumentOptionsTool.cs"},"includeParseOptions":true,"includeAnalyzerConfig":true}`

**Outcome:** Succeeded with both parse options and analyzer config populated, including 18 preprocessor symbols and 120 analyzer-config options.

### 585. `workspace-close`

**Activity:** Revised DOGFOOD-015 live post-restart validation cleanup.

**Purpose:** Close the validation Workspace after collecting the revised response-shape evidence.

**Request:** `{"workspace":{"alias":"dogfood-015-revised-live"}}`

**Outcome:** Succeeded and closed the repository solution at Workspace epoch 1.

### 586. `workspace-list`

**Activity:** Revised DOGFOOD-015 live post-restart validation cleanup.

**Purpose:** Confirm that validation left no Workspace or transaction owner.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 587. `workspace-list`

**Activity:** DOGFOOD-016 staged external review.

**Purpose:** Determine whether the configured published dogfood server already had a loaded Workspace available for Roslyn-backed read-only inspection.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner. The reviewer made no further dogfood requests because opening a Workspace would have changed server state during a read-only review.

### 588. `workspace-list`

**Activity:** DOGFOOD-016 post-restart tooling-data validation.

**Purpose:** Confirm that the restarted configured dogfood server was active and had clean Workspace state before inspecting its published schemas.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 589. `initialize`

**Activity:** DOGFOOD-016 raw protocol tooling-data validation.

**Purpose:** Inspect the configured published executable's unprojected MCP tool schemas rather than relying on Codex's normalised tool declarations.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"schema-verifier","version":"1.0"}}`

**Outcome:** The independently launched server stopped before initialisation because its default recovery directory was not writable in the validation sandbox. No schema evidence was obtained.

### 590. `initialize` / `tools/list`

**Activity:** DOGFOOD-016 raw protocol tooling-data validation retry.

**Purpose:** Retry schema inspection with an isolated temporary state directory.

**Request:** Initialisation used protocol version `2025-06-18` and client `schema-verifier/1.0`, followed by an empty `tools/list` request.

**Outcome:** The first isolated-state pipeline returned no captured protocol response, so the result was inconclusive and the approach was replaced with an interactive protocol probe.

### 591. `initialize`

**Activity:** DOGFOOD-016 raw protocol tooling-data validation retry.

**Purpose:** Diagnose the missing response from the isolated-state pipeline by keeping the server process open while reading its initialisation response.

**Request:** `{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"schema-verifier","version":"1.0"}}`

**Outcome:** No initialisation response was captured because the reused temporary state directory did not have the server's required private permissions.

### 592. `initialize` / `tools/list`

**Activity:** DOGFOOD-016 raw protocol tooling-data validation retry.

**Purpose:** Retry schema inspection after correcting the isolated state directory permissions.

**Request:** Initialisation used protocol version `2025-06-18` and client `schema-verifier/1.0`, followed by an empty `tools/list` request.

**Outcome:** The server emitted a tools-list response, but the diagnostic pipeline produced more output than could be retained reliably. A focused schema verifier was used next.

### 593. `initialize` / `tools/list`

**Activity:** DOGFOOD-016 focused raw protocol tooling-data validation.

**Purpose:** Verify representative server, bundled Core-plugin and Code Action input and output property descriptions in the restarted publication.

**Request:** Initialisation used protocol version `2025-06-18` and client `schema-verifier/1.0`, followed by an empty `tools/list` request. The verifier sampled `workspace-open`, `find-callees`, `get-control-flow-graph`, `search-symbols`, `transaction-preview`, `list-code-actions` and `server-status`.

**Outcome:** Initialisation succeeded against server version `1.0.0.0` and `tools/list` returned 56 tools. Every sampled DOGFOOD-016 property description was absent and none of the sampled tools contained an `outputSchema`. The configured `current` symlink also resolved to the older `dogfood-015-revised-precommit-e80c796-20260829-201636` publication, confirming that the restarted process does not contain the new tooling data.

### 594. Candidate `initialize`

**Activity:** DOGFOOD-016 committed publication.

**Purpose:** Confirm that the isolated Release candidate published from committed `HEAD` starts and negotiates the supported MCP protocol before promotion.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-016-smoke/1.0`.

**Outcome:** Succeeded against candidate `dogfood-016-27dce5b-20260830-091107`. The server returned identity `Roslyn.Workbench.Mcp` version `1.0.0.0` and protocol version `2025-06-18`.

### 595. Candidate `tools/list`

**Activity:** DOGFOOD-016 committed publication.

**Purpose:** Verify representative server, bundled Core-plugin and Code Action input descriptions under the normal output-schema configuration before promotion.

**Request:** Empty MCP `tools/list` request after successful initialisation. The verifier sampled `workspace-open`, `find-callees`, `search-symbols`, `transaction-preview` and `list-code-actions`.

**Outcome:** Succeeded with 56 published tools. Every sampled input description matched the committed DOGFOOD-016 contract text, and output schemas were omitted as configured by default.

### 596. Candidate `initialize`

**Activity:** DOGFOOD-016 optional output-schema validation.

**Purpose:** Start the isolated candidate with full output-schema publication enabled so the configured response-contract path could also be verified.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-016-smoke/1.0`; the server was started with `--tool-output-schema-mode Full`.

**Outcome:** Succeeded against candidate `dogfood-016-27dce5b-20260830-091107` with protocol version `2025-06-18`.

### 597. Candidate `tools/list`

**Activity:** DOGFOOD-016 optional output-schema validation.

**Purpose:** Confirm that full output schemas contain the committed agent-facing response and continuation descriptions.

**Request:** Empty MCP `tools/list` request after successful initialisation. The verifier sampled `transaction-preview` and `server-status` response descriptions.

**Outcome:** Succeeded with 56 published tools, all of which contained an `outputSchema`. The sampled response-property and continuation descriptions matched the committed DOGFOOD-016 contract text. The verified candidate was then promoted atomically to the configured dogfood `current` target.

### 598. `server-status`

**Activity:** DOGFOOD-016 live post-restart tooling-data validation.

**Purpose:** Confirm that the restarted Codex task is connected to the promoted committed candidate before inspecting Codex's normal callable declarations.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, 56 tools, no startup warnings and bundled Core plugin version `1.0.0+27dce5b4ffc4f0767f46ffb9865164cce1255027`, confirming the live task is connected to the DOGFOOD-016 commit. Codex exposed the expected property descriptions for representative server, Core-plugin and Code Action declarations, but compacted 17 parameterised complex declarations without any property comments, including `find-callees` and `get-control-flow-graph`; their raw MCP schemas retain the descriptions. `workspace-list` also has no property comments because its request is intentionally empty.

### 599. Published `initialize`

**Activity:** DOGFOOD-016 client-neutral input-schema budget analysis.

**Purpose:** Start an isolated instance of the promoted publication so raw input-schema size and complexity could be compared with Codex's rich and compact callable declarations.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-016-schema-budget/1.0`.

**Outcome:** Succeeded against the promoted DOGFOOD-016 publication.

### 600. Published `tools/list`

**Activity:** DOGFOOD-016 client-neutral input-schema budget analysis.

**Purpose:** Measure serialized UTF-8 size, description volume, described property occurrences, object count and nesting depth for every published input schema.

**Request:** Empty MCP `tools/list` request after successful initialisation.

**Outcome:** Succeeded with 56 tools. Excluding the intentionally empty `workspace-list`, Codex's richly annotated declarations ranged from 154 to 5,113 input-schema bytes, while every compacted declaration ranged from 5,211 to 7,672 bytes. The largest rich schema contained 38 described property occurrences and 2,473 description characters; the smallest compact schema contained 39 described property occurrences and 2,517 description characters. This clean boundary supports a client-neutral target of at most 4,096 serialized input-schema bytes, leaving roughly 20% headroom below the observed Codex boundary.

### 601. Published `initialize`

**Activity:** DOGFOOD-016 input-schema reduction feasibility analysis.

**Purpose:** Start another isolated promoted instance to distinguish description cost from unavoidable schema structure before selecting a portable size target.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-016-schema-shape/1.0`.

**Outcome:** Succeeded against the promoted DOGFOOD-016 publication.

### 602. Published `tools/list`

**Activity:** DOGFOOD-016 input-schema reduction feasibility analysis.

**Purpose:** Compare representative rich and compact schemas with every description annotation removed and inspect their reference structure.

**Request:** Empty MCP `tools/list` request after successful initialisation. The comparison sampled `get-symbol-attributes`, `find-callers`, `go-to-definition` and `search-symbols`.

**Outcome:** Succeeded. The 7,672-byte `find-callers` schema fell to 2,962 bytes without description annotations, while 5,211-byte `get-symbol-attributes` fell to 2,031 bytes; neither contained `$defs`, and their repeated project selector was already represented by a local reference. Because retaining a description key for every property has substantial fixed JSON overhead, a 4,096-byte hard cap would leave too little room for meaningful descriptions on the largest contracts without further structural redesign. The earlier 4,096-byte recommendation is therefore provisional rather than an adopted limit.

### 603. `workspace-list`

**Activity:** DOGFOOD-016 portable input-schema budget implementation.

**Purpose:** Confirm clean published Workspace state before inspecting the plugin authoring-warning paths through Roslyn-backed queries.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 604. `workspace-open`

**Activity:** DOGFOOD-016 portable input-schema budget implementation.

**Purpose:** Load the repository with isolated build artifacts for Roslyn-backed inspection of schema publication and existing plugin authoring warnings.

**Request:** `{"alias":"dogfood-016-budget","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<temporary-artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,617 documents. The expected isolated-artifacts analyser-reference warnings and WSL-on-Windows-filesystem warning were reported.

### 605. `search-symbols`

**Activity:** DOGFOOD-016 portable input-schema budget implementation.

**Purpose:** Locate the existing query-response authoring inspector and application-logging path to keep the new oversized-input-schema warning consistent with established plugin behaviour.

**Request:** `{"workspace":{"alias":"dogfood-016-budget"},"scope":{"kind":"Solution"},"query":"QueryResponseContract","kinds":["NamedType","Method","Property"],"symbolsLimit":100}`

**Outcome:** Succeeded and identified `QueryResponseContractInspector`, its tests, and `PluginCatalogEntryMaterializer.ReportQueryResponseContractWarnings` with its source-generated warning log.

### 606. `workspace-close`

**Activity:** DOGFOOD-016 portable input-schema budget implementation cleanup.

**Purpose:** Close the Roslyn-backed inspection Workspace after the relevant authoring-warning paths had been located.

**Request:** `{"workspace":{"alias":"dogfood-016-budget"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 1.

### 607. `workspace-list`

**Activity:** DOGFOOD-016 central snapshot-description design follow-up.

**Purpose:** Confirm the configured dogfood server had no loaded Workspace before deciding whether Roslyn-backed symbol inspection was available for the schema-generator design audit.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner. The design audit therefore continued against the local source without opening a Workspace because the relevant schema and contract files were already known and no Roslyn semantic refactor was yet required.

### 608. `workspace-list`

**Activity:** DOGFOOD-016 type-owned input-description candidate audit.

**Purpose:** Check whether a Workspace was already loaded before auditing request property types for other descriptions that could be published centrally.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner. The audit continued against the local contract source because no semantic refactor or call-site analysis was required.

### 609. `workspace-open`

**Activity:** DOGFOOD-016 cross-member input-schema composition discovery.

**Purpose:** Load the repository for Roslyn-backed inspection of the validation attributes and schema publication path.

**Request:** `{"alias":"dogfood-016-schema-composition","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<non-existent-temporary-artifacts-root>"}}`

**Outcome:** Failed with `WorkspaceMsBuildPropertiesInvalid` because the requested artifacts directory did not yet exist.

### 610. `workspace-open`

**Activity:** DOGFOOD-016 cross-member input-schema composition discovery retry.

**Purpose:** Load the repository with an existing isolated artifacts directory for Roslyn-backed inspection.

**Request:** `{"alias":"dogfood-016-schema-composition","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<temporary-artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,618 documents. The expected isolated-artifacts analyser-reference warnings and WSL-on-Windows-filesystem warning were reported.

### 611. `search-symbols`

**Activity:** DOGFOOD-016 cross-member input-schema composition discovery.

**Purpose:** Locate `RequiresExactlyOneAttribute` and its existing validation coverage before changing schema publication.

**Request:** `{"workspace":{"alias":"dogfood-016-schema-composition"},"scope":{"kind":"Solution"},"query":"RequiresExactlyOne","kinds":["NamedType","Method"],"symbolsLimit":50}`

**Outcome:** Succeeded and located the public validation attribute and its Workspace validation tests. Source inspection then established that the rule uses value-based “provided” semantics rather than JSON member presence.

### 612. `workspace-close`

**Activity:** DOGFOOD-016 cross-member input-schema composition discovery cleanup.

**Purpose:** Close the inspection Workspace after discovering that the approved required-property-only schema did not match runtime value-presence semantics.

**Request:** `{"workspace":{"alias":"dogfood-016-schema-composition"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 2.

### 613. Candidate `initialize`

**Activity:** DOGFOOD-016 pre-review cross-member schema validation.

**Purpose:** Start the Release candidate published from the current unstaged working tree and confirm MCP protocol negotiation before promotion.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-016-prereview-smoke/1.0`.

**Outcome:** Succeeded against candidate `dogfood-016-prereview-27dce5b-working-20260830-121151`; the server returned identity `Roslyn.Workbench.Mcp` version `1.0.0.0` and protocol version `2025-06-18`.

### 614. Candidate `tools/list`

**Activity:** DOGFOOD-016 pre-review cross-member schema validation and promotion.

**Purpose:** Confirm that the current working-tree candidate publishes the complete catalogue and the approved compact cross-member rules before using it through Codex's configured dogfood namespace.

**Request:** Empty MCP `tools/list` request after successful initialisation.

**Outcome:** Succeeded with 56 tools. Closed selectors such as `WorkspaceSelector` and `ProjectSelector` used `minProperties: 1`; `search-symbols` used required-property `anyOf` alternatives; exact selectors used required-property `oneOf` alternatives; participating properties rejected explicit JSON `null`; and descriptions no longer repeated the structural rules. The candidate shut down normally and was promoted atomically to `current`; the promoted Host DLL hash matched the smoke-tested candidate (`5423ca93ecab7b3315b67973bcffd2875630392390e5727e195e82be3f5eaa1e`).

### 615. `server-status`

**Activity:** DOGFOOD-016 live pre-review cross-member schema validation.

**Purpose:** Confirm that the restarted Codex task connected successfully to the promoted working-tree candidate before evaluating normal callable declarations.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, 56 tools, no startup warnings and bundled Core plugin version `1.0.0+27dce5b4ffc4f0767f46ffb9865164cce1255027`. Codex's normal declarations projected nested selectors using `minProperties` as named typed properties, but root request schemas containing `anyOf` or `oneOf` were reduced to unions of generic `{ [key: string]: unknown }` objects.

### 616. `workspace-list`

**Activity:** DOGFOOD-016 live pre-review cross-member schema validation.

**Purpose:** Confirm clean live Workspace state before exercising the new selector schemas.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 617. `workspace-open`

**Activity:** DOGFOOD-016 live pre-review cross-member schema validation.

**Purpose:** Load the repository through a normally projected request so representative `minProperties`, `anyOf` and `oneOf` requests could be exercised.

**Request:** `{"alias":"dogfood-016-cross-member-prereview","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<temporary-artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,662 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 618. `search-symbols`

**Activity:** DOGFOOD-016 live `RequiresAtLeastOne` validation.

**Purpose:** Confirm that a request satisfying the published `query` or `metadataName` alternatives and using an alias-only Workspace selector remains callable through Codex.

**Request:** `{"workspace":{"alias":"dogfood-016-cross-member-prereview"},"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production transformer and its test type. The server accepted the structurally valid request, but Codex exposed the complete request as a generic object union rather than named parameters.

### 619. `search-symbols`

**Activity:** DOGFOOD-016 live exactly-one setup.

**Purpose:** Resolve a representative method selector for the subsequent `find-callees` request.

**Request:** `{"workspace":{"alias":"dogfood-016-cross-member-prereview"},"query":"Transform","namespace":"Roslyn.Workbench.Mcp.Protocol","kinds":["Method"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp"}},"symbolsLimit":20}`

**Outcome:** Succeeded with the two schema-transformer methods, including the current working-tree `InputContractSchemaTransformer.Transform` implementation.

### 620. `find-callees`

**Activity:** DOGFOOD-016 live `RequiresExactlyOne` validation.

**Purpose:** Confirm that a request supplying only the `symbol` alternative remains callable and resolves the current transformer implementation.

**Request:** `{"workspace":{"alias":"dogfood-016-cross-member-prereview"},"symbol":{"documentationCommentId":"<InputContractSchemaTransformer.Transform documentation ID>","project":{"projectId":"<host-project-id>"}},"includeIndirect":false,"maxDepth":3,"calleesLimit":20}`

**Outcome:** Succeeded with eight direct callees, including the new cross-member publication helpers. The server accepted the structurally valid exactly-one request, but Codex again exposed the complete request as a generic object union rather than named parameters.

### 621. `workspace-close`

**Activity:** DOGFOOD-016 live pre-review cross-member schema validation cleanup.

**Purpose:** Close the live validation Workspace after the representative rule calls completed.

**Request:** `{"workspace":{"alias":"dogfood-016-cross-member-prereview"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 1. The dogfood result is therefore mixed: compact all-property `minProperties` rules project correctly, while `anyOf` and `oneOf` rules preserve runtime callability but fail the agent-readable TypeScript declaration requirement.

### 622. Candidate `initialize`

**Activity:** DOGFOOD-016 portable cross-member guidance validation.

**Purpose:** Start the revised Release candidate and confirm MCP protocol negotiation before promotion.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-smoke/1.0`.

**Outcome:** Failed before protocol initialisation because the direct sandboxed smoke process could not write to the default durable state directory. The Host returned the actionable requirement to configure a writable `--state-directory`; no candidate defect was indicated.

### 623. Candidate `initialize`

**Activity:** DOGFOOD-016 portable cross-member guidance validation retry.

**Purpose:** Retry candidate startup with an isolated writable state directory.

**Request:** MCP `initialize` using protocol version `2025-06-18`, client identity `dogfood-smoke/1.0` and a temporary state directory.

**Outcome:** Succeeded against candidate `dogfood-016-portable-prereview-27dce5b-working-20260830-134453`; the server returned identity `Roslyn.Workbench.Mcp` version `1.0.0.0` and protocol version `2025-06-18`.

### 624. Candidate `tools/list`

**Activity:** DOGFOOD-016 portable cross-member guidance validation and promotion.

**Purpose:** Confirm that the revised working-tree candidate publishes the complete catalogue with portable cross-member guidance before validating it through Codex's configured dogfood namespace.

**Request:** Empty MCP `tools/list` request after successful initialisation.

**Outcome:** Succeeded with 56 tools. Cross-member contracts retained ordinary named property schemas, published one object-level instruction such as `Provide exactly one of symbol or location.`, rejected explicit JSON `null` for participating supplied values and contained no root `anyOf` or `oneOf` composition. The central `SnapshotPrecondition` instruction remained present. The candidate shut down normally and was promoted atomically to `current`; the promoted executable hash matched the smoke-tested candidate (`83e048004cf5aa2d7a4f6204109897a994dec2290b2358050722017e1a0c773d`).

### 625. `server-status`

**Activity:** DOGFOOD-016 live portable cross-member guidance validation.

**Purpose:** Confirm that the restarted Codex task connected to the promoted portable-guidance candidate before inspecting normal callable declarations.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, 56 tools, no startup warnings and bundled Core plugin version `1.0.0+27dce5b4ffc4f0767f46ffb9865164cce1255027`. Codex projected `search-symbols` and `find-callees` as ordinary named request properties rather than generic `unknown` object unions. It retained property and nested-type descriptions, but omitted each root input-schema `description`, so the new object-level cross-member instruction was not visible in the callable declaration.

### 626. `workspace-list`

**Activity:** DOGFOOD-016 live portable cross-member guidance validation.

**Purpose:** Confirm clean Workspace state before exercising the revised requests.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 627. `workspace-open`

**Activity:** DOGFOOD-016 live portable cross-member guidance validation.

**Purpose:** Load the repository through Codex's normal dogfood declaration so representative at-least-one and exactly-one requests could be exercised.

**Request:** `{"alias":"dogfood-016-portable-prereview","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<temporary-artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,618 documents. The expected isolated-artifacts analyser-reference warnings and WSL-on-Windows-filesystem warning were reported.

### 628. `search-symbols`

**Activity:** DOGFOOD-016 live portable `RequiresAtLeastOne` validation.

**Purpose:** Confirm that an at-least-one request remains understandable and callable through its restored named parameters.

**Request:** `{"workspace":{"alias":"dogfood-016-portable-prereview"},"query":"InputContractSchemaTransformer","kinds":["NamedType"],"symbolsLimit":10}`

**Outcome:** Succeeded with the production transformer and its test type. Codex exposed `query` and `metadataName` as separate typed optional parameters, but did not expose the root `Provide query, metadataName, or both.` schema description.

### 629. `search-symbols`

**Activity:** DOGFOOD-016 live portable exactly-one setup.

**Purpose:** Resolve the current transformer method and its canonical source selector for an exactly-one `find-callees` request.

**Request:** `{"workspace":{"alias":"dogfood-016-portable-prereview"},"query":"Transform","namespace":"Roslyn.Workbench.Mcp.Protocol","kinds":["Method"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp"}},"symbolsLimit":20}`

**Outcome:** Succeeded with the two schema-transformer methods and their current source selectors.

### 630. `find-callees`

**Activity:** DOGFOOD-016 live portable `RequiresExactlyOne` validation.

**Purpose:** Exercise the restored named `symbol` alternative using the returned method identity.

**Request:** `{"workspace":{"alias":"dogfood-016-portable-prereview"},"symbol":{"documentationCommentId":"<InputContractSchemaTransformer.Transform documentation ID>","project":{"projectId":"<host-project-id>"}},"includeIndirect":false,"maxDepth":3,"calleesLimit":20}`

**Outcome:** Rejected with `SymbolNotFound` and an accurate `ReviseRequest` continuation. The callable shape was usable; the returned documentation-comment identity did not resolve when reconstructed as a symbol selector, so the canonical location alternative was used instead.

### 631. `find-callees`

**Activity:** DOGFOOD-016 live portable `RequiresExactlyOne` validation retry.

**Purpose:** Confirm the restored named `location` alternative is accepted without supplying `symbol`.

**Request:** `{"workspace":{"alias":"dogfood-016-portable-prereview"},"location":{"span":{"document":{"project":{"projectId":"<host-project-id>"},"documentId":"<transformer-document-id>"},"range":{"start":610,"length":9}}},"includeIndirect":false,"maxDepth":3,"calleesLimit":20}`

**Outcome:** Succeeded through the normal Codex declaration. The server accepted exactly one top-level alternative and returned a bounded callee result. Codex exposed both `symbol` and `location` with their complete typed nested shapes, but did not expose the root `Provide exactly one of symbol or location.` schema description.

### 632. `workspace-close`

**Activity:** DOGFOOD-016 live portable cross-member guidance validation cleanup.

**Purpose:** Close the validation Workspace after the representative calls completed.

**Request:** `{"workspace":{"alias":"dogfood-016-portable-prereview"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 1.

### 633. Candidate `initialize`

**Activity:** DOGFOOD-016 root input-guidance metadata validation.

**Purpose:** Start the final Release candidate and confirm MCP protocol negotiation before promotion.

**Request:** MCP `initialize` using protocol version `2025-06-18` and client identity `dogfood-016-input-metadata-smoke/1.0` with an isolated temporary state directory.

**Outcome:** Succeeded against candidate `dogfood-016-input-metadata-prereview-27dce5b-working-20260830-141055`; the server returned identity `Roslyn.Workbench.Mcp` version `1.0.0.0` and protocol version `2025-06-18`.

### 634. Candidate `tools/list`

**Activity:** DOGFOOD-016 root input-guidance metadata validation and promotion.

**Purpose:** Confirm the generated request schema remains the single source of cross-member guidance and that the publication pipeline copies its root description into tool metadata.

**Request:** Empty MCP `tools/list` request after successful initialisation.

**Outcome:** Succeeded with 56 tools. `search-symbols` retained `description: "Provide query, metadataName, or both."` on its root input schema and published `Input: Provide query, metadataName, or both.` in its tool description; `get-control-flow-graph` did the same for its exactly-one rule. Ordinary named request properties and nested descriptions remained intact. The candidate shut down normally and was promoted atomically to `current`; the promoted Host DLL hash matched the smoke-tested candidate (`6e17372295df8378e8b14d71ac0f6862fb7473b953d2e323bc084581d79c1ff4`).

### 635. `server-status`

**Activity:** DOGFOOD-016 final live root input-guidance validation.

**Purpose:** Confirm that the restarted Codex task connected to the final candidate and inspect the normal model-visible declarations.

**Request:** `{"detail":"Full"}`

**Outcome:** Succeeded with Host version `1.0.0.0`, 56 tools and no startup warnings. A complete audit of all 56 normal Codex declarations found a typed argument object on every tool, no `unknown` or `unknown & unknown`, no missing or malformed tool prose, no duplicated `Input:` or `Result:` sections and no unexpected root-guidance enrichment. Exactly the three root request contracts with type descriptions gained `Input:` guidance: `search-symbols` advertised `Input: Provide query, metadataName, or both.`, while `find-callees` and `get-control-flow-graph` advertised `Input: Provide exactly one of symbol or location.` Nested selector and snapshot descriptions remained visible. The largest model-visible declaration was `find-callees` at 3,258 characters; the separate automated raw-schema budget audit remained green. This satisfies the portable client-guidance requirement without manually duplicating request text in tool registrations.

### 636. `workspace-list`

**Activity:** DOGFOOD-017 required-string contract audit.

**Purpose:** Confirm clean Workspace state before tracing published contract producers.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces.

### 637. `workspace-open`

**Activity:** DOGFOOD-017 required-string contract audit.

**Purpose:** Load the repository so contract types and their producers could be traced through the published dogfood server.

**Request:** `{"alias":"dogfood-017-audit","path":"<repository-root>/Roslyn.Workbench.Mcp.slnx","workspaceRoot":"<repository-root>","msBuildProperties":{"artifactsPath":"<temporary-artifacts-root>"}}`

**Outcome:** Succeeded at Workspace epoch 1 with 31 projects and 1,618 documents. The expected isolated-artifacts analyser-reference warnings and WSL-on-Windows-filesystem warning were reported.

### 638. `search-symbols`

**Activity:** DOGFOOD-017 required-string contract audit.

**Purpose:** Resolve `CompilationOptionsInfo` as the representative contract whose empty defaults could conceal unavailable Roslyn options.

**Request:** `{"workspace":{"alias":"dogfood-017-audit"},"query":"CompilationOptionsInfo","kinds":["NamedType"],"scope":{"kind":"Project","project":{"name":"Roslyn.Workbench.Mcp.Plugins.Core"}},"symbolsLimit":10}`

**Outcome:** Succeeded with the production contract and its canonical project and document identities.

### 639. `find-references`

**Activity:** DOGFOOD-017 required-string contract audit.

**Purpose:** Trace all consumers and producers of `CompilationOptionsInfo`.

**Request:** The request supplied the resolved symbol, solution scope, a 50-reference limit, context and the unsupported `includeDeclarations` and `classifyAccess` properties.

**Outcome:** Rejected as `InvalidRequest` because `includeDeclarations` is not part of the published request contract. The failed call was retained as usage evidence and the retry used `includeDefinitions`.

### 640. `find-references`

**Activity:** DOGFOOD-017 required-string contract audit retry.

**Purpose:** Trace all consumers and producers using the published request shape.

**Request:** `{"workspace":{"alias":"dogfood-017-audit"},"symbol":{"documentationCommentId":"<CompilationOptionsInfo documentation ID>","project":{"projectId":"<Core-plugin-project-id>"}},"scope":{"kind":"Solution"},"includeDefinitions":true,"includeContext":true,"referencesLimit":50}`

**Outcome:** Succeeded, but the context-rich result exceeded the client projection limit and was truncated. It still established that the contract is owned by `ProjectDetailsData`, constructed by `InspectionProjectionFactory` and covered by projection tests; a bounded retry captured the complete set.

### 641. `find-references`

**Activity:** DOGFOOD-017 required-string contract audit bounded retry.

**Purpose:** Capture the complete reference set without unnecessary source context.

**Request:** `{"workspace":{"alias":"dogfood-017-audit"},"symbol":{"documentationCommentId":"<CompilationOptionsInfo documentation ID>","project":{"projectId":"<Core-plugin-project-id>"}},"scope":{"kind":"Solution"},"includeDefinitions":true,"includeContext":false,"referencesLimit":20}`

**Outcome:** Succeeded with all eight references and no continuation. The result confirmed that the nullable `ProjectDetailsData.CompilationOptions` property is populated only through `InspectionProjectionFactory`, while the null-options unit test currently expects an all-default object.

### 642. `workspace-close`

**Activity:** DOGFOOD-017 required-string contract audit cleanup.

**Purpose:** Close the audit Workspace after producer tracing completed.

**Request:** `{"workspace":{"alias":"dogfood-017-audit"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 1.

### 643. `workspace-list`

**Activity:** Internal XML-documentation work scoping.

**Purpose:** Confirm clean dogfood state before measuring the production symbol surface.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner.

### 644. `workspace-open`

**Activity:** Internal XML-documentation work scoping.

**Purpose:** Load the solution for semantic production-surface inspection.

**Request:** Opened `<repository-root>/Roslyn.Workbench.Mcp.slnx` as `internal-xml-doc-scope` with the repository root and isolated artifacts directory.

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,663 documents. The expected WSL-on-Windows-filesystem warning was reported.

### 645. `search-symbols`

**Activity:** Internal XML-documentation enforcement discovery.

**Purpose:** Find an existing documentation-policy analyser or source-governance type.

**Request:** Searched the solution for named types containing `Documentation` with a limit of 50.

**Outcome:** Succeeded with no matching named type.

### 646. `search-symbols`

**Activity:** Internal XML-documentation enforcement discovery.

**Purpose:** Find existing public-API governance tests that could inform enforcement ownership.

**Request:** Searched the solution for named types containing `PublicApi` with a limit of 50.

**Outcome:** Succeeded with the bundled-Core and plugin public API contract test classes.

### 647. `get-api-surface`

**Activity:** Internal XML-documentation scope measurement.

**Purpose:** Measure the complete solution-wide non-private symbol surface as an upper bound.

**Request:** Solution scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 8640`; this includes test and fixture projects and was narrowed to production projects subsequently.

### 648. `get-api-surface`

**Activity:** Internal XML-documentation scope measurement.

**Purpose:** Measure the public solution-wide control surface.

**Request:** Solution scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 3698`; this includes test and fixture projects and was narrowed to production projects subsequently.

### 649. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Start a per-project internal/public comparison for Abstractions.

**Request:** Abstractions project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 232`.

### 650. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Abstractions public control concurrently.

**Request:** Abstractions project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` because the first broad concurrent batch exceeded the configured two-query capacity.

### 651. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Start a per-project internal/public comparison for the Host.

**Request:** Host project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 933`.

### 652. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Host public control concurrently.

**Request:** Host project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 653. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Workspace internal surface concurrently.

**Request:** Workspace project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 654. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Workspace public control concurrently.

**Request:** Workspace project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 655. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Code Actions internal surface concurrently.

**Request:** Code Actions project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 656. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Code Actions public control concurrently.

**Request:** Code Actions project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 657. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Plugins internal surface concurrently.

**Request:** Plugins project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 658. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the Plugins public control concurrently.

**Request:** Plugins project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 659. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the bundled Core internal surface concurrently.

**Request:** bundled Core project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 660. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the bundled Core public control concurrently.

**Request:** bundled Core project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 661. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the plugin analyser internal surface concurrently.

**Request:** plugin analyser project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch.

### 662. `get-api-surface`

**Activity:** Internal XML-documentation production-scope measurement.

**Purpose:** Measure the plugin analyser public control concurrently.

**Request:** plugin analyser project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Returned retryable `WorkspaceBusy` during the over-capacity concurrent batch. The measurements were retried within the published capacity.

### 663. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Abstractions internal surface within query capacity.

**Request:** Abstractions project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 232`.

### 664. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Abstractions public control within query capacity.

**Request:** Abstractions project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 221`.

### 665. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Host internal surface within query capacity.

**Request:** Host project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 933`.

### 666. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Host public control within query capacity.

**Request:** Host project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 0`.

### 667. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Workspace internal surface within query capacity.

**Request:** Workspace project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 1325`.

### 668. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Workspace public control within query capacity.

**Request:** Workspace project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 63`.

### 669. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Code Actions internal surface within query capacity.

**Request:** Code Actions project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 602`.

### 670. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Code Actions public control within query capacity.

**Request:** Code Actions project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 0`.

### 671. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Plugins internal surface within query capacity.

**Request:** Plugins project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 397`.

### 672. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the Plugins public control within query capacity.

**Request:** Plugins project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 150`.

### 673. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the bundled Core internal surface within query capacity.

**Request:** bundled Core project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 703`.

### 674. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the bundled Core public control within query capacity.

**Request:** bundled Core project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 2`.

### 675. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the plugin analyser internal surface within query capacity.

**Request:** plugin analyser project scope, `minimumAccessibility: Internal`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 106`.

### 676. `get-api-surface`

**Activity:** Internal XML-documentation bounded production-scope measurement.

**Purpose:** Measure the plugin analyser public control within query capacity.

**Request:** plugin analyser project scope, `minimumAccessibility: Public`, obsolete symbols included and result limit 1.

**Outcome:** Succeeded with `totalCount: 15`. Across the seven production projects, the internal threshold covers 4,298 symbols versus 451 public symbols, an upper-bound increase of 3,847 declarations.

### 677. `workspace-close`

**Activity:** Internal XML-documentation work scoping cleanup.

**Purpose:** Close the read-only semantic Workspace after measurement.

**Request:** `{"workspace":{"alias":"internal-xml-doc-scope"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 2 with no transaction.

### 678. `workspace-list`

**Activity:** Internal XML-documentation first remediation batch.

**Purpose:** Confirm that no published dogfood Workspace was already loaded before inspecting the Abstractions and plugin analyser projects.

**Request:** `{}`

**Outcome:** Succeeded; no Workspaces or transaction owner were present.

### 679. `workspace-open`

**Activity:** Internal XML-documentation first remediation batch.

**Purpose:** Load the repository solution with isolated WSL build artifacts for semantic inspection.

**Request:** Trusted repository solution and root, alias `internal-xml-docs`, with a redacted absolute artifacts path that had not yet been created.

**Outcome:** Rejected with `WorkspaceMsBuildPropertiesInvalid` because the configured artifacts directory did not exist.

### 680. `workspace-open`

**Activity:** Internal XML-documentation first remediation batch.

**Purpose:** Retry the semantic Workspace load after creating the isolated artifacts directory.

**Request:** Trusted repository solution and root, alias `internal-xml-docs`, with the now-existing redacted absolute artifacts path.

**Outcome:** Succeeded at Workspace epoch 3 with 31 projects and 1,621 documents. The load reported two unresolved analyser-reference warnings for the isolated output path and the expected WSL-on-Windows-filesystem warning.

### 681. `get-document-outline`

**Activity:** Internal XML-documentation first remediation batch.

**Purpose:** Inspect the structure of the shared plugin diagnostic descriptor catalogue before documenting its type and fields.

**Request:** `PluginDiagnosticDescriptors.cs` in the plugin analyser project, members included, maximum depth 2 and node limit 80.

**Outcome:** Succeeded and identified the internal `PluginDiagnosticDescriptors` type. The projection was truncated and unexpectedly associated the namespace node with a location from `CancellationTokenUsageState.cs`; source inspection remained authoritative for the documentation change.

### 682. `workspace-close`

**Activity:** Internal XML-documentation first remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the project inspection.

**Request:** `{"workspace":{"alias":"internal-xml-docs"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 3 with no transaction.

### 683. `workspace-open`

**Activity:** Internal XML-documentation plugin authoring and adaptation remediation batch.

**Purpose:** Load the repository solution for semantic inspection while documenting the plugin authoring and adaptation project.

**Request:** Trusted repository solution and root, alias `internal-xml-plugins`, with the redacted isolated WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 4 with 31 projects and 1,621 documents. The load reported two unresolved analyser-reference warnings for the isolated output path and the expected WSL-on-Windows-filesystem warning.

### 684. `workspace-close`

**Activity:** Internal XML-documentation plugin authoring and adaptation remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the project inspection.

**Request:** `{"workspace":{"alias":"internal-xml-plugins"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 4 with no transaction.

### 685. `workspace-open`

**Activity:** Internal XML-documentation Workspace Caching remediation batch.

**Purpose:** Load the repository solution for semantic inspection of the Workspace cache subsystem.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-caching`, with the redacted isolated WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 5 with 31 projects and 1,665 documents. The load reported the expected WSL-on-Windows-filesystem warning.

### 686. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Caching remediation batch.

**Purpose:** Inspect the shared cache engine structure before documenting its generation, admission and in-flight computation responsibilities.

**Request:** `QueryCacheStateCore.cs` in the Workspace project, members included, maximum depth 2 and node limit 120.

**Outcome:** Succeeded and identified the internal `QueryCacheStateCore` type. The projection was truncated and unexpectedly associated the namespace node with a location from `IPluginQueryCacheState.cs`; source inspection remained authoritative for the documentation change.

### 687. `workspace-close`

**Activity:** Internal XML-documentation Workspace Caching remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the Caching inspection.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-caching"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 5 with no transaction.

### 688. `workspace-open`

**Activity:** Internal XML-documentation Workspace Change Detection remediation batch.

**Purpose:** Load the repository solution for semantic inspection of the Workspace input-manifest and change-monitoring subsystem.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-change-detection`, with the redacted isolated WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 6 with 31 projects and 1,665 documents. The load reported the expected WSL-on-Windows-filesystem warning.

### 689. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Change Detection remediation batch.

**Purpose:** Inspect the input-manifest coordinator and distinguish its public certification, manifest-building and stale-state operations from its private assembly helpers.

**Request:** `WorkspaceChangeDetector.cs` in the Workspace project, members included, maximum depth 3 and node limit 160.

**Outcome:** Succeeded and returned the complete type and member outline. The namespace node was again associated with the interface document rather than the requested implementation; source inspection remained authoritative for the documentation change.

### 690. `workspace-close`

**Activity:** Internal XML-documentation Workspace Change Detection remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the Change Detection inspection.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-change-detection"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 6 with no transaction.

### 691. `workspace-open`

**Activity:** Internal XML-documentation Workspace State remediation batch.

**Purpose:** Load the repository solution for semantic inspection of Workspace session ownership, snapshot identity and lifecycle state transitions.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-state`, with the redacted isolated WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 7 with 31 projects and 1,665 documents. The load reported the expected WSL-on-Windows-filesystem warning.

### 692. `get-document-outline`

**Activity:** Internal XML-documentation Workspace State remediation batch.

**Purpose:** Inspect the lifecycle state-machine boundary and separate its configured transition graph from session-storage responsibilities.

**Request:** `WorkspaceStateMachine.cs` in the Workspace project, members included, maximum depth 3 and node limit 180.

**Outcome:** Succeeded and returned the complete type and public member outline. The namespace node was again associated with a different State document; source inspection remained authoritative for the documentation change.

### 693. `workspace-close`

**Activity:** Internal XML-documentation Workspace State remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the State inspection.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-state"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 7 with no transaction.

### 694. `workspace-open`

**Activity:** Internal XML-documentation Workspace Loading remediation batch.

**Purpose:** Load the repository solution for semantic inspection of workspace loading, input normalisation and compatibility boundaries.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-loading`, with a redacted isolated WSL artifacts path that had not yet been created.

**Outcome:** Failed with `WorkspaceMsBuildPropertiesInvalid` because the artifacts path must be an existing absolute directory. The directory was then created before retrying.

### 695. `workspace-open`

**Activity:** Internal XML-documentation Workspace Loading remediation batch retry.

**Purpose:** Load the repository solution after creating the isolated artifacts directory required by the first request.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-loading`, with the redacted existing WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 8 with 31 projects and 1,621 documents. The load reported two unresolved analyzer-reference warnings because the isolated directory contained no prior analyzer build, plus the expected WSL-on-Windows-filesystem warning.

### 696. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Loading remediation batch.

**Purpose:** Inspect the validated load workflow and distinguish its compatibility filtering and root-enforcement responsibilities from raw workspace loading.

**Request:** `WorkspaceLoadWorkflow.cs` in the Workspace project, members included, maximum depth 3 and node limit 160.

**Outcome:** Succeeded and returned the complete type and member outline. The namespace node was again associated with a different Loading document; source inspection remained authoritative for the documentation change.

### 697. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Loading remediation batch.

**Purpose:** Inspect the MSBuild property resolution boundary and its separation of caller validation from workspace construction.

**Request:** `WorkspaceMsBuildPropertiesResolver.cs` in the Workspace project, members included, maximum depth 3 and node limit 160.

**Outcome:** Succeeded and returned the complete type and member outline. The namespace node was again associated with a different Loading document; source inspection remained authoritative for the documentation change.

### 698. `workspace-close`

**Activity:** Internal XML-documentation Workspace Loading remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the Loading inspection.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-loading"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 8 with no transaction.

### 699. `workspace-open`

**Activity:** Internal XML-documentation Workspace Lifecycle remediation batch.

**Purpose:** Load the repository solution for semantic inspection of workspace lifecycle coordination and resource cleanup.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-lifecycle`, with a redacted existing isolated WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 9 with 31 projects and 1,621 documents. The load reported two unresolved analyzer-reference warnings because the isolated directory contained no prior analyzer build, plus the expected WSL-on-Windows-filesystem warning.

### 700. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Lifecycle remediation batch.

**Purpose:** Inspect the service boundary coordinating open, list, close, shutdown, status and reload operations.

**Request:** `WorkspaceLifecycleService.cs` in the Workspace project, members included, maximum depth 3 and node limit 220.

**Outcome:** Succeeded and returned the complete lifecycle service outline. The namespace node was again associated with a different Lifecycle document; source inspection remained authoritative for the documentation change.

### 701. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Lifecycle remediation batch.

**Purpose:** Inspect the resource-cleanup boundary used by close and shutdown operations.

**Request:** `WorkspaceSessionCleanup.cs` in the Workspace project, members included, maximum depth 3 and node limit 220.

**Outcome:** Succeeded and returned the complete cleanup service outline. The namespace node was again associated with a different Lifecycle document; source inspection remained authoritative for the documentation change.

### 702. `workspace-close`

**Activity:** Internal XML-documentation Workspace Lifecycle remediation batch cleanup.

**Purpose:** Release the semantic Workspace after completing the Lifecycle inspection.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-lifecycle"}}`

**Outcome:** Succeeded and closed the solution at Workspace epoch 9 with no transaction.

### 703. `workspace-open`

**Activity:** Remaining internal XML-documentation Workspace remediation batches.

**Purpose:** Load the repository solution once for semantic inspection across the remaining Workspace responsibility areas.

**Request:** Trusted repository solution and root, alias `internal-xml-workspace-remaining`, with a redacted existing isolated WSL artifacts path.

**Outcome:** Succeeded at Workspace epoch 10 with 31 projects and 1,621 documents. The load reported two unresolved analyzer-reference warnings because the isolated directory contained no prior analyzer build, plus the expected WSL-on-Windows-filesystem warning.

### 704. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Execution Contexts remediation batch.

**Purpose:** Inspect the boundary that acquires query and mutation leases, validates session state, and constructs snapshot-scoped execution services.

**Request:** `WorkspaceExecutionContextFactory.cs` in the Workspace project, members included, maximum depth 3 and node limit 220.

**Outcome:** Succeeded and returned the complete execution-context factory outline. The namespace node was again associated with a different Execution Contexts document; source inspection remained authoritative for the documentation change.

### 705. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Diagnostics remediation batch.

**Purpose:** Inspect the event-source boundary that defines workspace performance phases and metrics.

**Request:** `WorkbenchPerformanceEventSource.cs` in the Workspace project, members included, maximum depth 3 and node limit 180.

**Outcome:** Rejected with `WorkspaceOutOfDate` because the preceding documentation batch changed the loaded workspace snapshot. The server directed the caller to reload before retrying.

### 706. `workspace-reload`

**Activity:** Remaining internal XML-documentation Workspace remediation batches.

**Purpose:** Refresh the long-lived semantic workspace after the preceding source documentation batch.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-remaining"}}`

**Outcome:** Succeeded at Workspace epoch 11 with 31 projects and 1,621 documents. The reload retained the two expected unresolved analyzer-reference warnings for the isolated artifacts directory.

### 707. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Diagnostics remediation batch retry.

**Purpose:** Inspect the event-source boundary after refreshing the semantic workspace.

**Request:** `WorkbenchPerformanceEventSource.cs` in the Workspace project, members included, maximum depth 3 and node limit 180.

**Outcome:** Succeeded and returned the complete event-source outline, including its performance-phase constants, cache families and emitted events. The namespace node was again associated with the other Diagnostics document; source inspection remained authoritative for the documentation change.

### 708. `workspace-reload`

**Activity:** Internal XML-documentation Workspace IO remediation batch preparation.

**Purpose:** Proactively refresh the semantic workspace after the Diagnostics documentation batch.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-remaining"}}`

**Outcome:** Rejected with `WorkspaceReloadNotRequired` because external-change detection had not yet transitioned the session to its out-of-date state.

### 709. `get-document-outline`

**Activity:** Internal XML-documentation Workspace IO remediation batch.

**Purpose:** Inspect the durable atomic-write boundary and its platform-specific commit responsibilities.

**Request:** `AtomicFileWriter.cs` in the Workspace project, members included, maximum depth 3 and node limit 180.

**Outcome:** Rejected with `WorkspaceOutOfDate`; query acquisition detected the preceding source changes and directed the caller to reload.

### 710. `workspace-reload`

**Activity:** Internal XML-documentation Workspace IO remediation batch preparation retry.

**Purpose:** Refresh the semantic workspace after query acquisition marked it out of date.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-remaining"}}`

**Outcome:** Succeeded at Workspace epoch 12 with 31 projects and 1,621 documents, retaining the two expected unresolved analyzer-reference warnings.

### 711. `get-document-outline`

**Activity:** Internal XML-documentation Workspace IO remediation batch retry.

**Purpose:** Inspect the durable atomic-write boundary after refreshing the workspace.

**Request:** `AtomicFileWriter.cs` in the Workspace project, members included, maximum depth 3 and node limit 180.

**Outcome:** Succeeded and returned the complete writer outline, separating temporary-file durability, native commit and retry responsibilities. The namespace node was again associated with another IO document; source inspection remained authoritative for the documentation change.

### 712. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Recovery remediation batch.

**Purpose:** Inspect the durable commit-recovery store and its persistence responsibilities.

**Request:** `CommitRecoveryStore.cs` in the Workspace project, members included, maximum depth 3 and node limit 220.

**Outcome:** Rejected with `WorkspaceOutOfDate`; query acquisition detected the preceding source changes and directed the caller to reload.

### 713. `workspace-reload`

**Activity:** Internal XML-documentation Workspace Recovery remediation batch preparation.

**Purpose:** Refresh the semantic workspace after query acquisition marked it out of date.

**Request:** `{"workspace":{"alias":"internal-xml-workspace-remaining"}}`

**Outcome:** Succeeded at Workspace epoch 13 with 31 projects and 1,621 documents, retaining the two expected unresolved analyzer-reference warnings.

### 714. `get-document-outline`

**Activity:** Internal XML-documentation Workspace Recovery remediation batch retry.

**Purpose:** Inspect the durable commit-recovery store after refreshing the workspace.

**Request:** `CommitRecoveryStore.cs` in the Workspace project, members included, maximum depth 3 and node limit 220.

**Outcome:** Succeeded and returned the complete store outline, including recovery plan, artifact, manifest, status and owner-record operations. The namespace node was associated with `CommitRecoveryLimits.cs`; source inspection remained authoritative for the documentation change.

### 715. `workspace-list`

**Activity:** Internal XML-documentation manual text review preparation.

**Purpose:** Determine whether an existing semantic workspace could be reused for the manual one-by-one review.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces.

### 716. `workspace-open`

**Activity:** Internal XML-documentation manual text review preparation.

**Purpose:** Load the repository for semantic inspection of each documented type and its implementation.

**Request:** Open the solution as alias `manual-doc-review`, using a dedicated temporary artifacts directory.

**Outcome:** Rejected with `WorkspaceMsBuildPropertiesInvalid` because the requested artifacts directory did not yet exist.

### 717. `workspace-open`

**Activity:** Internal XML-documentation manual text review preparation retry.

**Purpose:** Load the repository after creating the dedicated temporary artifacts directory.

**Request:** Open the solution as alias `manual-doc-review`, using the existing dedicated temporary artifacts directory.

**Outcome:** Succeeded with 31 projects and 1,621 documents. The expected unresolved analyzer-reference and WSL-on-Windows-filesystem warnings were reported.

### 718. `get-code-context`

**Activity:** Internal XML-documentation Error Reporting retention manual review.

**Purpose:** Read `IBoundedExpiringStore` with its semantic context before assessing whether each XML comment explains the contract naturally and accurately.

**Request:** Select `IBoundedExpiringStore` in `IBoundedExpiringStore.cs` with five lines before and 120 lines after the selection.

**Outcome:** Rejected with `InvalidRequest` because `afterLines` exceeded the supported limit.

### 719. `get-code-context`

**Activity:** Internal XML-documentation Error Reporting retention manual review retry.

**Purpose:** Read `IBoundedExpiringStore` with a supported context window.

**Request:** Select `IBoundedExpiringStore` in `IBoundedExpiringStore.cs` with five lines before and 60 lines after the selection, including enclosing symbols.

**Outcome:** Succeeded and exposed several mechanical comments that do not describe the bounded, expiring storage contract, including “Stores bounded expiring”, “Adds or replace” and references to “the bounded expiring”.

### 720. `workspace-list`

**Activity:** ALPHA-004 generated tool-reference design discovery.

**Purpose:** Confirm that the existing repository workspace is available for semantic inspection of tool catalogue and schema-generation ownership.

**Request:** `{}`

**Outcome:** Succeeded and reported the repository solution loaded under the existing `manual-doc-review` alias with no transaction owner.

### 721. `get-document-outline`

**Activity:** ALPHA-004 generated tool-reference design discovery.

**Purpose:** Inspect the production tool-protocol factory that combines registered metadata with generated schemas.

**Request:** Outline `McpToolProtocolFactory.cs` with members, maximum depth 3 and node limit 160 in the `manual-doc-review` workspace.

**Outcome:** Rejected with `WorkspaceOutOfDate` because the design-document edits made after the Workspace load required a reload.

### 722. `workspace-reload`

**Activity:** ALPHA-004 generated tool-reference design discovery recovery.

**Purpose:** Refresh the semantic workspace after the documentation changes made during discovery.

**Request:** `{"workspace":{"alias":"manual-doc-review"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 31 projects and 1,621 documents. It retained the two expected unresolved analyzer-reference warnings.

### 723. `get-document-outline`

**Activity:** ALPHA-004 generated tool-reference design discovery retry.

**Purpose:** Identify the common production path for Server, plugin and Code Action protocol-tool construction after refreshing the workspace.

**Request:** Outline `McpToolProtocolFactory.cs` with members, maximum depth 3 and node limit 160 in the `manual-doc-review` workspace.

**Outcome:** Succeeded and confirmed that `McpToolProtocolFactory` owns Server, plugin and Code Action protocol construction through one `IToolSchemaFactory`, with a common catalogue-tool path and published-description projection.

### 724. `workspace-list`

**Activity:** Microsoft.CodeAnalysis 5.9.0 upgrade feasibility audit.

**Purpose:** Check whether a repository workspace was already loaded for semantic dependency inspection without opening another instance.

**Request:** `{}`

**Outcome:** Succeeded and reported no loaded workspaces and no transaction owner; package ownership was therefore inspected from the repository configuration without opening a workspace.
