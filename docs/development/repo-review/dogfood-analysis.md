# RWMCP3 dogfood analysis and improvement plan

**Status:** Analysis complete; improvement worklist approved on 2026-08-23.

**Evidence:** [RWMCP3 dogfood usage log](dogfood-usage.md) and the continuing [post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md)

## Purpose

This document evaluates how the published Roslyn Workbench Host was used while remediating RWMCP3. It separates confirmed server or client-interoperability problems from expected lifecycle behaviour, records the workflows that were not exercised and defines the dependency-ordered follow-on work without reopening the completed RWMCP3 findings.

## Usage summary

The log contains 60 calls. Fifty-one completed successfully and nine returned explicit failures. Eleven of the successful calls exposed no visible response through the client projection even though the server returned structured content.

| Tool | Calls |
|---|---:|
| `search-symbols` | 25 |
| `workspace-list` | 12 |
| `workspace-open` | 7 |
| `server-status` | 5 |
| `get-symbol-members` | 4 |
| `find-references` | 3 |
| `workspace-reload` | 2 |
| `get-code-context` | 1 |
| `get-symbol-info` | 1 |

All 60 calls were query or lifecycle operations. No mutation tool was used. This reflects the structural nature of most RWMCP3 remediation rather than evidence that mutation tools are unsuitable, but it leaves the published mutation workflow without dogfood evidence from this stage.

The 26 lifecycle calls to `workspace-list`, `workspace-open`, `workspace-reload` and `server-status` are not representative of an ordinary steady-state session. The remediation process deliberately published and restarted the Host between work items, which required repeated process and Workspace checks.

## Failure classification

Four failures exposed avoidable contract or selector friction:

- `InvalidRequest` followed an incomplete `SnapshotPrecondition`; the agent-facing declaration did not expose all required members.
- Two solution-wide documentation-comment identifiers returned `SymbolAmbiguous` without a project-qualified selector.
- One `LocationSelector` was constructed with the wrong nested shape and returned `SymbolNotFound`.

Five failures represented expected, recoverable operational state:

- `WorkspaceReloadNotRequired` was returned before change detection had certified the Workspace as stale.
- `WorkspaceOutOfDate` correctly required a reload, after which the operation succeeded.
- Two `WorkspaceBusy` responses followed concurrent calls exceeding the configured two-query capacity; retrying after a slot became available succeeded.
- `WorkspaceNotOpen` correctly identified a review process without an open Workspace and instructed the caller to open one.

The stale, busy, reload-not-required and not-open responses are not product defects. The agent should follow their structured continuations, avoid speculative reloads and keep parallel Workspace queries within the capacity published by full `server-status` detail.

## Confirmed observations

### Structured results are not interoperable with every client projection

The published Host sets `StructuredContent` but deliberately leaves `Content` empty for structured successes and failures. Direct inspection of `server-status` and `workspace-list` confirmed this response shape. Eleven successful calls therefore appeared blank through the dogfood client and several had to be repeated before their structured result became visible.

The [MCP tools specification](https://modelcontextprotocol.io/specification/2025-11-25/server/tools) states that, for backwards compatibility, a tool returning structured content should also return the serialized JSON in a `TextContent` block. The server should retain its authoritative structured object and publish exactly the same object as a JSON string fallback rather than maintaining two independently shaped representations.

### Agent-facing input declarations lose material contract information

Fifty-one of the 56 published dogfood tool declarations contained `unknown`; 18 contained opaque intersections such as `unknown & unknown`. The projected `SnapshotPrecondition` exposed `workspaceId` but omitted `workspaceEpoch`, `snapshotId` and `transactionRevision`. Selector declarations similarly hid nested shapes and constraints.

The C# contracts and current schema tests retain these members and validation rules. The observed problem is therefore compatibility between the composed JSON Schema emitted by the Host and the agent client's callable type projection, not evidence that the underlying contracts lack validation. The attribute-driven single source for schema publication and runtime validation remains the intended design.

### Server instructions consume repeated catalogue context

The initialisation instructions contain approximately 967 characters and 138 words. The dogfood client projects them into every one of the 56 tool descriptions, representing approximately 54,152 repeated characters before tool-specific descriptions and schemas. The trust warning, concise transaction rules, distinction from a Git commit and version-specific Agent Guide link remain valuable, but detailed operational guidance can live solely in the linked guide.

### Search and lifecycle recovery are effective

`search-symbols` was the most-used operation and reliably supplied production and test identities. Its four failures were caused by stale, busy or unopened Workspace state, not search implementation defects. Structured continuations enabled successful reload or retry where the caller followed them.

## Approved improvement worklist

| Order | Identifier | Improvement | Status |
|---:|---|---|---|
| 1 | DOGFOOD-001 | Publish an equivalent JSON `TextContent` fallback with every structured result | Complete |
| 2 | DOGFOOD-002 | [Make composed input schemas project into complete, usable agent-facing declarations](dogfood-002-schema-publication-design.md) | Confirmed through published dogfood and acceptance validation |
| 3 | DOGFOOD-003 | Compact repeated server instructions while retaining essential safety guidance and the Agent Guide link | Incomplete |
| 4 | DOGFOOD-004 | Repeat selector workflows and reconsider selector composability only if material friction remains | Incomplete |
| 5 | DOGFOOD-005 | Exercise a supported mutation through a controlled preview-and-rollback dogfood workflow | Incomplete |

### DOGFOOD-001 — Structured-result text fallback

Retain `StructuredContent` as the authoritative object and serialize that same object into one JSON `TextContent` item for successes, expected failures, binding failures and unhandled failures. Add unit, protocol-integration and published-Host acceptance coverage that proves the two representations are equivalent and that both normal and error paths are visible to content-only clients.

### DOGFOOD-002 — Agent-compatible schema publication

Begin with focused schema experiments rather than a broad transformer rewrite. Preserve the attribute-driven contract model, apply simple member constraints directly to the existing property schema where compatible, and avoid fragmentary composition that causes consumers to discard the base object's members. If necessary, publish complete object alternatives for exactly-one and at-least-one rules. Validate both JSON Schema semantics and the resulting agent-facing declarations for `SnapshotPrecondition`, `WorkspaceSelector`, `SymbolSelector`, `LocationSelector` and `ScopeSelector` through a published Host.

### DOGFOOD-003 — Concise server instructions

Retain the fully trusted Workspace warning, query-first guidance, the coherent transaction and prompt commit-or-rollback rule, the distinction between `transaction-commit` and a Git commit, and the version-specific Agent Guide URL. Remove detailed transaction, breadth and stale-state prose already covered by the linked guide. Update the initialisation integration and acceptance assertions to protect the retained guidance without fixing the entire wording unnecessarily.

### DOGFOOD-004 — Selector composability reassessment

After DOGFOOD-002, repeat the documentation-comment and returned-location workflows that caused ambiguity and malformed selectors. Only then decide whether ambiguity responses need bounded candidates or whether resolved locations should be directly reusable as input selectors. Do not redesign selector contracts solely on evidence that may have been caused by an opaque client projection.

### DOGFOOD-005 — Controlled mutation dogfooding

Use a disposable fixture or otherwise reversible Workspace to resolve a target, start a transaction, perform one supported semantic rename, format or Code Action, inspect the preview, roll back and confirm both Workspace and filesystem state returned to the baseline. During later development, use dogfood mutations when the required edit naturally matches an existing semantic tool; do not force unsupported structural edits through the mutation surface merely to increase usage.

## Explicit non-actions

- Do not increase the default concurrent query capacity based on two caller-created contention responses.
- Do not change stale detection or reload semantics based on the successful out-of-date and reload sequence.
- Do not suppress `WorkspaceNotOpen` or `WorkspaceReloadNotRequired`; both accurately represented current state.
- Do not reopen completed RWMCP3 findings. These improvements have separate identifiers and evidence.
- Do not undertake a broad selector redesign until the corrected schema projection has been dogfooded.

## Remediation process

Each implementation item follows the finding-remediation and commit review gate in [Deep Dive Review](../DeepDiveReview.md): approve the design, implement and validate, obtain the first user confirmation, stage the confirmed baseline, run a fresh context-free Review Agent pass without duplicating current validation, address findings as an unstaged comparison, obtain final confirmation, update this worklist and let the user commit before publishing the next dogfood build.

Every request sent to the published dogfood server during this work or any other subsequent repository operation must be recorded in the [post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md) until the user explicitly ends the logging requirement.
