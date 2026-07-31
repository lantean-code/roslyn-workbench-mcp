# Subsystem review: local diagnostics and approved error reporting

## Scope and relationships

This unit covers unexpected-tool exception capture, bounded local stores, consent/availability, sanitised external projection, immutable preparation, Sentry/logging dispatch and the three server-owned diagnostic/reporting tools. It consumes Host catalogues and Workspace session state but is isolated from transaction mutation.

## Implementation and boundary review

- The top-level call-tool filter rethrows cancellation and converts other unexpected failures to a correlation envelope after logging and storing a bounded local record.
- Local capture may contain exception messages, stack file paths and workspace identity. It is accessible only through the explicitly described local details tool and expires/caps by configured time, count and byte size.
- `ExternalErrorReportProjector` constructs a separate allowlisted payload: external tool names are generalised, messages/paths/identities are omitted, stack frames become component classes and workspace context contains only counts/state/revision.
- Preparation serialises the exact dispatcher payload, enforces the byte cap, stores it under an unguessable handle and returns its preview/digest without network activity. Submission atomically acquires the handle, applies consent/elicitation, releases it after cancellation/failure and returns the prior receipt after a successful send.
- Consent supports never, per-report prompt, workspace epoch and process session grants; workspace close/reload invalidates workspace grants. Sentry configuration disables default PII, redirects and global SDK state.

## Consumers, DI and configuration

Stores, projector, consent, availability and dispatcher are singletons. Consent/capacity/lifetime/byte limits are copied from validated startup options. Provider builds embed a fixed public Sentry destination; other builds dispatch approved payloads to stderr.

## Tests and conclusion

Unit and Host integration coverage lock capture bounds, sanitisation, consent choices, concurrent submission, retry and dispatcher payload equivalence. The richer captured record does not flow to either dispatcher. No validated finding originated in this unit.
