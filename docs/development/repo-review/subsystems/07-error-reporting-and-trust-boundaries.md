# Unit 7 — Error reporting and trust boundaries

Date: 2026-08-16

**Report status:** Completed. No new candidate was substantiated; `RWMCP3-015` is independently corroborated.

## Scope and evidence

This review covered all ErrorReporting implementation, the top-level exception filter and details tool, conditional tool publication, configuration/status/DI, Workspace attribution and lifecycle observation, logging and Sentry dispatch, and current unit, integration and acceptance claims. It used only current source, normative documentation, configuration and tests. No test command ran.

## Capture, retention and attribution

Unexpected non-cancellation, non-protocol failures receive a random correlation ID and enter a bounded process-local store. Capture bounds exception depth, frames, messages, names and paths, then reduces detail further when the configured size limit is exceeded. Local details are explicitly marked sensitive and unsafe for external submission.

Pre-acquisition failures use selector rebinding against the current session store. Plugin and Code Action adapters preserve immutable execution-time Workspace context after acquisition. Server-owned lifecycle tools do not: close can remove a session before cleanup fails, and reload can replace the epoch before old-session disposal fails. Fallback attribution then loses the removed session or attributes the failure to the replacement. This independently corroborates `RWMCP3-015`.

## External projection and immutable preparation

External preparation never serialises the local diagnostic directly. It emits only fixed classifications, bounded first-party/runtime component categories, bounded exception messages, approved first-party source file names and line numbers, coarse Workspace state/counts, failure timing and process/version details. External plugin tool names are normalised; absolute paths, unapproved source locations, implementation names, Workspace IDs and original correlation IDs are excluded. Exception messages are unfiltered reviewed content that may contain otherwise excluded values, so preparation warns about that limitation and submission offers a message-free variant.

Provider adapters create immutable prepared bytes. Preview JSON and SHA-256 digests derive from those same stored bytes, and submission uses defensive copies. No bypass of the allow-list projector was found.

## Consent, concurrency and submission

Preparation performs no dispatch. `never` omits external-report tools and publishes disabled consent state, `prompt` elicits consent separately for each prepared report, and `always` authorises only an explicit submission call. Prompt mode offers only send, send without exception messages or do not send. Decline, cancellation, missing response content and explicit do-not-send discard the handle without dispatch; approval unavailability and invalid choices release it for an explicit retry. No choice creates a Workspace or session override.

Prepared payloads deliberately survive Workspace changes. Consent comes only from the immutable startup policy or the current report's prompt response. Store acquisition serialises submission: one caller transitions to sending, concurrent callers see in-progress, successful repeats return the receipt, and rejected/thrown/cancelled dispatch returns the handle to prepared.

## Dispatch, configuration and lifetimes

The logging dispatcher writes the reviewed JSON string to stderr. The Sentry path constructs a typed event during preparation, disables automatic sensitive enrichment and applies a final allow-list. A controlled integration test exercises the real SDK serializer and checks exact preview/envelope equality. Remote delivery after SDK queue acceptance is provider-owned and not claimed by the submission response.

The bounded stores and dispatcher are synchronised process singletons. Container ownership covers timers and Sentry client disposal. Invalid command-line consent fails closed; similarly named environment input cannot establish `never` or `always`. Status publishes the effective disabled, prompt-required or always-approved consent state but does not expose the DSN.

## Evidence gaps and conclusions

Current tests cover capture provenance, redaction, consent, preparation, store transitions, logging/Sentry dispatch, expiry during elicitation, tool-level capacity and payload limits, real concurrent duplicate submission, submission rejection/retry workflows, and all consent choices through an elicitation-capable published client. Actual server-owned post-transition failures remain tracked separately by `RWMCP3-015`. Remote Sentry failure after queue acceptance cannot be established locally.

No stale-payload mutation, old-epoch consent reuse, projection bypass, preview/dispatch mismatch, publication defect or additional actionable finding was substantiated.

