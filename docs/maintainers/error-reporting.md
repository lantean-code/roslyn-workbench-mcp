# Error-report provider implementation

The user-facing [privacy guide](../content/error-reporting.md) owns the reporting workflow, consent choices and disclosure limitations. This document records adapter implementation details.

## Request failure boundaries

The Host installs separate call-tool and read-resource filters. Unexpected resource failures become JSON-RPC internal errors whose data contains the same correlated diagnostic and reporting fields as a failed tool envelope. Both filters retain records in the same bounded, expiring store; the existing tools retrieve, prepare and explicitly submit them. The resource boundary preserves request cancellation, deliberate Host protocol rejections and SDK routing failures for unmatched URIs. A registered resource throwing an arbitrary protocol exception still passes through diagnostic capture.

Resource records use the fixed identity `resources/read`, execution family `Resource` and ownership `Host`. The existing `toolName` field is retained for wire compatibility. Resource capture bypasses workspace selection and never retains the request URI or request arguments; bounded exception messages and approved stack details retain their existing disclosure rules. No request filter submits a report.

The outer `Main` boundary catches otherwise unhandled managed failures across command handling, Host construction, execution and disposal. It writes a random diagnostic ID and the exception to stderr directly, then returns exit code 1. It does not depend on DI, logger availability or the process-local error store, and it does not dispatch a report. Existing known storage-failure diagnostics retain their direct non-zero return path. The Host's existing `using` lifetime remains inside the protected block; this handler does not resume a failed Host or install process-global exception hooks.

## Preparation and dispatcher boundary

The Host first creates a provider-neutral, versioned external report using an allow-list. That immutable report is the provider mapping source. The configured `IErrorReportDispatcher` creates and stores the provider-specific dispatch state and exposes its exact UTF-8 JSON as the `payloadJson` string rather than embedding provider JSON into the structured MCP response. A build with an embedded Sentry DSN uses the Sentry dispatcher. A build without one uses the logging dispatcher, which writes explicitly approved sanitised reports to stderr. Consent, sanitisation, MCP contracts and temporary stores do not depend on either dispatcher.

## Immutable dispatch and Sentry transport

The immutable external report, provider dispatch state and exact preview bytes are stored separately from the local diagnostic record. The report identifier is created during preparation, and successful duplicate calls return the original receipt.

The Sentry adapter constructs the strongly typed `SentryEvent` during preparation, including its event ID, timestamp and structured exception chain, and derives `payloadJson` directly from that event. On submission it gives a defensive copy of the prepared event to the official SDK for background delivery to the reviewed HTTPS destination. If message removal is selected, it rebuilds the event from the stored report without exception messages and returns the digest of those final bytes.

It uses a stable message template, a human-readable formatted message and a privacy-safe fingerprint derived from the tool, coarse exception classification, execution family and approved stack-component sequence. Report identity, timing, Workspace state and version values do not fragment issue grouping.

Automatic stack attachment and assembly reporting are disabled, and a final post-processing allow-list removes SDK-added threads, modules, runtime/device contexts and every other unreviewed field before the envelope is queued. The final serialised event therefore matches the selected payload; envelope routing headers remain provider transport metadata.

Normal Host shutdown flushes the SDK queue. The client is also configured without global capture, automatic sessions, client reports or default PII, and its HTTP handler disables redirects and checks certificate revocation.

The logging adapter instead submits the selected JSON as one structured error-level log entry on stderr and returns the report ID as its local reference.
