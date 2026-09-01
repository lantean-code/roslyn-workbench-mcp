# User-Approved Error Reporting Proposal — 2026-07-30

Status: Implemented and validated on 2026-07-31; consent and diagnostic-content design amended for DOGFOOD-013 on 2026-08-28

## Purpose

Add a v1 diagnostic workflow that lets a trusted MCP agent inspect unexpected Roslyn Workbench failures locally and submit a bounded, reviewed report to a configured hosted service only under an explicit user-selected consent policy.

Early v1 usage is likely to expose unusual SDK, Workspace, Roslyn, operating-system and plugin combinations. Correlated, privacy-preserving reports will shorten diagnosis without introducing automatic telemetry or weakening the local trust model.

## Goals

- Preserve the existing safe unexpected-error response while making its correlation ID useful after the failure.
- Give the trusted local agent detailed diagnostic information for investigation.
- Never submit diagnostic information automatically.
- Make the complete proposed external payload available before submission.
- Guarantee that submission uses the immutable content previously prepared, with one strictly subtractive exception-message redaction option.
- Support per-report approval and explicit command-line permanent approval or rejection.
- Avoid repeated preparation attempts when reporting is unavailable.
- Exclude source code, project identity, user identity, secrets and stable installation identity from external reports.
- Keep error records and prepared submissions bounded, process-local and automatically expiring.
- Make explicit submission idempotent across concurrent and duplicate calls.
- Keep consent, correlation, sanitisation and MCP contracts independent of the initial reporting provider.

## Non-Goals

- Automatic crash telemetry, background submission or submission directly from an exception handler.
- Persisting raw diagnostics or consent across server restarts.
- Treating correlation IDs or submission handles as security boundaries.
- Guaranteeing network-layer anonymity from the hosted provider.
- Uploading raw logs, source documents, dumps, environment variables, process command lines or agent conversation content.
- Allowing agents or MCP tools to establish permanent consent.
- Adding error-reporting capabilities to the third-party plugin API.

## Existing Architecture

`UnhandledToolExceptionFilter` already catches unexpected non-cancellation exceptions at the top-level MCP tool boundary, creates a random correlation ID, records the exception through normal local logging and returns a generic `UnhandledException` envelope containing the correlation ID. Full exception details are not currently retained in an addressable store.

This filter remains the capture boundary. The implementation must continue logging to stderr while also creating a bounded immutable diagnostic record. It must not scrape stderr later or depend on a logging provider retaining raw output.

ModelContextProtocol 1.4.1 already exposes form elicitation through `McpServer.ElicitAsync`, `ClientCapabilities.Elicitation`, `ElicitRequestParams` and `ElicitResult`; this proposal does not require an MCP SDK 2.0 upgrade. Any SDK upgrade remains a separate compatibility change.

## Host-Owned Tool Surface

The Host publishes up to three server-owned tools:

| Tool | Behaviour | Purpose |
| --- | --- | --- |
| `get-error-details` | Read-only, idempotent, closed-world | Returns the bounded local diagnostic projection for a correlation ID. |
| `prepare-error-report` | Read-only, non-idempotent, closed-world | Produces and stores one complete bounded external payload for review without network activity. |
| `submit-error-report` | State-changing, idempotent, destructive, open-world | Applies the effective consent policy and submits one previously prepared immutable payload. |

`get-error-details` remains available independently of external-reporting consent. The Host includes a provider destination as application configuration, so `prepare-error-report` and `submit-error-report` are published whenever the startup consent mode is not `never`. All three names remain reserved against plugin collisions regardless of conditional publication.

Consent does not alter `tools/list`; the published list remains fixed for the process lifetime.

The protocol factory must allow server-owned tools to publish `openWorldHint` and `idempotentHint` independently. `submit-error-report` is the first Host-owned tool with an external network effect and therefore publishes `openWorldHint: true`.

## Correlated Error Capture

When an unexpected tool exception occurs, the filter creates one immutable `CapturedErrorRecord` and stores it under the correlation ID. Capture occurs once while the exception and invocation context are available.

The record contains bounded, explicitly selected fields:

- correlation ID and failure time;
- tool name and execution family;
- operation duration and cancellation state;
- exception type, message, stack frames and a bounded inner-exception chain;
- bounded invocation breadcrumbs collected before the failure;
- server, Roslyn and .NET runtime versions;
- operating-system family and processor architecture;
- safe plugin classification;
- Workspace lifecycle and transaction state where applicable;
- Workspace epoch and transaction revision;
- project and document counts where cheaply available;
- language and whether generated, linked, miscellaneous or loaded-solution documents were involved;
- selector category, symbol category, accessibility, source-or-metadata origin, syntax or operation category, diagnostic IDs and relevant configured bounds where explicitly recorded by the failing path.

The capture path must not retain the live `Exception` object. It must not traverse arbitrary object graphs or include `Exception.Data`, raw Roslyn objects, environment variables, command lines, credentials, source documents or complete logs. Exception-chain depth, stack frames, breadcrumbs, field lengths and total record size are bounded before insertion.

Correlation IDs remain random GUID-derived values and are not authentication credentials. Unknown and expired IDs return a structured failure.

## Local Diagnostic Inspection

`get-error-details` accepts a correlation ID and returns the retained local diagnostic record. It is intended for the trusted agent that the user has chosen to operate over a fully trusted Workspace.

The local projection may include unsanitised exception messages, stack information, paths and user-authored identifiers needed to diagnose the failure. It is labelled:

```json
{
  "sensitivity": "LocalDiagnostic",
  "safeForExternalSubmission": false
}
```

The response must still exclude deliberately credential-bearing or unbounded sources such as environment variables, process command lines, arbitrary exception data, complete raw logs and source-document dumps. It must never be accepted as input to `submit-error-report`.

Using an MCP agent entails trusting that agent and its configured model data boundary with the local information exposed by the server. Enterprise deployments are responsible for selecting and configuring an agent appropriate to their data policy. Local diagnostic inspection does not require a separate reporting-consent prompt.

External reporting may be disabled without disabling local correlated diagnostics.

## Reporting-State Projection

Every unexpected correlated error response includes concise diagnostic and reporting availability so the agent does not attempt an unavailable workflow:

```json
{
  "error": {
    "code": "UnhandledException",
    "message": "The tool failed unexpectedly.",
    "correlationId": "correlation-id"
  },
  "diagnostics": {
    "detailsAvailable": true,
    "detailsTool": "get-error-details"
  },
  "reporting": {
    "state": "Available",
    "canPrepare": true,
    "prepareTool": "prepare-error-report"
  }
}
```

Reporting states are:

- `Available` — a provider is configured and this report requires elicitation;
- `AlwaysApproved` — startup policy permits submission calls without elicitation;
- `DisabledByConfiguration` — startup policy is `never`;
- `ApprovalUnavailable` — approval is required but the connected client does not support elicitation.

`canPrepare` is false for disabled configuration. When client capability is known at the failure boundary, it is also false for `ApprovalUnavailable`; otherwise submission detects the unsupported capability and fails closed without network activity.

Full `server-status` reports the effective non-sensitive provider and configured consent state: `Disabled` for `never`, `PromptRequired` for `prompt` or `AlwaysApproved` for `always`. `workspace-status` reports the same configured consent state. Provider credentials and destination connection details are never included in status output.

## Report Preparation

`prepare-error-report` accepts only a correlation ID. It obtains the immutable captured record, selects externally useful fields through an allow-list projection and applies field-specific sanitisation to construct the immutable versioned external report.

Preparation:

- performs no network activity;
- creates a random submission handle unrelated to the correlation ID;
- assigns the stable external report identifier;
- creates the provider-specific dispatch state and serialises its exact JSON into canonical UTF-8 bytes;
- calculates and returns a SHA-256 digest of the preview bytes;
- stores the immutable external report and preview with the handle, destination identity, expiry and submission state; and
- returns that JSON as an opaque `payloadJson` string together with the destination, digest, expiry and excluded or redacted categories so provider JSON is not confused with the structured MCP response.

The tool description instructs the agent to present the payload and destination to the user before invoking `submit-error-report` when prompting is required.

Raw logs and the local diagnostic projection never form the submission payload. Preparing a report does not establish consent and does not change Workspace or transaction state.

## External Diagnostic Content

External projection is an allow-list transformation, not a claim that arbitrary strings can be made safe through generic redaction.

Where useful and safe, the report may include:

- a safe exception classification;
- a safe known message template or categorised failure reason;
- bounded exception messages, subject to explicit per-submission removal by the user;
- approved Host, framework and Roslyn exception types and stack-frame details;
- first-party source file names and line numbers, without absolute paths;
- bounded sanitised breadcrumbs;
- failure time, tool name, execution family, duration and cancellation state;
- server, Roslyn and .NET versions;
- operating-system family and processor architecture;
- structural Workspace, transaction and Roslyn operation context;
- compiler and built-in IDE diagnostic IDs; and
- external report ID, payload schema version and report format version.

Bundled component identity and version may be included. External plugin identity is generalised unless an explicitly reviewed field can be shown not to expose a user-owned assembly, organisation or project.

The report excludes dedicated fields and data sources for:

- source text and document contents;
- user-authored identifiers, symbols, namespaces, types and members;
- file names other than approved first-party source file names, and absolute or relative paths;
- repository, solution and project names;
- user-owned assembly and analyser identities;
- Git remotes, branches and commit identifiers;
- user, machine, host and stable installation identities;
- environment variables and process command lines;
- credentials, tokens, connection strings and secrets;
- agent prompts and conversation content;
- raw Roslyn objects and their string representations; and
- external plugin implementation frames, absolute stack source paths and unselected log data.

These exclusions do not imply content filtering within exception messages. Exception messages are deliberately present in the prepared report because they are often necessary to diagnose a failure and the complete prepared payload is available for review before submission. Messages are bounded but otherwise unfiltered and may contain source text, paths, identifiers or secrets originating from the exception producer. The prepared response distinguishes its excluded dedicated sources from this review warning. Prompt mode therefore offers a case-by-case choice to remove every exception message before dispatch. This redaction does not alter exception types, approved stack frames or any other prepared field.

Compiler or built-in diagnostic identifiers may be included only when their provenance is known. Unknown project analyser identifiers are excluded or generalised because they may reveal private tooling.

No payload field may intentionally link separate anonymous reports to one user or installation. The provider can still observe ordinary network metadata such as the source IP; preventing that requires an external relay or anonymity network and is outside this proposal.

## Consent Model

The startup option is:

```text
--error-reporting-consent never|prompt|always
```

`prompt` is the default. `never` and `always` are accepted only as explicit command-line choices so ambient environment configuration, fallback behaviour or an MCP request cannot establish a permanent decision.

- `never` prevents report preparation and submission for the complete process lifetime.
- `prompt` uses form elicitation for every prepared report.
- `always` bypasses elicitation for explicit submission calls.

`always` does not enable background submission. Every report must still be captured, explicitly prepared and passed to an explicit `submit-error-report` call.

When prompting is required, `submit-error-report` uses MCP form elicitation with this concise choice set:

- **Yes, send it**
- **Yes, without exception messages**
- **No, don't send it**

The complete payload was already returned by preparation. The elicitation identifies the destination. If the user chooses the message-free variant, the dispatcher derives it from the stored immutable report by removing only exception messages and returns the digest of the payload actually sent.

The choices have these effects:

| Choice | Current report |
| --- | --- |
| Yes, send it | Submit the prepared payload unchanged. |
| Yes, without exception messages | Remove all exception messages from the stored report, rebuild the provider payload and submit that variant. |
| No, don't send it | Do not submit and discard the prepared handle. |

Client-level decline, cancellation, an empty response or the explicit “No” choice sends nothing, establishes no persistent decision and discards the current prepared handle. These outcomes return `ErrorReportNotApproved`. Its guidance says that, when no form appeared, the client may have blocked MCP elicitation and the agent can ask the user to enable manual MCP approvals, prepare a new report and retry. The server does not claim that the user declined because client policy and a user action are not distinguishable in this result.

If approval is required and the client does not advertise elicitation, submission returns `ApprovalUnavailable` without sending. A Boolean, phrase or replacement approval value supplied by the agent is never accepted as proof of consent.

## Configuration

Startup configuration covers:

- reporting provider and destination;
- `--error-reporting-consent never|prompt|always`;
- correlated-error lifetime and capacity;
- prepared-submission lifetime and capacity;
- maximum captured-record and external-payload bytes; and
- bounded provider timeout, response and provider-specific transport settings.

Exact option names and curated defaults for the bounds are fixed during implementation and published through `docs/content/configuration.md`. Validation applies hard upper limits so command-line input cannot create unbounded retention or response sizes.

The provider destination and public submission DSN are application-owned configuration rather than user options. The DSN is treated as a routing identifier rather than a secret, while any private credential or connection material remains outside the Host and MCP contracts.

Invalid configuration must never broaden consent. In particular, malformed `never` or `always` input cannot silently become the opposite permanent policy, and only an exact explicit command-line `always` value establishes permanent approval.

## Submission

`submit-error-report` accepts only the opaque submission handle. It cannot accept replacement diagnostic fields, payload bytes, destination values, consent flags or provider options.

The submission store tracks `Prepared`, `Sending` and `Sent` states under one concurrency boundary:

- exactly one caller may transition a prepared report to sending;
- concurrent callers observe the in-progress state without starting another network operation;
- successful provider identity and result remain available until expiry;
- retries after success return the original result;
- local provider-SDK rejection retains a retryable prepared state.

An accepted submission returns the provider name, provider-assigned event reference and preview digest. Acceptance means that the provider SDK accepted the event for delivery; it does not assert remote ingestion and does not return credentials or a URL containing secret material.

## Temporary State and Capacity Isolation

Correlated errors and prepared submissions are process-local. A server restart invalidates them.

The implementation uses separate stores and capacity budgets:

- the correlated-error store owns diagnostic records indexed by correlation ID;
- the prepared-submission store owns immutable external reports, representative previews and submission state indexed by submission handle.

These budgets remain isolated from Workspace query results and Code Action replay references so one workload cannot evict another feature's security- or correctness-sensitive state. Each record has an absolute expiry; access does not extend it. Expiration callbacks and explicit lifecycle invalidation remove index entries, successful results and abandoned state.

Implementation must define curated defaults and hard upper bounds for record count, record bytes, prepared-submission count, payload bytes and lifetime. `TimeProvider` and deterministic concurrency seams support expiry and race tests.

## Provider Abstraction and Sentry

Consent, correlation, local inspection, sanitisation and temporary state do not depend on a hosted provider. An immutable provider-neutral external report is passed to an internal `IErrorReportDispatcher` owned by the Host; the dispatcher owns the strongly typed SDK mapping, exact provider event preview and transport. The event ID, timestamp and exact provider body bytes are created during preparation and form part of the reviewed preview. Provider-owned envelope routing and delivery metadata are added during submission.

Sentry is the hosted provider target. Open-source sponsorship may influence hosting cost but is not an architectural dependency. A Sentry DSN supplied through `ROSLYN_WORKBENCH_SENTRY_DSN` during compilation is embedded in the Host assembly and selects the Sentry dispatcher. Without that build input, the built-in logging dispatcher writes explicitly approved bounded reports to stderr, so a source fork does not inherit the maintainer's Sentry destination. The Sentry adapter uses an isolated client from the official Sentry SDK to construct and submit a deliberately minimal strongly typed event. It does not initialise the global static SDK, automatic exception capture or automatic sessions over live exceptions.

The Sentry event uses a stable message template and separate formatted message. It maps the bounded approved exception chain to Sentry's structured exception and stack-trace fields, including first-party file names and line numbers where symbols supplied them. Its explicit privacy-safe fingerprint includes the tool, coarse exception classification, execution family and sanitised stack-component sequence, but excludes per-report identity, timing, Workspace state and version values that would unnecessarily fragment issue grouping.

The stable external report ID and Sentry event ID are generated during preparation and included in the reviewed report. The configured Sentry project should enable IP-address suppression and server-side data scrubbing as defence in depth. The DSN public key is a routing identifier, but provider credentials, private connection material and mutable endpoint selection never cross the MCP boundary.

The provider abstraction should permit a Sentry-compatible hosted or self-hosted alternative such as GlitchTip or Bugsink without changing tool contracts or consent behaviour. A project-owned relay may be added later if schema enforcement, abuse controls or provider independence justify its operational cost.

Outbound requests use a configured HTTPS destination, bounded timeouts, bounded response bodies and controlled redirect behaviour. The provider result is validated and projected before it reaches the MCP response.

## Architecture Placement

This is a Host-owned feature under a responsibility-based `ErrorReporting` area in `Roslyn.Workbench.Mcp`. It does not belong to Workspace, CodeActions, Plugins or Plugins.Core and does not extend the public plugin authoring contract.

The top-level exception filter receives focused capture and availability collaborators. Tools receive the stores, consent service, projection service and provider abstraction through constructor injection. Provider transport must not receive the local diagnostic projection or live exception.

Server-owned tool registration, protected-name composition, metadata, schemas, status projection and tool-count evidence must include the conditionally published reporting tools. No tool may write source files or interact with the Workspace transaction pipeline.

## Validation Strategy

### Unit coverage

- bounded immutable exception capture, including inner-chain, frame, breadcrumb and size limits;
- no retention of live exceptions or prohibited context sources;
- local-detail projection and unknown or expired correlation IDs;
- allow-list external projection and every excluded data category;
- canonical representative-preview serialisation and stable preview digest;
- submission-handle opacity and separation from correlation and provider identifiers;
- consent choice mapping, command-line-only `never` and `always`, and subtractive exception-message redaction;
- unsupported elicitation fails closed;
- reporting-state projection for every availability and consent state;
- prepared, sending, sent, retry and expiry state transitions;
- concurrent duplicate prevention and stable provider idempotency identifiers;
- conditional tool registration, reserved names and open-world annotations; and
- provider timeouts, bounded responses, redirects and safe result projection.

### Integration coverage

- an unexpected plugin, Code Action and server-owned tool exception creates one correlated record and retains safe MCP failure containment;
- `get-error-details` returns the matching local record without exposing unrelated records;
- preparation performs no network activity and returns the complete representative preview;
- submission passes the stored immutable external report to a local fake provider;
- send, send without exception messages, decline and cancel work through a real MCP elicitation-capable client;
- no-elicitation clients cannot submit in prompt mode;
- `never` omits reporting tools while retaining local details and reserved names;
- `always` skips prompting but still requires preparation and explicit submission;
- `server-status`, `workspace-status`, `tools/list` and unexpected-error projections agree; and
- local provider-SDK rejection leaves the prepared report retryable.

### Published-host acceptance

Published acceptance uses a local deterministic provider and an elicitation-capable test client; it never contacts Sentry or another external service. It proves local inspection, representative preview, explicit approval, immutable-report submission, exception-message redaction, decline, unavailable-state guidance, no automatic traffic and continued Host operation after the original exception.

Provider contract tests may validate Sentry envelope shape and duplicate identifier reuse without sending production diagnostic data.

## Documentation

Implementation updates:

- release-facing error-reporting and privacy guidance;
- `docs/content/configuration.md` with destination, consent, bounds and status behaviour;
- `docs/content/tool-discovery.md` with conditional publication and external-effect annotations;
- trusted-Workspace guidance explaining the local agent data boundary;
- `server-status` and `workspace-status` response documentation; and
- the release security and trust-boundary audit.

Documentation must distinguish local diagnostic disclosure from reviewed external submission and must state that exception messages can contain Workspace data and that a hosted provider can observe network metadata.

## Implementation Order

1. Add the correlated immutable error-record model, bounded store and invocation diagnostic context at the existing exception boundary.
2. Add `get-error-details` and the diagnostic availability projection.
3. Add the allow-list external projection, representative preview schema and prepared-submission store.
4. Add startup provider and consent configuration, validation, status projection and protected-name composition.
5. Add the consent state service and MCP elicitation workflow.
6. Add `prepare-error-report` and `submit-error-report` with immutable-report and idempotent state transitions.
7. Implement and validate the Sentry provider behind the internal abstraction.
8. Complete unit, integration and published-host acceptance coverage.
9. Publish release-facing privacy, configuration, tool and trusted-agent documentation.
10. Re-run the pre-release security and trust-boundary audit against the completed feature.

## Acceptance Criteria

- Unexpected tool errors retain generic correlated MCP failures and normal local stderr logging.
- Correlated records are immutable, bounded, temporary and available through `get-error-details`.
- Local details are clearly marked unsafe for external submission and exclude deliberately credential-bearing or unbounded sources.
- Reporting availability is projected with each unexpected error so agents do not prepare unavailable reports.
- No diagnostic information is submitted automatically under any consent mode.
- Preparation performs no external communication.
- A complete representative provider preview, destination and preview digest are available before submission.
- Submission accepts only the opaque handle and dispatches either the exact reviewed provider event or its strictly subtractive message-free variant; the provider adds only envelope routing and delivery metadata.
- Dedicated fields for source code, project identity, user identity, stable installation identity and secrets are absent from external payloads; the prepared response warns that bounded exception messages are unfiltered reviewed content and may contain those values.
- Prompt mode offers only send, send without exception messages and do not send; it establishes no runtime override.
- Explicit command-line `never` and `always` policies cannot be established through an MCP call or ambient fallback.
- Unknown and expired correlation IDs and submission handles are rejected.
- Concurrent calls and retries cannot create duplicate provider reports.
- Temporary error and submission state expires and remains capacity-isolated.
- Reporting-disabled operation retains local correlated diagnostics.
- Sentry is replaceable without changing MCP contracts, sanitisation or consent behaviour.
- The implementation follows existing Host composition, dependency-injection, result, testing, documentation and CRLF conventions.
