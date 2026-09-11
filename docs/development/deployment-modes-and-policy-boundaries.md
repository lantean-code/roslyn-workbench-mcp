# Deployment modes and policy boundaries

## Status

This document defines target operational modes and provisional policy requirements for Roslyn Workbench. It guides later implementation work but does not claim that every described control exists in the current release. A requirement becomes a supported security invariant only after its implementation, validation and evidence mapping have been completed and published.

An operational mode is a startup-selected policy profile that drives the Host's security and authority model. It determines the capabilities the Host publishes and the operations it authorises. The selected mode is resolved before the tool catalogue is published, remains fixed for the process lifetime and cannot be changed or widened by an MCP request.

All modes use the same released executable; they are not separate product builds. An enterprise may launch that executable through a wrapper or managed client configuration that fixes approved startup arguments. The modes describe operator intent and responsibility boundaries without requiring independent implementations: each mode should resolve to ordinary composable policy settings once those primitives are implemented and stable, rather than creating separate execution paths behind a `DeploymentMode` runtime enum.

## Existing trust boundary

Roslyn Workbench is a local stdio process running with the operating-system authority of the user who starts it. The connected agent is an MCP client acting on the user's behalf. It may be stale, imprecise, over-eager or wrong, so Host controls are designed to make plausible mistakes and misfires fail safely. The Host protects supported contracts, transaction semantics, source containment and recovery, but it does not isolate the user and agent from each other or restrict unrelated editors, shells, Git clients or filesystem tools.

A Workspace is executable input. Opening a solution or project evaluates MSBuild logic, and later diagnostic or Code Action operations can execute trusted analysers and providers. Inspection-only source policy does not make an untrusted Workspace safe to open. Less-trusted execution requires an operating-system sandbox and a separate support and security model.

Third-party plugins execute as trusted in-process code. A plugin can use ordinary .NET APIs to access files, processes or networks outside the Host's supported tool pipeline. Host mutation policy can prevent compliant plugin mutation handlers from acquiring or staging through that pipeline, but it cannot govern direct filesystem operations performed by plugin implementation code. Strong inspection-only deployment therefore requires that no third-party plugin directories are configured.

See the [`operating model`](../maintainers/operating-model.md) for the supported actors, concurrency assumptions and scenario-assessment rules.

## Current behaviour

The current release most closely resembles the autonomous trusted mode:

- built-in query, mutation and transaction tools are published;
- mutations stage immutable Roslyn `Solution` changes and only `transaction-commit` persists supported source changes;
- the caller may request an absolute Workspace path and may supply a containing Workspace root;
- the Host has no configured allow-list of Workspace roots;
- evaluated documents outside the Workspace root can be queried but remain read-only;
- commit revalidates the transaction snapshot and filesystem inputs but does not compile the staged solution;
- the Host does not require proof of human approval before commit;
- third-party plugins are absent unless plugin directories are explicitly configured; and
- error-report availability and consent are configured independently from source mutation.

This baseline remains subject to the trust, transaction and cross-instance constraints documented in [`workspaces and safe transactions`](../content/workspaces-and-transactions.md).

## Target operational modes

### Inspection-only

Inspection-only operation allows trusted Workspace loading and semantic queries while disabling every supported path that can stage or persist source changes.

Target Host behaviour:

- Workspace lifecycle and semantic query tools remain available;
- `list-code-actions` and `prepare-fix-all` may remain available because they inspect or prepare bounded semantic results without staging source;
- transaction start, preview, history, rollback and commit tools are not published because no transaction can exist in this mode;
- bundled and third-party mutation tools, including `stage-code-action`, are not published;
- authoritative policy rejects transaction acquisition or mutation staging even if a future registration error exposes an invocation path; and
- omitted tool names remain reserved against plugin collisions.

Inspection-only refers specifically to supported source mutation. Workspace loading still executes trusted build logic, local diagnostic inspection can disclose Workspace context to the connected agent, and error-report submission remains a separately configured external effect.

Strong inspection-only deployment uses no third-party plugins. If an operator deliberately configures a trusted plugin, its in-process behaviour falls within the trusted-extension boundary rather than the source-mutation guarantee.

### Transactional

Transactional operation provides the staged mutation pipeline with a simple Host-requested confirmation before each commit. The Host enforces snapshots, transaction revisions, candidate policy, containment, preview, revalidation and durable recovery. The confirmation is a client-mediated checkpoint intended to catch agent mistakes or misfires, but it is not a Host-issued exact-change approval receipt. Whether the client presents that checkpoint to a human depends on client and operator policy.

Target Host behaviour:

- query, mutation and transaction tools are published;
- commit requests a simple yes-or-no confirmation through MCP elicitation;
- commit is permitted only after the client accepts that elicitation and existing transaction and filesystem validation succeeds;
- unavailable, declined, cancelled or failed elicitation fails closed without persistence and leaves the transaction available for inspection or a later deliberate retry;
- the failure result tells the agent that client policy may have blocked interactive MCP requests and that the user may need to enable them before retrying; and
- compiler validation may be configured independently once available.

This mode is suitable when an operator wants transactional integrity and a client-mediated confirmation, but does not require approval to be bound to a canonical change-set digest.

### Approval-required

Approval-required operation extends the transactional pipeline by requiring one-use Host approval bound to the canonical transaction review receipt. The receipt and approval mechanisms are later roadmap work; defining this mode does not claim they currently exist.

Target Host behaviour:

- query, mutation and transaction tools are published;
- commit requires a current receipt bound to the Workspace, snapshot, transaction revision and canonical change-set digest;
- the Host requests approval through its protocol interaction adapter;
- unavailable client interaction support, rejected approval, expired approval or receipt mismatch fails closed without persistence;
- mutation or external change after approval invalidates that approval; and
- final transaction and filesystem revalidation still occurs after approval.

This control demonstrates that the Host requested approval for a particular change identity. It cannot demonstrate that the user understood the change, and it does not prevent another process with the same operating-system authority from modifying files.

### Autonomous trusted

Autonomous trusted operation uses the transactional pipeline without requiring human interaction for each commit. It is intended only for an authorised agent, fully trusted Workspaces and an operator who deliberately accepts autonomous decisions within configured Host authority.

Target Host behaviour:

- query, mutation and transaction tools are published;
- Host-issued commit approval is not required;
- snapshot, candidate, containment, durable commit and recovery controls remain mandatory; and
- compiler validation may be required independently and should be the recommended configuration once implemented.

Autonomous trusted operation differs from transactional operation because it deliberately omits Host elicitation. Both use the same mutation and commit implementation; the effective commit-authorisation policy determines whether a confirmation is required.

## Client capability discovery and elicitation failure

MCP elicitation support is a negotiated client capability, independent from the client's filesystem sandbox, tool permission policy or other measure of agent autonomy. A permissive execution mode must not be assumed to support interactive MCP requests.

The Host records the connected client's advertised elicitation capability during MCP initialisation. Transactional and approval-required commit use that capability at invocation time. If the client does not advertise elicitation, the Host returns an actionable approval-unavailable result without attempting persistence. A client may advertise the capability but still decline, cancel or fail an interaction because of its current policy, so runtime responses must also be handled explicitly and fail closed.

The existing error-report consent workflow is the behavioural precedent. It distinguishes unavailable approval from a request that was not approved, states that client policy may have blocked the prompt and confirms that the external effect did not occur. Transaction commit should apply the same pattern with transaction-specific guidance:

- an unavailable or failed interaction reports that commit approval could not be obtained;
- a declined, cancelled or otherwise unapproved interaction reports that the commit was not approved;
- every result states unambiguously that no files were persisted and that the transaction remains active;
- guidance tells the agent to inform the user that interactive MCP requests may need to be enabled before a deliberate retry;
- the Host does not automatically retry the elicitation or downgrade to autonomous operation; and
- final error-code and structured-result names are defined with the implementation contract rather than fixed by this provisional design.

The Host cannot distinguish a client-policy decline from a deliberate user decline unless the protocol and client provide that distinction. It must not claim that a particular human approved merely because the client returned acceptance. Receipt-bound approval proves which change identity was accepted, not the identity or comprehension of the person interacting with the client.

### Protocol-isolated user interaction

Before transaction commit depends on elicitation, the Host must introduce a protocol-neutral `IUserInteractionService` boundary with an MCP SDK-backed implementation. Transaction and error-report workflows depend only on neutral interaction requests and a closed outcome model such as accepted, declined, cancelled, unavailable, failed and invalid response. MCP SDK capability, request-schema, response, exception and server types remain inside the Host adapter and do not enter Workspace or domain workflow contracts.

The service owns client-capability discovery, protocol request translation, outcome normalisation and expected SDK failure translation. It does not decide whether a transaction may commit, bind approval to a receipt, dispatch an error report, discard domain state or construct domain-specific error guidance; those responsibilities remain with the calling workflow.

The existing error-report consent path must be migrated to this service without changing its established consent, handle-lifetime or failure behaviour before transaction commit becomes a second consumer. Tests must cover relevant advertised-capability and runtime-response combinations through the adapter so a future MCP SDK or protocol change can be absorbed at that boundary.

## Composable policy primitives

Later implementation should express deployment policy through independent startup-owned primitives rather than a single mode switch with hidden behaviour.

| Primitive | Target alternatives | Responsibility |
| --- | --- | --- |
| Source mutation | Disabled; Enabled | Controls publication and authoritative acquisition of supported transaction and mutation capabilities. |
| Commit authorisation | None; confirmation; receipt approval | Determines whether commit proceeds autonomously, requires simple MCP elicitation or requires elicitation bound to an exact-change receipt. |
| Commit validation | None; No new compiler errors | Determines whether a snapshot-bound compiler comparison is a commit prerequisite. |
| Workspace admission | Unrestricted trusted paths; configured allowed roots | Constrains which solution and project paths the Host may open. |
| External document read | Allowed; denied; future narrower policy | Controls agent-facing disclosure of evaluated documents outside admitted source authority. |
| Plugin loading | No configured directories; configured trusted directories | Selects trusted in-process extensions at startup. |
| Error-report consent | Never; prompt; always | Independently controls preparation and explicit submission of allow-listed external diagnostics. |

The table defines semantic alternatives, not final command-line option names. Each implementation work item must design its public configuration contract, defaults, compatibility behaviour and status projection before code changes begin.

Named operational-mode profiles may later provide recommended combinations:

| Operational mode | Source mutation | Commit authorisation | Recommended validation | Third-party plugins |
| --- | --- | --- | --- | --- |
| Inspection-only | Disabled | Not applicable | Not applicable | None |
| Transactional | Enabled | Confirmation | Operator selected | None by default |
| Approval-required | Enabled | Receipt approval | No new compiler errors | None by default |
| Autonomous trusted | Enabled | None | No new compiler errors | Explicitly configured trusted plugins only |

These are presets, not restrictions on valid explicit combinations. For example, an operator may require compiler validation while retaining simple commit confirmation.

## Policy ownership and immutability

Security-sensitive policy is resolved and validated during Host startup. MCP requests may select or narrow authority within that policy but cannot widen, disable or replace it. Invalid security-sensitive values must fail closed when falling back would grant more authority than the supplied value.

The Host owns protocol publication, user-interaction adaptation and policy reporting. Workspace owns neutral enforcement needed to ensure transaction acquisition, staging and commit cannot bypass effective source-mutation and validation policy. Protocol-specific commit authorisation remains in the Host and reaches Workspace services only through a validated, protocol-neutral confirmation or receipt.

Effective non-sensitive capability state must be visible through `server-status`. At minimum, status should disclose the operational mode; whether source mutation, Host confirmation, receipt approval, compiler validation, Workspace admission constraints and external-document restrictions are active; and whether the client advertised the elicitation prerequisite. Acceptance, cancellation, failure and receipt validity belong to the transaction-specific result because status cannot predict the outcome of a future interaction. The later Workspace-authority design will decide whether exact configured paths are returned, summarised or deliberately withheld.

## Publication and enforcement requirements

Tool publication improves least-privilege discovery but is not the sole enforcement mechanism.

For disabled source mutation:

- omit Host transaction tools;
- omit bundled Code Action mutation tools;
- omit bundled ordinary mutation tools;
- omit mutation tools contributed by configured plugins;
- retain the reserved-name set independently from the published set; and
- enforce policy before acquiring a transaction or mutation staging capability.

Query handlers must continue to lack mutation staging capability. A query result may create bounded process-local cache or replay state without becoming a source mutation. Error-report preparation and submission retain their existing independent publication and consent rules.

Contract and acceptance coverage must exercise every supported publication combination. A tool omitted by policy must be absent from `tools/list`, unavailable through invocation, and unable to reach a lower-level mutation path. Status tool counts and generated documentation must derive from the effective published catalogue rather than fixed assumptions.

## Responsibility matrix

| Concern | Roslyn Workbench Host | MCP client or operator | Operating environment |
| --- | --- | --- | --- |
| Select supported source capabilities | Enforces startup policy and published catalogue | Configures the intended operational mode | May restrict process configuration centrally |
| Decide whether a proposed change is desirable | Provides semantic evidence and enforces configured confirmation or exact-change approval | Reviews, approves or delegates the decision | Supplies organisational review requirements |
| Apply an intended semantic change safely | Enforces transaction, snapshot, containment, commit and recovery controls | Uses the supported workflow and follows continuations | Avoids unsupported concurrent structural writes |
| Restrict unrelated file or shell tools | No authority outside its process | Applies client tool permissions | Applies endpoint, filesystem, user or sandbox controls |
| Establish Workspace trust | Warns and documents executable-input boundaries | Opens only approved Workspaces | Controls repository, package, SDK and build-input provenance |
| Control model disclosure | Bounds and projects tool results | Uses approved model and data-handling settings | Applies network and data-loss-prevention policy |
| Control third-party plugin effects | Loads only configured packages and enforces supported contracts | Configures only trusted plugins | Sandboxes the entire process when extensions are less trusted |
| Control external error reports | Enforces configured consent and allow-listed submission | Reviews or grants the configured approval | May block network destinations independently |

## Provisional requirements for later work

The following requirements guide subsequent roadmap designs:

- Workspace open, external-document read and source mutation authority must be independently expressible where their boundaries differ.
- Caller-supplied Workspace roots may narrow configured Host authority but must never widen it.
- Transaction review, compiler validation and approval must share an exact snapshot- and revision-bound change identity.
- Transactional and approval-required commit must fail closed with actionable client guidance when required elicitation is unavailable, declined, cancelled or fails.
- Autonomous operation must retain all deterministic transaction, revalidation and recovery controls.
- Stable 1.x contracts must define how policy-dependent tool publication and new optional capability fields evolve.
- Generated and intermediate source classification must be explicit before final source-authority invariants are published.
- Final security invariants must describe implemented, tested behaviour rather than copying these provisional requirements.

## Non-goals

This policy direction does not:

- sandbox MSBuild, analysers, Code Action providers or plugins;
- establish whether a repository or dependency is trustworthy;
- prevent the user, agent client or another tool from writing files outside Roslyn Workbench;
- make path allow-lists a substitute for operating-system access control;
- guarantee that a human understands an approved change;
- require a bespoke implementation for each named operational mode;
- define final command-line option names or compatibility defaults before their implementation designs; or
- convert aspirational roadmap behaviour into a current security claim.

This design requires status observability but does not define new audit or operational telemetry. Policy-decision and interaction telemetry, including its privacy and identifier boundaries, is deferred to the later operational-health design after the underlying controls exist.

## Completion and assurance

This design item is complete when the operational modes, composable primitives, ownership boundaries, provisional requirements and non-goals have been reviewed and accepted. It intentionally has no runtime acceptance claim.

After the relevant controls are implemented, the later security-invariants work must reconcile this target model with actual behaviour, remove or revise requirements that proved unsound, and map every retained enforceable guarantee to current automated evidence.
