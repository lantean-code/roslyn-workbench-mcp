# Post-RWMCP3 dogfood usage

**Status:** Active until the user explicitly ends dogfood usage logging.

This log records every request sent to the configured published Roslyn Workbench dogfood server after the RWMCP3 usage analysis. It covers implementation of the [approved dogfood improvement worklist](dogfood-analysis.md) and all other repository operations. Failed requests, retries, blank client projections and abandoned approaches remain part of the evidence.

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
