# Unit 7 — Error reporting and trust boundaries

Date: 2026-08-16

**Report status:** Completed. No new candidate was substantiated; `RWMCP3-015` is independently corroborated.

## Scope and evidence

This review covered all ErrorReporting implementation, the top-level exception filter and details tool, conditional tool publication, configuration/status/DI, Workspace attribution and lifecycle observation, logging and Sentry dispatch, and current unit, integration and acceptance claims. It used only current source, normative documentation, configuration and tests. No test command ran.

## Capture, retention and attribution

Unexpected non-cancellation, non-protocol failures receive a random correlation ID and enter a bounded process-local store. Capture bounds exception depth, frames, messages, names and paths, then reduces detail further when the configured size limit is exceeded. Local details are explicitly marked sensitive and unsafe for external submission.

Pre-acquisition failures use selector rebinding against the current session store. Plugin and Code Action adapters preserve immutable execution-time Workspace context after acquisition. Server-owned lifecycle tools do not: close can remove a session before cleanup fails, and reload can replace the epoch before old-session disposal fails. Fallback attribution then loses the removed session or attributes the failure to the replacement. This independently corroborates `RWMCP3-015`.

## External projection and immutable preparation

External preparation never serialises the local diagnostic directly. It emits only fixed classifications, bounded first-party/runtime component categories, coarse Workspace state/counts, failure timing and process/version details. External plugin tool names are normalised; exception messages, paths, source locations, implementation names, Workspace IDs and original correlation IDs are excluded.

Provider adapters create immutable prepared bytes. Preview JSON and SHA-256 digests derive from those same stored bytes, and submission uses defensive copies. No bypass of the allow-list projector was found.

## Consent, concurrency and submission

Preparation performs no dispatch. `never` omits external-report tools, `prompt` elicits one-report/Workspace/session consent, and `always` authorises only an explicit submission call. Decline discards the handle, cancellation retains it, session suppression clears grants, and Workspace grants are keyed by ID and epoch and invalidated on lifecycle change.

Prepared payloads deliberately survive Workspace changes, but later consent is evaluated against current lifecycle state. Store acquisition serialises submission: one caller transitions to sending, concurrent callers see in-progress, successful repeats return the receipt, and rejected/thrown/cancelled dispatch returns the handle to prepared.

## Dispatch, configuration and lifetimes

The logging dispatcher writes the reviewed JSON string to stderr. The Sentry path constructs a typed event during preparation, disables automatic sensitive enrichment and applies a final allow-list. A controlled integration test exercises the real SDK serializer and checks exact preview/envelope equality. Remote delivery after SDK queue acceptance is provider-owned and not claimed by the submission response.

Stores, consent state, lifecycle observer and dispatcher are synchronised process singletons. Container ownership covers timers and Sentry client disposal. Invalid command-line consent fails closed; similarly named environment input cannot establish `never` or `always`. Status does not expose the DSN.

## Evidence gaps and conclusions

Current tests claim broad capture, redaction, consent, preparation, store-transition, logging/Sentry and submission coverage. Material gaps remain for actual server-owned post-transition failures (`RWMCP3-015`), real concurrent duplicate submission, published-client elicitation acceptance, expiry races, tool-level capacity/oversize failures and complete rejection/retry workflows. Remote Sentry failure after queue acceptance cannot be established locally.

No stale-payload mutation, old-epoch consent reuse, projection bypass, preview/dispatch mismatch, publication defect or additional actionable finding was substantiated.

