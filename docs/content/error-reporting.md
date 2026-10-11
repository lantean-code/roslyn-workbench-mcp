# Error reporting and privacy

Roslyn Workbench retains a bounded, process-local diagnostic record when an MCP tool fails unexpectedly. The ordinary tool response remains generic and correlated, while `get-error-details` lets the connected trusted agent inspect that one temporary record. Local details are marked `LocalDiagnostic` and `safeForExternalSubmission: false`; they can contain exception messages, stack information, tool identity and Workspace context and must not be copied directly to an external service.

## Prepare, review and submit

External reporting is a separate, explicit workflow:

1. Choose the consent policy at startup; the report destination is supplied by the build, not by the caller.
2. Call `prepare-error-report` with the correlation ID.
3. Review the returned destination, provider `payloadJson` string, SHA-256 digest, exclusions and expiry.
4. Call `submit-error-report` with only the opaque submission handle.
5. When the policy is `prompt`, the MCP client obtains user consent through form elicitation. The user can send the reviewed payload, send a derived variant with all exception messages removed, or send nothing.

Preparation performs no network activity. Submission cannot replace the report, destination or approval value, and no consent mode permits unsolicited submission. Message removal is strictly subtractive and does not mutate the stored report. After an explicit accepted submission, the provider SDK may deliver its event through its normal background transport.

## External payload

| Category | Handling |
| --- | --- |
| Report identity and versions | Random per-report ID; Host, Roslyn, OS and architecture versions |
| Failure context | Coarse tool/exception classification, timing, bounded messages and approved stack details |
| Workspace context | Coarse state and counts, not dedicated repository/project identity fields |
| Dedicated sensitive fields | No source/document content, absolute paths, credentials, environment, command line, prompts, conversations or raw logs |
| Exception messages | Bounded but **not filtered for secrets**; inspect them or choose the message-free variant |
| Network metadata | A hosted provider can observe ordinary transport metadata, including source IP |

### Review exception messages

Exception messages are bounded but otherwise unfiltered reviewed content and can nevertheless contain Workspace data supplied by the exception producer, including source text, paths, identifiers or secrets. The prepared response reports this limitation separately from its excluded-source list. Review the exact payload before submission or choose **Yes, without exception messages**.

### Stack information

The external projection can include a random per-report identifier, coarse tool and exception classifications, bounded exception messages, approved first-party/runtime exception and stack details, coarse Workspace state and counts, failure timing and duration, and Host, Roslyn, operating-system and process-architecture versions. Roslyn Workbench frames may include assembly, type, method, source file name and line number. Roslyn and .NET frames omit source locations. External plugin implementation frames are omitted.

### Preview and delivery

The prepared `payloadJson` is the provider-specific event preview, not the transport envelope. The submitted event matches the selected reviewed payload; provider routing headers are transport metadata. Choosing the message-free variant removes exception messages and returns its final digest without modifying the stored report.

Anonymous report content does not make network transport anonymous. A hosted provider can observe ordinary metadata such as source IP address. Preventing that requires a separately operated relay or anonymity network. Configure server-side IP suppression and data scrubbing at the selected provider as defence in depth.

## Consent and lifetime

`never` disables preparation and submission for the process lifetime. `prompt` requests consent for every prepared report. `always` skips the prompt for explicit submissions. Only exact command-line input can establish `never` or `always`; an ambient environment value cannot establish a permanent policy.

The prompt offers exactly **Yes, send it**, **Yes, without exception messages**, and **No, don't send it**.

It creates no Workspace or session override. Client decline, cancellation, missing response content and explicit **No** all send nothing and discard the current prepared handle.

Because a client-policy decline cannot be distinguished from a user action, the result is `ErrorReportNotApproved`; if no prompt appeared, its guidance tells the agent that the client may have blocked elicitation and that manual MCP approvals can be enabled before preparing a fresh report.

When a prompt is required, a client without MCP elicitation support receives `ApprovalUnavailable` and nothing is sent. Prepared handles and correlation IDs are opaque, process-local and expire absolutely; reading them does not extend their lifetime.

## Operational guidance

Official Sentry-enabled builds use a provider project dedicated to Roslyn Workbench reports with an application-owned public-key DSN embedded from `ROSLYN_WORKBENCH_SENTRY_DSN` during compilation. The variable is build input, not runtime or user configuration, and the DSN is not exposed through MCP. Builds without that input retain the complete workflow through the stderr logging dispatcher and never contact the maintainer's Sentry project. Restrict provider access to maintainers and keep private connection material outside the application. Disable report preparation and submission with `--error-reporting-consent never` when local diagnostics are sufficient.


Builds use an embedded application-owned Sentry destination or a stderr logging dispatcher. The caller cannot supply a destination. Provider implementation details are recorded in the [maintainer guide](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/docs/maintainers/error-reporting.md).
