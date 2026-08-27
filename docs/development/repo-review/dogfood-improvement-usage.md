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
