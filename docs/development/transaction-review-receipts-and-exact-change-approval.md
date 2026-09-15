# Transaction review receipts and exact-change approval

## Status and scope

This document defines the proposed design for roadmap item 5: producing a canonical transaction review receipt and activating the `approval-required` operational mode. It is an implementation design, not a statement of current behaviour. The feature is not complete until the receipt contract, approval workflow, final Workspace enforcement, documentation and automated evidence described here have been implemented and reviewed together.

The feature protects against an authorised MCP client or AI agent committing a different staged change from the one presented for approval. It does not authenticate a human approver, prove that the approver understood the change, constrain unrelated tools running as the same operating-system user or make untrusted Workspace build inputs safe.

Compiler-impact validation remains roadmap item 6. The receipt exposes only validation implemented at the time of this work and must not imply that compiler or analyser validation has occurred.

## Supported workflow

Approval-required operation uses the following workflow:

1. Call `transaction-start`.
2. Stage one or more mutations.
3. Call `transaction-review` with the current snapshot precondition.
4. Inspect the returned receipt and request bounded document diffs where more detail is required.
5. Call `transaction-commit` with the current snapshot precondition and returned `receiptId`.
6. Respond to the Host's approval elicitation for that exact receipt.
7. Allow the Host to rebuild and revalidate the exact persistence set before it writes source files.

`transaction-preview` is not published and cannot be invoked in approval-required mode. It remains the lightweight review operation in transactional and autonomous-trusted modes. Inspection-only publishes neither review operation because it cannot create a transaction.

| Operational mode | Review operation | Commit authorisation |
| --- | --- | --- |
| Inspection-only | None | Commit unavailable |
| Transactional | `transaction-preview` | Confirmation for one commit or the server session |
| Approval-required | `transaction-review` | One-use approval for the supplied receipt |
| Autonomous-trusted | `transaction-preview` | None |

## Agent guidance and mode transitions

An agent must not be expected to infer the active workflow from a previously used operational mode. The Host publishes mode-specific server instructions after startup policy has been resolved. Approval-required instructions tell the client to call `transaction-review` after staging, inspect the receipt and any required diffs, then pass the returned `receiptId` to `transaction-commit`; they also state that `transaction-preview` is unavailable in this mode.

The effective mode and receipt-approval requirement remain visible through `server-status`. The `transaction-review` description identifies it as the required review operation, and its successful result returns a continuation to `transaction-commit` with guidance to supply the receipt identifier. Missing, expired, consumed or stale receipt failures return a continuation to call `transaction-review` again.

There is no unpublished compatibility implementation of `transaction-preview`. An agent reconnecting under a different mode must use the newly negotiated server instructions and effective `tools/list`. This preserves the existing rule that a tool excluded by policy is physically unreachable rather than merely omitted from discovery.

## Mode-specific tool composition

Approval-required mode publishes `transaction-review` instead of `transaction-preview`. It also publishes a receipt-aware implementation of `transaction-commit` whose request requires the normal Workspace selector, `expectedSnapshot` and `receiptId`. Transactional and autonomous-trusted modes retain the existing commit request without a receipt field.

Separate mode-specific registrations may share the public `transaction-commit` name, but only one implementation and one request schema may be composed for a process. Receipt-specific request fields, result fields, descriptions, examples and supporting services are composed only when the effective commit-authorisation policy is receipt approval.

All Host-owned names remain reserved regardless of publication. `transaction-review` is therefore reserved in every mode even though it is published only for approval-required operation.

Approval-required remains startup-rejected until every component in this document is available. Activation changes the profile mapping to source mutation enabled and receipt approval required; composition failure must stop startup and must never fall back to confirmation or autonomous commit.

## Review request and result contract

`transaction-review` requires a Workspace selector and `expectedSnapshot`. Optional detailed-diff inputs follow the existing bounded preview model: a caller explicitly selects one document, chooses whether to include its diff and supplies the permitted context-line count. The operation does not return unbounded solution-wide source diffs.

The result contains:

- an opaque process-local `receiptId`;
- the receipt expiry time;
- Workspace ID and epoch;
- snapshot ID and transaction revision;
- canonicalisation algorithm identifier and change-set digest;
- added, modified and deleted document counts;
- deterministic per-document line summaries;
- affected project references;
- the mutation operations and summaries recorded through the active transaction history;
- definitive document classification supported by existing Workspace policy;
- containment and other completed commit-planning validation results; and
- the explicitly requested bounded document diff, when present.

The result distinguishes identity-bearing fields from explanatory projections. Commit authorisation is bound to the canonical persistence identity; line summaries, display names and prose are review aids and are not alternative identities.

Receipt generation acquires a shared Workspace lease, validates `expectedSnapshot`, builds the proposed persistence plan entirely in memory and returns no receipt if planning or containment validation fails. It performs no source mutation, durable recovery write or external network activity.

## Canonical change-set identity

The change-set digest is SHA-256 over a versioned binary canonical form. It is not calculated from serialized MCP JSON. The canonical writer emits a fixed format marker and version, then length-prefixes every scalar and collection element so separators, nulls, empty values and concatenated values cannot be confused.

File entries are ordered by canonical Workspace-relative path using ordinal comparison after directory separators have been normalised to `/`. A case-only rename remains governed by the existing platform-aware commit-planning rejection. Source text is represented by the hash of the exact serialized bytes that the commit planner intends to persist, preserving encoding, byte-order mark and line-ending distinctions.

Each canonical file entry binds:

- canonical Workspace-relative target path;
- create, replace or delete operation;
- expected original existence and original-byte hash;
- intended serialized-byte hash, or an explicit absence marker for deletion;
- intended Unix file mode when the platform and operation make it applicable;
- stable project ownership represented by canonical Workspace-relative project paths and target frameworks rather than process-local Roslyn IDs; and
- definitive source classification used by the current mutation and containment policy.

The digest excludes receipt ID, expiry, process-local Workspace and transaction IDs, absolute paths, commit IDs, recovery-artifact paths, line-summary prose and optional detailed diffs. Identical validated persistence sets therefore produce the same digest independently of enumeration order and receipt creation time.

The receipt binding combines the digest with Workspace ID, Workspace epoch, transaction ID, snapshot ID and transaction revision. These values need not alter the deterministic change-set digest, but every one must match at commit. Moving through transaction history therefore invalidates a receipt even when an earlier revision happens to produce the same file content.

The canonicalisation algorithm identifier is part of the public receipt. Any future incompatible canonicalisation change introduces a new algorithm version and explicit compatibility handling rather than silently changing the meaning of an existing digest.

## Receipt store and lifetime

The Host holds at most one current receipt per loaded Workspace. The initial lifetime is 15 minutes and is measured through `TimeProvider` so expiry behaviour is deterministic under test. The result exposes the corresponding UTC expiry for client guidance.

Reviewing the same immutable Workspace epoch, transaction, snapshot and revision may reuse the existing receipt and projection. A different transaction, revision or snapshot replaces or invalidates the stored receipt. Workspace rollback, reload or closure also makes the receipt unusable.

A declined, cancelled, unavailable, failed or invalid elicitation does not consume the receipt, allowing a deliberate retry while the transaction remains unchanged. Accepted approval atomically consumes the receipt before persistence is attempted. Consumption remains final if subsequent revalidation or persistence preparation fails, so another attempt requires a new review. A consumed receipt cannot be replayed for the same or another Workspace.

The store is process-local and is never written into durable recovery state. Process restart invalidates every outstanding receipt.

## Approval interaction

The receipt-aware commit handler resolves and validates the supplied receipt before requesting elicitation. An already missing, expired, consumed or state-mismatched receipt fails without prompting and directs the client to `transaction-review`.

The approval request identifies the canonical digest, transaction revision, changed-document counts and receipt expiry. It offers exactly two choices: `approve-this-receipt` and `do-not-commit`. Approval for the rest of the session is deliberately unavailable because it cannot preserve exact-change binding.

The existing protocol-neutral `IUserInteractionService` owns MCP capability discovery, request translation and outcome normalisation. Receipt selection, lifetime, consumption and authorisation remain Host workflow responsibilities and do not enter the MCP adapter. Unavailable, declined, cancelled, failed or malformed responses fail closed without persistence and state clearly that the transaction remains active. Where appropriate, guidance explains that client policy may have blocked MCP elicitation and that interactive MCP requests must be enabled before a deliberate retry.

## Final Workspace enforcement

After accepted elicitation, the Host consumes the receipt and passes a protocol-neutral receipt authorisation to Workspace. Workspace configuration contains only the neutral requirement that receipt authorisation is mandatory; Workspace does not depend on Host operational-mode enums or MCP protocol types.

Under the exclusive Workspace transaction lease, the commit path:

1. verifies the Workspace, epoch, transaction, snapshot and revision binding;
2. rebuilds the validated persistence plan from the current baseline and staged solution;
3. recalculates the versioned canonical change-set digest;
4. compares the rebuilt identity with the consumed authorisation;
5. performs the existing external-change, containment, filesystem, commit-lock and recovery checks; and
6. enters persistence only when every comparison and validation succeeds.

Any mismatch fails without writing source files. The active transaction remains available for review, rollback or a deliberate retry with a newly generated receipt. No failure path downgrades to simple confirmation or autonomous operation.

The canonical identity is derived from the same commit-planning representation used for persistence so review and commit cannot disagree about serialized content, target paths or file operations. Transient commit and recovery identifiers are excluded from the digest, allowing the final plan to use its own durable commit identifier without changing the approved persistence identity.

## Cost and data boundaries

Receipt projection, line-summary calculation, canonical hashing, receipt storage and final digest comparison execute only when the effective commit-authorisation policy is receipt approval. Confirmation and autonomous operation retain `transaction-preview` and perform none of this work. Inspection-only cannot reach either path.

Receipt results remain bounded and local. They contain source paths and project ownership already available to the connected authorised client, but no source text unless the caller explicitly requests one bounded document diff. Receipt generation performs no network access.

## Documentation changes

Implementation updates the docs-site configuration reference, getting-started examples, agent guide, tool-discovery reference and transaction workflow documentation. It also updates generated tool descriptions, examples and canonical `tools/list` baselines for the effective mode-specific catalogues.

The documentation presents preview-based confirmation, receipt-based exact-change approval, autonomous commit and inspection-only operation as distinct workflows. It explicitly instructs clients to renegotiate instructions and tool discovery after changing process configuration rather than reusing assumptions learned from another mode.

## Automated evidence

New implementation files require full line and branch coverage unless an explicit repository exception is approved. Unit and contract coverage includes canonical digest golden vectors, ordering independence, length-prefix ambiguity resistance, exact serialized-byte distinctions, project ownership and file-mode binding, receipt reuse and replacement, expiry, atomic consumption, replay rejection and every normalised interaction outcome.

Workspace and Host integration coverage proves content, path, ownership, classification, Workspace, epoch, transaction, snapshot and revision mismatches; no elicitation for an invalid receipt; external change after approval; final digest recalculation before persistence; and transaction retention after failed approval or validation.

Composition and contract coverage proves the mode matrix, mode-specific server instructions, status projection, reserved names, tool counts, both output-schema modes and the physical unreachability of the review operation excluded by policy. Instrumented tests prove that receipt planning, projection, hashing and storage are not invoked by inspection-only, transactional or autonomous-trusted operation.

Published-Host acceptance covers the complete approval-required happy path, refusal, missing elicitation capability, stale receipt, replay rejection, invocation of unavailable `transaction-preview`, and the distinct workflow advertised after changing modes. Because acceptance sources and checked-in catalogue assets change, the complete platform acceptance wrapper is mandatory. Validation also includes affected unit and integration suites, latest-all analyzer builds, the complete solution build and representative mutation scenarios.

## Completion criteria

The work item is complete only when:

- approval-required is selectable and cannot start in a partially composed state;
- its catalogue publishes `transaction-review` and the receipt-aware commit contract but not `transaction-preview`;
- every approved commit is bound to one current canonical receipt and revalidated immediately before persistence;
- receipts are bounded, expiring, process-local and non-replayable after accepted approval;
- other operational modes incur no receipt cost or receipt contract exposure;
- failures preserve the transaction and provide actionable mode-correct continuations;
- docs and generated references describe the implemented workflow; and
- the required unit, integration, acceptance, analyzer, build and scenario evidence is green.
