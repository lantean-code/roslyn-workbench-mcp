# Error reporting and privacy

Roslyn Workbench retains a bounded, process-local diagnostic record when an MCP tool fails unexpectedly. The ordinary tool response remains generic and correlated, while `get-error-details` lets the connected trusted agent inspect that one temporary record. Local details are marked `LocalDiagnostic` and `safeForExternalSubmission: false`; they can contain exception messages, stack information, tool identity and Workspace context and must not be copied directly to an external service.

External reporting is a separate, explicit workflow:

1. Configure a supported dispatcher destination and a consent policy at startup.
2. Call `prepare-error-report` with the correlation ID.
3. Review the returned destination, provider `payloadJson` string, SHA-256 digest, exclusions and expiry.
4. Call `submit-error-report` with only the opaque submission handle.
5. When the policy is `prompt`, the MCP client obtains user consent through form elicitation before the Host gives the immutable external report to the configured provider adapter.

Preparation performs no network activity. Submission cannot replace the report, destination or approval value, and no consent mode permits unsolicited submission. After an explicit accepted submission, the provider SDK may deliver its event through its normal background transport.

## External payload

The Host first creates a provider-neutral, versioned external report using an allow-list. That immutable report is the provider mapping source. The configured `IErrorReportDispatcher` creates and stores the provider-specific dispatch state and exposes its exact UTF-8 JSON as the `payloadJson` string rather than embedding provider JSON into the structured MCP response. A build with an embedded Sentry DSN uses the Sentry dispatcher. A build without one uses the logging dispatcher, which writes explicitly approved sanitised reports to stderr. Consent, sanitisation, MCP contracts and temporary stores do not depend on either dispatcher.

The external projection can include a random per-report identifier, coarse tool and exception classifications, bounded first-party/runtime stack categories, coarse Workspace state and counts, failure timing and duration, and Host, Roslyn, operating-system and process-architecture versions.

It excludes source and document content; user-authored identifiers and paths; repository, solution and project identity; user, machine and stable installation identity; environment variables and command lines; credentials and secrets; agent prompts and conversation content; raw logs; and arbitrary exception messages. External plugin and unknown private implementation details are generalised before the dispatcher sees the report.

The immutable external report, provider dispatch state and exact preview bytes are stored separately from the local diagnostic record. The report identifier is created during preparation, and successful duplicate calls return the original receipt. The Sentry adapter constructs the strongly typed `SentryEvent` during preparation, including its event ID and timestamp, and derives `payloadJson` directly from that event. On submission it gives a defensive copy of the prepared event to the official SDK for background delivery to the reviewed HTTPS destination. It uses a stable message template, a human-readable formatted message and a privacy-safe fingerprint derived from the tool, coarse exception classification, execution family and sanitised stack-component sequence. Report identity, timing, Workspace state and version values do not fragment issue grouping. Automatic stack attachment and assembly reporting are disabled, and a final post-processing allow-list removes SDK-added threads, stacks, modules, runtime/device contexts and every other unreviewed field before the envelope is queued. The final serialised event therefore matches the reviewed `payloadJson`; envelope routing headers remain provider transport metadata. Normal Host shutdown flushes the SDK queue. The client is also configured without global capture, automatic sessions, client reports or default PII, and its HTTP handler disables redirects and checks certificate revocation. The logging adapter instead submits the exact reviewed JSON as one structured error-level log entry on stderr and returns the report ID as its local reference.

Anonymous report content does not make network transport anonymous. A hosted provider can observe ordinary metadata such as source IP address. Preventing that requires a separately operated relay or anonymity network. Configure server-side IP suppression and data scrubbing at the selected provider as defence in depth.

## Consent and lifetime

`never` disables preparation and submission for the process lifetime. `prompt` requests one-report, Workspace or server-session approval. `always` skips the prompt for explicit submissions. Only exact command-line input can establish `never` or `always`; an ambient environment value cannot establish a permanent policy.

Workspace approval is process-local and tied to one Workspace ID and epoch. It is invalidated by close, reload, epoch change or restart. Session approval and “No, and don't ask again” suppression last only until restart. A normal decline discards the current prepared handle, while client cancellation retains it until expiry.

When a prompt is required, a client without MCP elicitation support receives `ApprovalUnavailable` and nothing is sent. Prepared handles and correlation IDs are opaque, process-local and expire absolutely; reading them does not extend their lifetime.

## Operational guidance

Official Sentry-enabled builds use a provider project dedicated to Roslyn Workbench reports with an application-owned public-key DSN embedded from `ROSLYN_WORKBENCH_SENTRY_DSN` during compilation. The variable is build input, not runtime or user configuration, and the DSN is not exposed through MCP. Builds without that input retain the complete workflow through the stderr logging dispatcher and never contact the maintainer's Sentry project. Restrict provider access to maintainers and keep private connection material outside the application. Disable report preparation and submission with `--error-reporting-consent never` when local diagnostics are sufficient.
