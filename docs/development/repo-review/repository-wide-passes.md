# RWMCP3 repository-wide validation passes

Date: 2026-08-16

**Status:** Complete. All 18 subsystem candidates remained supported and one new candidate was raised.

## Scope and method

This read-only pass used current source, tests, configuration, packages and normative documentation. It independently retraced all ledger candidates through producers, cross-project contracts, consumers and external boundaries. No Git history, earlier review evidence or broad test command was used.

## 1. Cross-project and package contracts

Project references follow the documented direction and the packed plugin surface does not expose Workspace or MCP implementation types. The remaining mismatches are already captured: target-framework selection (`RWMCP3-002`), public/internal snapshot identity (`RWMCP3-004`), add/delete versus explicit project membership (`RWMCP3-005`), ignored document identity in format ranges (`RWMCP3-011`) and closed schemas versus permissive binding (`RWMCP3-014`). No new package mismatch was found.

## 2. Dependency direction and abstraction ownership

Abstractions remains implementation-independent, Host owns transport/loading/persistence/reporting, Code Actions is independent of Plugins, and plugin handlers cannot commit directly. Add/delete and rename-file defects are common Workspace capability mismatches rather than ownership inversions. No new candidate arose.

## 3. End-to-end behaviour

Complete MCP-to-Roslyn/Workspace-to-response traces preserve `RWMCP3-001` and `RWMCP3-003`–`015`: later projects contain no compensating check for external wildcard invisibility, transaction admission, stale snapshot aliases, project graph persistence, malformed recovery paths, directory swaps, CFG/code-context handler failures, rename-file planning, format selector reinterpretation, Code Action recipe/provider failures, permissive binding or lost lifecycle attribution.

## 4. Dependency injection and lifetimes

Process singletons are stateless or synchronised; Workspace resources are session-owned; leases/contexts are invocation-owned; plugin providers are isolated; stores are bounded. Container-owned disposal and async plugin-provider disposal are correct. `RWMCP3-015` is attribution after cleanup failure, not a lifetime leak. No new issue was found.

## 5. Configuration

Command-line precedence, environment fallback, path deduplication, bounds and fail-closed reporting consent reach their consumers consistently. Unknown Host arguments may belong to surrounding Host/MCP infrastructure, so ScenarioRunner option parsing does not imply a duplicate Host finding. No additional mismatch was found.

## 6. Error, cancellation, continuation and retry

Caller cancellation and protocol exceptions pass through correctly; commit cancellation changes intentionally at the durable boundary; failed/cancelled cache factories are not retained. Generic failures remain for `RWMCP3-006`, `008`, `009`, `013` and `015`, while `RWMCP3-017` discards operational failure evidence. No additional retry-state contradiction was found.

## 7. Concurrency, shared state and caches

Per-Workspace gates do not make global transaction admission atomic, confirming `RWMCP3-003`. Workspace/plugin caches, Code Action references and reporting stores otherwise synchronise state, invalidate correctly and avoid retaining invalid values. No additional race was substantiated.

## 8. Transaction, persistence and cross-process consistency

The normal durable boundary is coherent: lock, plan, recovery, disk revalidation, Applying state, non-cancellable writes, applied validation, promotion and cleanup. Distinct remaining inconsistencies are global owner state (`003`), public solution identity (`004`), explicit project membership (`005`), malformed recovery composition (`006`), path containment TOCTOU (`007`) and missing same-document move representation (`010`).

## 9. Serialization, schema, binary and package compatibility

Plugin schemas are preflighted, public query responses are object-shaped, typed adapters are prebuilt, and package identity sharing is consistent. `RWMCP3-014` remains the key schema/runtime contradiction, while `RWMCP3-009` shows schema-valid numeric input exceeding safe handler arithmetic. No binary/package issue was found.

## 10. Security and trust boundaries

Trusted plugin execution is explicit; discovery performs containment/admission; external reports use an immutable allow-list projection and consent. Remaining security/integrity issues are directory-swap writes (`007`), silently discarded destructive selectors (`014`) and contaminated operational caches (`016`). No additional disclosure or plugin/state-directory issue was found.

## 11. Resource ownership and disposal

Open failures dispose partial resources, close/shutdown attempt all cleanup, leases dispose deterministically, and caches/providers are owner-disposed. `RWMCP3-017` deletes diagnostic evidence rather than leaking resources. No new leak was substantiated.

## 12. Plausible-scale performance

`RWMCP3-009` is also a boundedness failure. A new issue was found in manifest construction: every project recursively enumerates its entire directory tree before excluded roots are filtered, and overlapping roots repeat work. This is `RWMCP3-019`.

## 13. Missing or misleading evidence

The targeted gaps identified by Units 1–8 remain material: external wildcard additions, TFM ancestor collisions, simultaneous transaction starts, stale tuple aliases, explicit-item commits, malformed recovery admission, native directory swaps, Unit 4 edge cases, Unit 5 provider cases, Host binding/attribution cases, ScenarioRunner restoration/failure/parser cases and large overlapping manifest roots. Published EOF with open resources, elicitation acceptance, concurrent submission, expiry races and platform durability remain limitations without a separately substantiated defect.

## 14. Duplicate, conflicting, unreachable or partial behaviour

All candidates remained individually reachable and non-duplicate. External wildcard observation differs from project-membership persistence; public snapshot aliasing differs from stronger Code Action replay identity; add/delete differs from same-ID file moves; handler/arithmetic/binder failures have distinct causes; provider failure differs from lifecycle attribution; and the three ScenarioRunner findings separately affect restoration, diagnostics and workload selection. `RWMCP3-019` concerns excessive in-root traversal and is distinct from missing external membership observation.

## Repository-wide conclusion

Architecture, composition, normal lifetime ownership, cache synchronisation, package surface and the principal durability protocol are coherent. The highest provisional risks are global transaction admission, stale public snapshot aliasing and filesystem containment TOCTOU. The remaining candidates are concrete contract, lifecycle, tool-behaviour, performance and evidence-integrity defects. All 19 candidates proceeded to and were subsequently retained by independent Stage 4 validation.

