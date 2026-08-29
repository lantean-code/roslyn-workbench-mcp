# Dogfood response-shaping worklist

**Status:** Approved for design discovery on 2026-08-29.

**Evidence:** [Final post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md)

## Purpose

The completed post-RWMCP3 worklists addressed every confirmed functional defect found during 510 recorded dogfood requests. Final analysis retained two lower-priority response-shaping observations where accurate results were less useful or substantially larger than an agent needed. Neither observation is a confirmed product defect. Each item must first validate the behaviour against the current committed implementation, inspect existing unit, integration, acceptance and Scenario Runner coverage, and establish a concrete agent-facing usability gap before proposing a design.

## Worklist

| Order | Identifier | Investigation | Status |
|---:|---|---|---|
| 1 | DOGFOOD-014 | [Exclude intermediate build documents from agent-facing operations](dogfood-014-bounded-structure-document-selection-design.md) | Implemented, validated and confirmed |
| 2 | DOGFOOD-015 | [Reduce unnecessary analyser-config context in document-options responses](dogfood-015-document-options-response-design.md) | Implemented and validated; awaiting manual confirmation |
| 3 | DOGFOOD-016 | Publish agent-visible contract guidance in MCP input and configured output schemas | Issue and existing-coverage validation pending |

### DOGFOOD-014 — Bounded structure document selection

During the representative query-surface sweep, `get-solution-structure` and `get-project-details` each returned generated `obj` documents as the first bounded documents. With a limit of two, ordinary source documents were entirely absent. Source inspection at the time showed that candidates were sorted only by normalised path before applying the limit.

Issue validation must reproduce the current committed behaviour and determine whether generated documents consistently displace the documents an agent normally needs. Existing tests and scenarios must be inspected for ordering, generated-document handling, pagination or continuation semantics, and deterministic projection. Design discovery should compare source-first deterministic ordering with an explicit include or preference option and must preserve access to generated documents when they are intentionally requested. No production or test change should begin until the surviving gap and design receive manual approval.

Issue and existing-coverage validation are complete in [DOGFOOD-014 — Agent-facing document eligibility](dogfood-014-bounded-structure-document-selection-design.md). The clean published build reproduced intermediate `obj` documents consuming every slot at a limit of two and three of five slots at a limit of five. Design discussion established that these compiler inputs must remain in Roslyn's solution for semantic correctness but should not be exposed or accepted by agent-facing queries, mutations or Code Actions. The revised design keeps classification Workspace-owned, provides pre-filtered document collections through `IWorkspaceResolver`, retains mutation-validation enforcement as a backstop, and preserves existing public request and response shapes.

### DOGFOOD-015 — Document-options response size

The representative query-surface sweep found that `get-document-options` returned accurate language, parse and effective analyser-config information, but the complete analyser-config option map made the response substantially larger than the rest of the request required. The published contract exposed no category selection, named-key filter or collection bound.

Issue validation must measure the current response shape on representative documents, identify which fields agents use in real workflows, and inspect existing coverage for exact option projection and compatibility expectations. Design discovery should determine whether the best contract is a concise default, explicit categories, named option keys, bounded projection or no change. It must preserve a supported way to obtain complete effective options when genuinely required and avoid introducing pagination whose continuation semantics are less useful than targeted selection. No production or test change should begin until the surviving gap and design receive manual approval.

Issue and existing-coverage validation are complete in [DOGFOOD-015 — Document-options response size](dogfood-015-document-options-response-design.md). A representative production document returned 120 effective options, with the option map consuming 8,223 of approximately 9,557 JSON characters. The approved design keeps the document reference, language version and nullable context in the concise default response, with independent `includeParseOptions` and `includeAnalyzerConfig` switches for the two complete detailed projections.

### DOGFOOD-016 — Agent-visible contract guidance

Design review of DOGFOOD-015 confirmed that XML documentation on contract properties is not automatically included in published MCP schemas. The client therefore sees property names and types but may not receive the semantics needed to use them correctly or efficiently. For `get-document-options`, this includes understanding the distinction between its concise default and the optional complete parse-options and analyser-config projections, but the concern applies to every published request property and to every published response property when the server is configured to publish output schemas.

Issue validation must inventory every published request and response contract, inspect generated input and configured output schemas across server-owned, Code Action and plugin tools, determine which runtime metadata the SDK carries into property descriptions, and audit existing contract tests for description preservation. Design discovery should establish whether descriptions belong on runtime attributes, in a central schema-enrichment layer or in another SDK-supported source. It must provide a maintainable ownership and enforcement rule, avoid unmanaged duplication between XML and runtime documentation, and cover the complete published contract surface rather than special-casing `get-document-options`. No production or test change should begin until the surviving gap and design receive manual approval.

## Process

For each item, perform issue and existing-coverage validation first and present the evidence and proposed design for manual approval before the expensive implementation and review steps. If the behaviour is already covered and intentional, or the measured context cost is immaterial, close or narrow the item rather than inventing work. Any confirmed behaviour-affecting change follows the repository's normal implementation, validation, user-confirmation, staging and fresh Review Agent process.
