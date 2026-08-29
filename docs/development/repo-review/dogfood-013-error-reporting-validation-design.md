# DOGFOOD-013 — Error-reporting client usability

**Status:** Confirmed through published Codex and Sentry dogfood validation.

## Approved remediation

The startup policy remains `never|prompt|always`. No MCP tool can create a Workspace, session or elicitation override. In prompt mode, every prepared report uses one form with exactly three choices: **Yes, send it**, **Yes, without exception messages**, and **No, don't send it**.

Preparation now retains bounded exception messages because they can be necessary to diagnose a failure. Those messages are unfiltered reviewed content and may contain source text, paths, identifiers or secrets; the prepared response reports this warning separately from its list of excluded dedicated sources. Preparation also publishes a bounded exception chain and privacy-filtered structured stack frames. First-party Roslyn Workbench frames may include assembly, type, method, source file name and line number. Roslyn and .NET frames may include assembly, type and method but not source locations. External plugin exception types are generalised and external implementation frames are omitted. Dedicated absolute-path fields remain excluded.

The prepared submission remains one immutable complete report. Choosing the message-free option derives a strictly subtractive variant during submission by removing every exception message, rebuilding the provider payload and returning the digest of the payload actually sent. The original prepared report is not mutated and no second stored variant is required.

A client decline, cancellation, missing consent content or explicit **No** sends nothing and discards the prepared handle. These indistinguishable non-approved outcomes return `ErrorReportNotApproved` with guidance that, if no form appeared, client policy may have blocked elicitation and the agent can ask the user to enable manual MCP approvals, prepare a new report and retry. The result does not claim that the user declined.

## Validated existing coverage

The original error-reporting workflow was extensively tested. Unit coverage exercised captured-record projection, immutable payload preparation, consent states, elicitation outcomes, retry and idempotence state, logging dispatch, Sentry dispatch and privacy allow-lists. Integration coverage verified Host composition, schema publication, Workspace attribution and the outbound Sentry envelope. Published-host acceptance coverage used the existing `HostQuery` fixture to create a deterministic unexpected failure, inspect its local correlation record, prepare a Logging payload without dispatch, fail closed when client elicitation was unavailable, submit under `always` consent and verify the approved report reached standard error without the fixture's exception message or Workspace root.

No Scenario Runner definition invokes `prepare-error-report` or `submit-error-report`. Adding one would duplicate the existing published-host acceptance workflow and still would not validate whether Codex can understand the prepared payload, present it for approval and complete MCP elicitation.

## Resolved gap

The remaining item after remediation was a client-usability dogfood run through Codex's configured Roslyn Workbench namespace. It established that an agent can follow the correlated-failure continuation, distinguish the sensitive local diagnostic from the bounded external payload, present the exact destination and immutable payload before submission, respect a separate explicit user-consent boundary, complete MCP elicitation and understand the resulting receipt.

The live run revealed concrete production gaps: the prompt was unnecessarily complex, a client-policy decline was described as a user decline, and the external report omitted the exception message and useful first-party stack locations needed for diagnosis. Those gaps justify the approved production and test changes above. Existing published-host acceptance expectations must be updated to require the controlled bounded exception message in prepared and approved reports; a later dogfood run will validate the Codex-specific interaction.

## Trust boundary

At the user's direction, the DOGFOOD-013 candidate embeds the supplied public-key Sentry DSN at build time. Its isolated smoke test reports provider `Sentry`, consent mode `Prompt` and session state `PromptRequired`. Preparation performs no network activity, but an approved submission will send the reviewed event to the configured Sentry project through the SDK's HTTPS transport. The provider can observe ordinary transport metadata such as source IP address. Submission is state-changing, destructive and open-world and therefore requires explicit approval for the concrete prepared payload.

The DSN permits event ingestion but does not provide read access to the Sentry project. The Host receipt can prove SDK acceptance and expose the event reference, but final appearance in the Sentry project must be confirmed by a user with project access.

The local `get-error-details` result may contain the controlled exception message and Workspace context. It is marked `LocalDiagnostic` and `safeForExternalSubmission: false`; the local diagnostic object is never submitted directly. `prepare-error-report` copies only explicitly allow-listed bounded values, including the reviewed exception message, into the immutable payload that may be submitted.

## Proposed validation setup

Prepare a temporary dogfood-only candidate from the exact committed `HEAD` without modifying repository source or tests:

1. Publish the normal Release Host into a new versioned candidate.
2. Build and install the repository's existing `HostQuery` acceptance fixture into a private temporary plugin directory. This fixture exposes `host-valid-query` with an explicit `throw: true` switch and throws the fixed test-only message `Sensitive query failure.`; no new throwing tool or product backdoor will be added.
3. Publish with the user-supplied Sentry DSN as build input, then start the candidate through the existing configured `current` path with `--plugin-directory` pointing at the private fixture package. Preserve the Host's `Prompt` consent and do not use `always` consent.
4. Tee the candidate's standard error to a private temporary log while preserving normal stderr delivery. This retains startup, SDK and shutdown evidence but is not treated as proof that Sentry stored the event.
5. Smoke-test the isolated candidate, promote it atomically, and ask the user to restart Codex. Restore the normal non-fixture candidate after validation.

The candidate and plugin package are disposable validation infrastructure outside the repository. They must not be staged or committed.

## Proposed dogfood run

After restart:

1. Call full `server-status` through the configured namespace and require provider `Sentry`, consent mode `Prompt`, state `PromptRequired`, the expected committed plugin version and the temporary `host-valid-query` tool.
2. Open the main solution read-only and invoke `host-valid-query` with `throw: true`. Require the ordinary response to be a generic `UnhandledException` containing a correlation ID without the fixed exception message.
3. Call `get-error-details` with that correlation ID. Require `LocalDiagnostic`, `safeForExternalSubmission: false`, the controlled fixed message and the expected local tool classification. Do not reproduce its payload outside the dogfood evidence log.
4. Call `prepare-error-report` with the correlation ID. This is read-only with no network activity. Require dispatcher `Sentry`, the derived HTTPS destination, a non-empty opaque submission handle, expiry, SHA-256 digest, excluded-category list and parseable `payloadJson`.
5. Inspect the entire prepared payload before submission. Require it to include the bounded fixed exception message, the explicit warning that exception messages are unfiltered reviewed content and useful approved structured stack frames, while excluding dedicated repository-root, source-text and absolute-path fields, external plugin implementation details and other documented prohibited data sources. Require its digest to match the returned payload bytes.
6. Present the destination, exact prepared payload and digest to the user, then stop. Preparation is not consent to submit.
7. Only after the user explicitly approves calling the submission tool, invoke `submit-error-report` with the opaque handle and exercise the intended form choice. Validate both **Yes, send it** and, using a separately prepared report, **Yes, without exception messages**. No runtime override is available.
8. If Codex cannot complete MCP elicitation, require `ApprovalUnavailable` or `ErrorReportNotApproved` and no dispatch. For `ErrorReportNotApproved`, verify the result tells the agent that absent UI may mean client policy blocked elicitation and that it can ask the user to enable manual MCP approvals before preparing a fresh report. If elicitation succeeds, require a Sentry receipt whose digest matches the payload actually submitted and whose report reference identifies the accepted event. Allow normal SDK flushing and ask a user with Sentry access to confirm that event reference appears in the configured project with the reviewed content.
9. Close the Workspace and confirm no transaction owner exists. After Sentry-side confirmation, restore the normal dogfood candidate and remove the private fixture package and captured log after the evidence has been recorded.

## Success criteria

DOGFOOD-013 is confirmed when the complete workflow is understandable through Codex, preparation exposes enough diagnostic detail, the three-choice prompt is surfaced or fails closed with actionable guidance, both sending variants are strictly understood, no unapproved content is submitted, and any successful Sentry submission has the matching final digest and is confirmed in the configured project.

## Live validation outcome

Codex successfully followed the generic correlated failure into the local diagnostic and separate preparation tools. The local record was marked unsafe for external submission and contained the controlled exception message and repository path. The prepared Sentry event excluded those values and the correlation ID, and an independent SHA-256 calculation over the exact UTF-8 payload matched the returned digest. The user reviewed and explicitly approved that exact destination, payload and digest in chat.

The first `submit-error-report` attempt incorrectly treated preceding conversational approval as sufficient before invoking the separate MCP elicitation. The user identified that process error, so a fresh immutable payload was prepared and displayed, then submission was invoked specifically to let the Host's elicitation form collect one-report consent. No form was surfaced to the user: Codex immediately returned the elicitation action as a decline. The original Host returned `ErrorReportDeclined`, discarded the second handle and emitted no report; private stderr evidence contained neither prepared event identity nor an approved-report entry. This historical result motivated the approved `ErrorReportNotApproved` guidance above.

A later retry used the active profile's `on-request` approval policy. Codex surfaced the Host's MCP form, the user approved the fresh reviewed payload and Sentry accepted the event with the exact reviewed event reference and digest. This confirmed that the earlier immediate declines were caused by the task's effective `never` policy rather than a Codex elicitation defect. Published-host acceptance now validates the remediated three-choice form, explicit decline and cancellation, and both message-retaining and message-free dispatch variants. Codex-specific presentation remains a dogfood usability concern rather than an automated protocol gap.

The final published run exercised both positive choices through Codex's normal configured namespace. **Yes, send it** submitted event `e20803dc742c462482f1c747b9b5631d` with the same digest as the reviewed complete payload. A separately prepared report then used **Yes, without exception messages** and submitted event `aec10a3ab06744f289076fad03642019` with a different final digest, proving that dispatch derived the strictly subtractive message-free payload. The user observed the latter as an additional event on the existing Sentry issue, which is expected because the stable fingerprint deliberately excludes exception-message content. The controlled Workspace was closed with no transaction owner, the private HostQuery candidate was removed from `current`, and a restarted full status check confirmed the clean 56-tool candidate with only the bundled Core plugin.

## Rejected alternatives

### Treat existing acceptance coverage as dogfood confirmation

The acceptance tests prove the published Host and dispatcher but use an SDK client configured specifically for the test. They do not prove Codex's presentation, consent or elicitation behaviour.

### Trigger an arbitrary failure in a bundled production tool

Malformed requests produce supported contract failures rather than captured unexpected exceptions, while searching for a production crash would be unreliable and could exercise unintended state. The existing fixed throwing fixture is deterministic and already trusted by the acceptance suite.

### Retain the Logging fallback

The Logging dispatcher would validate the same preparation and consent mechanics locally, but it would not exercise the configured provider delivery requested by the user. The Sentry candidate therefore retains `Prompt` consent and requires a second payload-specific approval before making the external call.

### Add a permanent diagnostic failure tool or new scenario

A product-exposed throwing tool would create an unnecessary attack and maintenance surface. A new Scenario Runner case would duplicate acceptance coverage and would not exercise Codex.

Candidate preparation was explicitly authorised by the user. Submission requires a second, payload-specific approval after preparation.
