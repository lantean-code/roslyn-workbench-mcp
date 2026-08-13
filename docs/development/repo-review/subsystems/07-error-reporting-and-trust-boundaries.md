# Review Unit 7: Error Reporting and Trust Boundaries

Date: 2026-08-13

**Status:** Complete

## Evidence boundary

This review used only the current checked-out source, tests, project and configuration files, the current normative review programme and current error-reporting documentation, plus current official Microsoft Learn documentation where System.Text.Json web-default behaviour required confirmation. It did not use Git history, diffs, changed-file discovery, commits, branches, tags, stashes, reflogs, deleted or renamed artefacts, external backups, historical audits or previous review findings as evidence.

## Scope completed

The review covered the complete current error-reporting implementation and every direct producer, dependency and consumer: fallback/plugin exception capture; correlation and local diagnostic retrieval; bounded retention and expiry; Workspace attribution; tool/plugin/exception classification; external allow-list projection; provider-specific immutable preparation, review JSON and digest; consent modes, elicitation choices and lifecycle invalidation; single-flight, retry and repeated-submission state; logging and Sentry dispatch; provider configuration, redirects, certificate checks, final event allow-listing and Host-owned disposal; server/Workspace status and unexpected-error availability; DI registrations, options resolution/validation and conditional tool publication; focused Host tests, Sentry integration coverage and relevant published-process source.

Representative sensitive, invalid, expired, capacity, consent-required, consent-free, concurrent, repeated, cancelled, rejected, throwing-dispatch, Workspace-close, stderr and Sentry paths were followed to their final local, protocol, filesystem/lifecycle or network-queue boundary. No production code was modified and no real network destination was contacted.

## Capture, correlation and local retention

`UnhandledToolExceptionFilter` is the only producer of `CapturedErrorRecord`. Final inspection of the current MCP SDK routing established that it wraps the combined direct-tool/fallback dispatcher, so ordinary escaping server-owned, Code Action and plugin exceptions reach it. The filter creates a random correlation ID, logs the exception and matching ID to stderr, builds one immutable record and returns only a generic structured `UnhandledException` response. Local logging and `get-error-details` intentionally remain inside the trusted local-agent boundary and can contain exception messages, stack source paths and Workspace details. The details contract labels that response `LocalDiagnostic` and `safeForExternalSubmission: false`.

Capture bounds exception depth, frames and individual names/messages/paths, records only selected runtime/tool/Workspace fields and retains no live exception, request object graph, source document, environment or command line. The captured store is lock-protected, absolutely expiring and capacity bounded; it evicts the oldest failure when necessary. The prepared store has an independent budget and refuses eviction, so diagnostic pressure cannot evict a reviewed submission. Both timers are DI-owned and support synchronous/asynchronous disposal. Current runtime validation prevents the artificially sub-minimum byte limits used by reduction tests, and the minimal runtime record fits beneath the supported lower bound.

Tool classification uses the immutable server-owned, Code Action and plugin catalogues. External plugin identity is retained locally but becomes a coarse external classification before dispatch. The ordinary captured record and its correlation ID are never accepted by the submission tool.

Workspace attribution has one material mismatch. Normal `WorkspaceSelector` accepts Workspace ID, alias or absolute path and typed binding uses the case-insensitive System.Text.Json web defaults. Capture instead examines the raw argument dictionary for exact-case `workspace.workspaceId` only. If exactly one Workspace is open it can fall back to that sole session, but with multiple open Workspaces a valid alias/path selector, or valid differently cased JSON, produces no captured Workspace context. Preparation then cannot apply an existing Workspace grant, cannot offer a new Workspace-scoped choice, and omits otherwise permitted coarse Workspace state. This is `RWMCP2-017`. Current capture tests exercise only exact camel-case Workspace ID and no populated multi-Workspace snapshot.

## External projection, preparation and review

`ExternalErrorReportProjector` constructs a new versioned provider-neutral record by allow-list. It removes the correlation ID, Workspace ID, tool identity for external plugins, exception messages, source files, type/member names, user paths and arbitrary external assembly names. It retains only coarse tool/plugin/exception categories, bounded first-party/runtime stack-component categories, coarse Workspace lifecycle/count/revision values, duration/cancellation and product/runtime/platform versions. The sensitive projection test serialises a record containing a token, private path, private tool and private assembly/type and proves that those strings do not survive.

`prepare-error-report` looks up the local record, applies current availability, creates a fresh report ID, lets the selected dispatcher build its complete provider state and exact UTF-8 preview, enforces the configured payload byte limit, calculates the SHA-256 preview digest and stores an immutable submission under an opaque random handle with absolute expiry. It returns the destination, exact JSON string, digest, expiry and explicit excluded categories without network activity. The logging payload freezes an immutable string; the Sentry payload stores immutable preview bytes and an internally held prepared `SentryEvent`, from which dispatch takes a fresh allow-listed copy. No caller can replace payload, destination, report or approval data during submission.

The Sentry event allow-list serialises the candidate, copies only event ID, timestamp, platform, level, logger, fingerprint, log entry and the Workbench context, then reconstructs the event and adds only coarse SDK name/version. `BeforeSend` repeats the allow-list after SDK processing. The real-Sentry-client integration test uses an in-memory transport, explicitly flushes it and proves that the final event JSON equals the reviewed preview exactly while threads, modules, runtime context, test assembly and source file are absent. Envelope routing headers remain provider transport metadata, as documented.

## Consent, lifecycle and concurrent submission

Startup mode `Never` disables preparation/submission publication and availability but keeps local correlated diagnostics. `Prompt` requires MCP form elicitation unless a process-local Workspace/session grant applies; a client without elicitation fails closed. `Always` skips elicitation only after an explicit prepare and submit call. The prompt binds its destination and digest to the stored payload and offers submit once, allow Workspace when a Workspace/epoch is present, allow session, decline, or suppress session. Decline discards the handle; cancellation retains it; suppression discards it and clears all grants. No request field can assert consent.

Workspace grants are keyed by Workspace ID and epoch. `WorkspaceSessionStore` invokes the consent lifecycle observer on close and epoch replacement; transaction and ordinary snapshot changes deliberately retain the grant, agreeing with the current documented scope. Session and Workspace consent state is lock protected.

Submission state is also lock protected. `TryBeginSubmission` atomically changes one handle from `Prepared` to `Sending`; a concurrent caller observes `InProgress`, a completed caller receives the stored receipt, and a dispatcher rejection, cancellation or exception releases `Sending` to `Prepared` for retry. A successful receipt contains the dispatcher, stable provider/report reference and digest. Absolute expiry can remove any state once its documented lifetime has elapsed.

Consent authorisation and submission acquisition are separate decisions. `SubmitErrorReportTool` reads consent before `TryBeginSubmission` and does not validate it again before handing the payload to the dispatcher. Final disposition review compared this order with the explicit-submission contract: the caller has reviewed an immutable payload and explicitly started submission, while Workspace close/reload invalidates the temporary grant for later requests. The contract does not retroactively revoke an already-authorised request, and dispatch remains part of that request rather than background submission. The initially recorded `RWMCP2-018` candidate is therefore rejected.

## Dispatch, network boundary and disposal

Without an embedded build-time Sentry DSN, approved reports are written as one structured error-level stderr entry containing the exact sanitised logging JSON; cancellation and mismatched payload types do not log. With an embedded DSN, startup requires an absolute HTTPS URI with public-key user information, host and exactly one project path. Status exposes only provider name, not the DSN. The isolated Sentry options disable automatic stack traces, sessions, logs, metrics, global mode, assembly reports, client reports and default PII; the HTTP handler disables redirects and enables certificate-revocation checks.

Sentry dispatch makes a defensive allow-listed event copy and calls `ISentryClient.CaptureEvent`. A non-empty event ID is the documented acceptance boundary; the local Sentry 6.8.0 contract identifies capture as queueing and `FlushAsync` as draining the queue. The Workbench documentation explicitly states that acceptance does not assert remote ingestion and that background transport may subsequently deliver the event. Accordingly a later network failure does not reopen the prepared handle and was not recorded as a contract defect. Normal Host ownership disposes the singleton Sentry client with a ten-second shutdown timeout, but current tests establish final envelope content only by calling `FlushAsync` explicitly; they do not inject a failing transport through full Host shutdown. This is a residual delivery/coverage limitation, not evidence of unsolicited transmission or a new candidate.

Local SDK rejection returns `SentryCaptureRejected` and makes the handle retryable. An exception thrown synchronously by any dispatcher also releases the handle, then reaches the combined Workbench exception filter for logging, correlation and capture. The same applies to unexpected projector/provider preparation failures. Genuine submission cancellation is likewise released and rethrown; Unit 6's cancellation classification issue applies to direct and fallback tool routes before the outer SDK dispatcher handles an uncancelled cancellation-shaped fault generically.

## Configuration, DI and status

The command line/default exclusively controls consent elevation: an ambient consent environment variable is ignored with a warning and invalid consent input fails closed to `Never`. Capacity, lifetime and byte settings accept command-line then environment values with bounded fallback and are copied into independently typed error-reporting options. Startup validation rejects unsupported enum/range/lifetime values. `prepare-error-report` and `submit-error-report` are omitted only for `Never`; all three names, including always-published `get-error-details`, remain reserved against plugins.

The Host registers the capture/projector/stores/consent/availability/filter and selected dispatcher as singletons. The optional `ISentryClient` is an owned singleton; Workspace lifecycle notification reaches the singleton consent observer. Full server status exposes provider, configured mode and global/session state but no destination credential. Workspace status queries the resolved Workspace ID/epoch and exposes the applicable process-local consent state. Unexpected plugin failure responses calculate availability from the captured Workspace and actual client elicitation capability; therefore `RWMCP2-017` also changes the user-facing availability from Workspace-approved to generic available when a multi-Workspace alias/path call fails.

## Representative outcomes

| Trace | Current outcome | Evidence assessment |
| --- | --- | --- |
| Sensitive unexpected tool failure | Raw detail is logged locally, bounded under a random correlation ID and returned only through the labelled local-details tool; the ordinary result is generic. | Focused and published-process source cover the fallback plugin path; current SDK inspection establishes the same filter route for direct tools. |
| Multi-Workspace failure selected by alias/path or case variant | The request can bind and resolve normally, but capture loses the Workspace because it recognises exact `workspace.workspaceId` only. | `RWMCP2-017`; current tests use exact camel-case ID or one Workspace. |
| Sensitive report preparation | Projection removes raw messages, paths, private identity and Workspace identity; dispatcher freezes exact preview bytes and digest without dispatch. | Projection/preparation tests and Sentry envelope integration establish the reviewed content boundary. |
| Disabled reporting | `Never` omits prepare/submit tools and availability is `DisabledByConfiguration`; local details remain. | Resolver, availability and registration tests cover the branches. |
| Prompt without elicitation | Preparation can still return review data; submission returns `ApprovalUnavailable` without dispatch. | Unit and published-process source cover the fail-closed path. |
| Submit once / Workspace / session approval | Prompt choice establishes only its documented process-local scope and dispatches the stored payload. | Focused tests cover session approval and consent-store Workspace state; there is no joined published elicitation fixture. |
| Decline / cancel / suppress | Decline discards, cancel retains, suppression discards and clears all temporary grants. | Focused tool/store tests cover these state effects. |
| Repeated/concurrent same-handle submission | Atomic `Prepared` -> `Sending` -> `Sent` prevents a second dispatch and retains the receipt until absolute expiry. | Store state tests cover each transition; current tool tests do not run concurrent calls against the real store. |
| Workspace close during authorised submission | Close invalidates the grant for later requests; a request which already obtained consent may complete dispatch of its immutable payload. | Current explicit-submission semantics are coherent; `RWMCP2-018` is rejected. |
| Dispatcher rejection/throw/cancellation | Rejection returns handled failure; a throw releases for retry and reaches Workbench capture; cancellation releases and is rethrown. | Focused tests cover rejection/throw and logging cancellation; SDK pipeline inspection establishes direct-tool filtering. |
| Logging fallback | Exact reviewed sanitised JSON is written to stderr only after accepted submission. | Unit and published-process source cover prompt-free `Always` submission. |
| Sentry acceptance and final payload | Official SDK queues one event whose final JSON equals the preview after final allow-listing. | In-memory-transport integration covers the event boundary without a real destination. |
| Sentry network failure | A failure after queue acceptance is background provider behaviour; the receipt means SDK acceptance, not remote ingestion. | Explicitly documented; current tests do not model failing full-Host transport or prove remote delivery. |
| Normal shutdown | DI owns store timers and optional Sentry client; client shutdown timeout is ten seconds. | Store timer disposal is integration tested; Sentry envelope flushing is tested only through an explicit client flush. |

## Candidate findings

| ID | Severity | Confidence | Summary |
| --- | --- | --- | --- |
| `RWMCP2-014` | P2 | High | Revalidated: an uncancelled `OperationCanceledException` from any tool route bypasses Workbench correlation and error-report availability before generic SDK handling. |
| `RWMCP2-015` | P2 | High | Revalidated: deliberate fallback-router protocol errors are captured as reportable unexpected failures. |
| `RWMCP2-017` | P2 | High | Capture recognises only exact `workspace.workspaceId`, losing valid alias/path/case-insensitive Workspace attribution when multiple Workspaces are open. |
| `RWMCP2-018` | Rejected | High | Lifecycle invalidation controls later temporary-grant reuse and does not retroactively revoke the explicit submission request which already obtained consent for the immutable payload. |

No additional candidate was recorded for external-field sanitisation, payload immutability/digest, prepared-store single-flight, SDK queue acceptance, background remote-ingestion semantics, DSN secrecy, redirect policy, absolute expiry, capacity isolation or conditional publication because current contracts, source and focused evidence are internally consistent at those reviewed boundaries.

## Test evidence

| Evidence | Result | Boundary established |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Test` | 480/480 passed | Capture bounds/classification, stores/expiry, projection, preparation, consent states, submission transitions, logging/Sentry mapping, options/configuration, status and fallback filter. |
| `Roslyn.Workbench.Mcp.IntegrationTest` | 68/68 passed | DI/lifetime composition, selected provider, timer disposal and final Sentry event-envelope equality through an in-memory transport. |
| Relevant acceptance source | Inspected, not executed | Published fallback-plugin correlation/local detail, prompt without elicitation, `Always` logging submission, stderr sanitisation and continued Host usability. |
| Sentry 6.8.0 local package contract | Inspected | `CaptureEvent` is the SDK acceptance/queue boundary and `FlushAsync` drains captured events. |
| Microsoft Learn System.Text.Json documentation | Inspected | `JsonSerializerDefaults.Web` performs case-insensitive property matching, confirming the runtime/capture mismatch in `RWMCP2-017`. |

The pinned .NET 10 SDK was available. WSL test commands used the required `/tmp/artifacts/roslyn-workbench-mcp` artefact routing. The acceptance suite was not executed because no acceptance artefact was changed and repository policy does not authorise an automatic acceptance run for this docs-only review. No real Sentry or other network endpoint was used.

The repository-required Roslyn MCP was not available in the active tool set, so solution and call-site navigation used current local source inspection. This is a tooling limitation, not an evidence-boundary expansion.

## Earlier-unit revisits

Unit 1's Workspace selector and lifecycle paths were reopened. The public selector's ID/alias/path and case-insensitive binding semantics establish `RWMCP2-017`; `WorkspaceSessionStore.RemoveWorkspace` and epoch replacement do invoke the consent observer, so the lifecycle invalidation producer itself is present. `RWMCP2-002` and `RWMCP2-003` remain resource-ownership defects but do not alter consent-store logic.

Unit 3's plugin catalogue participates in capture classification. External plugin identity is correctly generalised before dispatch, and provider/network code remains Host-owned rather than plugin-extensible. Unit 3's `RWMCP2-006` still generates a correlated captured error on the fallback path, but no Unit 7 mechanism repairs the admission/runtime mismatch.

Unit 6's exception routing was rechecked rather than assumed. `RWMCP2-014` applies across direct and fallback routes because the combined dispatcher is filtered; `RWMCP2-015` makes malformed fallback calls consume/report captured-error capacity. Final SDK inspection rejected `RWMCP2-016`: direct server-owned and Code Action failures, including synchronous preparation/dispatcher exceptions, do enter this Unit 7 workflow.

The architecture map was corrected to describe combined direct/fallback capture, scope consent invalidation to Workspace identity/epoch and describe Sentry success as SDK queue acceptance. No project, dependency, entry-point, composition-root, external boundary or extension-mechanism omission was otherwise found.

## Unit conclusion

The current reporting workflow has a strong data boundary: local sensitive detail is bounded and explicitly labelled, external content is allow-listed, preparation freezes the exact provider payload before consent, same-handle submission is single-flight, and final Sentry event content matches the review preview. One new P2 defect remains: valid multi-Workspace alias/path or case-insensitive selectors lose their Workspace association at capture. The earlier cancellation/protocol defects also remain directly relevant. Final validation rejected both the direct-tool bypass candidate and the claimed consent-revocation race. Review unit 7 is complete.
