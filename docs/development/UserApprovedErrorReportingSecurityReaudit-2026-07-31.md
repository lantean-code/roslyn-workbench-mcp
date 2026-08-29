# User-Approved Error Reporting Security Re-audit

Date: 2026-07-31

## Scope and result

This re-audit applies the security and trust-boundary portions of the [Pre-release Readiness Audit](PreReleaseReadinessAudit-2026-07-24.md) to the completed [User-Approved Error Reporting Proposal](UserApprovedErrorReportingProposal-2026-07-30.md). It covers unexpected-error capture, local agent disclosure, external projection, consent, temporary state, provider dispatch, transport configuration, MCP publication and status projection.

No new critical, high or medium release-blocking finding remains in the reviewed feature. The implementation preserves the existing generic MCP failure and stderr log, adds bounded process-local diagnostic inspection, and requires a separately prepared immutable payload plus an explicit submission call before any external effect.

## Trust-boundary findings

| ID | Result | Evidence and disposition |
| --- | --- | --- |
| `ER-SEC001` | Resolved | Capture is performed once at the top-level exception boundary into an immutable bounded model. Exception chains, messages, stack frames, paths, field lengths, record size, capacity and absolute lifetime are bounded. The store does not retain a live `Exception`, raw Roslyn objects, logs, environment variables, process arguments or arbitrary object graphs. |
| `ER-SEC002` | Resolved | `get-error-details` is always available for the trusted local agent and labels its response `LocalDiagnostic` with `safeForExternalSubmission: false`. Lookup is limited to the supplied opaque correlation ID and unknown or expired IDs are rejected. Release guidance now states that this local disclosure is distinct from external-report consent. |
| `ER-SEC003` | Resolved | External preparation uses a provider-neutral versioned projection built from an allow-list. It excludes dedicated fields for paths, source, user-authored identifiers, Workspace identity, repository and project identity, user and machine identity, stable installation identity, environment and command-line data, credentials, prompts, conversation content and raw logs. Bounded exception messages are unfiltered reviewed content and may contain those values; preparation warns about this explicitly and submission offers a strictly subtractive message-free variant. Unknown and external implementation details are generalised before dispatch. |
| `ER-SEC004` | Resolved | Provider-specific payload construction and transport are isolated behind `IErrorReportDispatcher`. The initial `SentryErrorReportDispatcher` receives only the provider-neutral projection, and Host composition evidence proves that another dispatcher can replace it without changing consent, sanitisation, MCP contracts or temporary stores. |
| `ER-SEC005` | Resolved | Preparation performs no network operation and stores the immutable allow-listed external report in a capacity-isolated absolute-expiry store. The exact strongly typed Sentry event body, including its event ID and timestamp, is returned with its destination and SHA-256 digest before submission. `submit-error-report` accepts only the opaque handle and dispatches those reviewed bytes or the strictly subtractive message-free variant; only provider envelope routing and delivery metadata are added later. |
| `ER-SEC006` | Resolved | Consent fails closed. `never` and `always` can be established only by exact command-line input; invalid input becomes `never`, and the ambient consent environment variable is ignored with a warning. Prompt mode requires advertised MCP form elicitation for each prepared report and offers only send, send without exception messages or do not send. Decline, cancellation, missing response content and explicit do-not-send all discard the prepared handle without dispatch. Approval unavailability and invalid choices release the handle for an explicit retry. No choice creates a Workspace or session override, and provider background delivery begins only after explicit SDK acceptance. |
| `ER-SEC007` | Resolved | Prepared-state transitions serialise concurrent senders. SDK acceptance completes the submission and successful duplicate calls return the original receipt; local SDK rejection returns the handle to a retryable state. The report identifier remains stable independently of the provider-assigned event identifier. |
| `ER-SEC008` | Resolved | The HTTPS public-key Sentry DSN is supplied as build input and embedded in official Sentry-enabled binaries rather than committed to source or exposed as a user option. Builds without the input select the stderr logging dispatcher, preventing forks from inheriting the maintainer's destination. The official SDK is configured without global capture, automatic sessions, client reports or default PII. Its HTTP handler disables redirects and checks certificate revocation, and the DI-owned client flushes its delivery queue during normal Host shutdown. MCP status exposes the effective provider name and consent state but never the DSN or key. |
| `ER-SEC009` | Resolved | Reporting tools are Host-owned, transaction-independent and protected against plugin name collisions. Local detail remains published when reporting is disabled; preparation and submission are omitted only under `never`. Submission alone carries the open-world external-effect annotation. |

## Residual risks

| Risk | Disposition |
| --- | --- |
| Hosted-provider network metadata | A provider can observe ordinary transport metadata such as source IP address even though the payload body is anonymous. Release guidance states this explicitly and recommends provider-side IP suppression and data scrubbing. A relay or anonymity network remains a separate operational feature. |
| Trusted local agent disclosure | Local exception messages and Workspace context are intentionally available to the connected agent for diagnosis. Deployments must select an agent and model data boundary compatible with their policy; local details must not be copied into an external report. |
| Same-user compromise | A malicious plugin, Workspace build, analyser or process already executing as the same user can access Host memory and network authority. Error reporting does not claim to sandbox same-user code and does not weaken the existing trusted-code boundary. |
| Provider duplicate semantics | Ambiguous retries reuse the same Sentry event ID and rely on the configured provider honouring its duplicate semantics. The dispatcher boundary permits replacement if a selected provider cannot supply an equivalent stable identifier contract. |
| Temporary sensitive memory | Local records remain in process memory until absolute expiry, capacity eviction or restart. Separate bounded stores and no persistence limit exposure; operating-system process isolation remains authoritative. |

## Validation evidence

- Normal solution restore and build succeeded with zero warnings and errors.
- The affected production and test projects passed the SDK `latest-all` analyzer build with no retained diagnostics in changed files.
- Host unit coverage includes projection exclusions, immutable prepared-event dispatch through the official Sentry SDK, consent decisions, expiry, concurrency, configuration, metadata and status behaviour.
- Focused Host composition and status integration coverage passed with eight tests, including replacement of the Sentry dispatcher through `IErrorReportDispatcher`.
- The complete published-Host acceptance suite passed with 55 tests, including local correlated detail inspection, sanitised preparation, no automatic traffic, fail-closed submission without elicitation and continued Host operation.
- The broader Host integration run passed 55 of 58 tests. The three failures are outside this feature: two Workspace containment cases construct an incomplete service collection without `IHostApplicationLifetime`, and plugin discovery retains a stale two-tool expectation for a fixture that now publishes three tools. The focused affected integration surface is green.

## Release disposition

The feature is suitable to proceed with the remaining release-candidate work subject to the accepted residual risks above. A provider project must enable IP suppression and server-side scrubbing before production use. Future dispatchers must preserve the provider-neutral input boundary, immutable reviewed-event contract, stable retry identifier and safe projected result.
