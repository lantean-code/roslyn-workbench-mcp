# Enterprise pilot and 1.0 roadmap

## Status and purpose

This document records the current development direction arising from the enterprise design and security review completed on 11 September 2026 and the subsequent assessment against the repository's implementation and supported operating model. It is a prioritised planning aid, not a release commitment, delivery schedule or claim that every proposed capability is required for every deployment.

The roadmap focuses on making the product's existing safety properties explicit, enforceable, testable, observable and stable. It does not expand the trust boundary: Roslyn Workbench continues to run as the current operating-system user, and Workspace loading continues to require fully trusted MSBuild inputs, analysers, Code Action providers and configured plugins.

## Decision principles

- Evaluate each proposed control against the actors, plausible actions, existing controls and user-visible impact defined in [`../maintainers/operating-model.md`](../maintainers/operating-model.md).
- Treat host policy as a defence against accidental or agent-initiated use through Roslyn Workbench, not as an operating-system sandbox or a control over unrelated shell, editor or filesystem tools.
- Prefer a small number of composable policy primitives over hidden profile-specific behaviour. Named profiles may expand to those primitives for operator convenience.
- Preserve the distinction between deciding whether a change should be made and using Roslyn to perform the intended semantic change safely.
- Extend the existing transaction pipeline rather than creating direct-write mutation paths or a bespoke MCP tool for every Roslyn refactoring.
- Require measured evidence before adding stateful scaling mechanisms such as generic continuation tokens or automatic provider suppression.

## Existing baseline

The following controls already form the baseline and should be preserved:

- immutable Workspace snapshots and explicit snapshot preconditions;
- transaction-only source mutation with preview, history, rollback, commit revalidation and durable recovery;
- physical containment checks for mutable source files;
- read-only treatment of evaluated documents outside the Workspace root;
- rejection of non-source and intermediate `obj` document mutation;
- bounded semantic results and bounded query concurrency;
- short-lived, snapshot-bound Code Action references with replay validation;
- advisory cross-instance status, final commit locking and external-change detection;
- fixed startup plugin discovery, with external plugin loading disabled unless the operator explicitly enables it and configures plugin directories;
- allow-listed error-report projection, explicit preparation and submission, payload digests and configurable consent;
- low-overhead performance events for operation phases, caches, retries and input monitoring; and
- release checksums, pinned workflow actions, build identity, installed-tool acceptance coverage, OIDC NuGet publication and manual GitHub Release publication.

## Priority 0: define the supported policy and compatibility boundaries

### 1. Define deployment modes and policy boundaries

Define the target deployment modes and provisional security requirements before implementing new policy controls. The initial modes should include inspection-only use, transactional use, approval-required commit and autonomous trusted use. Each mode must state which responsibilities belong to Roslyn Workbench, the MCP client and the surrounding operating environment.

This initial work defines the intended boundaries and policy primitives; it must not present unimplemented behaviour as a current guarantee. The definitive security invariants and their automated evidence mapping are published only after the relevant controls have been implemented and reviewed.

The detailed design is recorded in [`deployment-modes-and-policy-boundaries.md`](deployment-modes-and-policy-boundaries.md).

Acceptance bar:

- every deployment mode identifies its actors, intended authority, client dependencies and residual risks;
- the required host-owned policy primitives are explicit rather than hidden inside named profiles;
- protocol-specific user interaction is assigned to a reusable Host adapter rather than transaction, error-report or Workspace domain logic;
- target requirements cover Workspace authority, query versus mutation effects, commit validation and approval, recovery, status observability and startup-policy immutability, while new telemetry is explicitly deferred to the operational-health design; and
- provisional requirements are clearly distinguished from implemented and tested guarantees.

### 2. Add host-owned Workspace authority

Add startup policy that constrains which solution and project paths the Host may open. A caller may select or narrow authority within configured roots but cannot widen it.

Model read and mutation authority separately. Evaluated linked, generated, package-provided, additional and analyser-config documents may exist outside the selected repository root; policy must state whether those documents are queryable, redacted from agent-facing results or rejected during Workspace admission. Mutation must remain physically contained and must not become broader than read authority.

Acceptance bar:

- canonical and physical path checks prevent traversal, symbolic-link and reparse-point escapes;
- the loaded path, caller-requested Workspace root and every retained project comply with host policy;
- external evaluated documents follow an explicit read policy and remain non-mutable unless a future policy deliberately grants narrower supported authority;
- effective non-sensitive authority policy is observable through status; and
- documentation makes clear that MSBuild may still load trusted build logic from SDK, package, user and parent-directory locations outside the source root.

### 3. Establish the 1.x compatibility policy

Define independent compatibility guarantees for MCP tools, structured errors and continuations, durable recovery state and the public plugin API. Document supported upgrade and downgrade behaviour before declaring `1.0.0`.

Acceptance bar:

- canonicalised built-in `tools/list` contracts are compatibility-tested without relying on incidental JSON property ordering;
- existing tool names, required inputs, enum semantics and output field types follow a documented additive-change policy during 1.x;
- supported later 1.x versions can recover supported earlier 1.x durable state through explicit cross-version fixtures;
- unknown or unsupported recovery versions fail closed with actionable status and do not reinterpret state; and
- plugin compatibility and package versioning rules are documented independently from the Host's MCP contract.

Implementation evidence is maintained in the docs-site [compatibility policy](../content/compatibility.md), canonical published-Host catalogue baselines for both output-schema modes, explicit V1 recovery fixtures and unsupported-version preservation coverage, stable error/continuation contract tests, and shipped public-API analyser baselines for Plugins and Abstractions.

## Priority 1: bind review, validation and persistence

### 4. Establish operational modes and transactional confirmation

Implement the startup-owned policy resolution and conditional catalogue composition needed by the target operational modes before adding receipt-specific review work. Named operational-mode profiles should resolve to the composable source-mutation, commit-authorisation and commit-validation primitives defined by the deployment-mode design; they must not create independent execution paths. The safe default is inspection-only; retaining the current mutation behaviour requires explicit autonomous-trusted selection.

Policy-dependent operations and schemas must be published only when they are usable under the complete effective policy. In particular, receipt review must not add a tool, output field, schema member, description or example to the agent's catalogue outside receipt-approval operation. A partially implemented approval-required profile must not be selectable or published: unsupported or incomplete combinations fail during startup rather than silently degrading to a less restrictive policy.

This item also introduces the protocol-neutral `IUserInteractionService`, its MCP SDK-backed elicitation adapter and client-capability discovery, then migrates error-report consent to that service without changing established behaviour. Transactional mode becomes fully usable by requesting commit confirmation with three deliberate outcomes: approve this commit, approve transaction commits for the rest of the current server session, or decline. Session approval is memory-only, commit-specific in scope, unavailable to receipt approval and error reporting, and cleared by process termination; every commit still performs snapshot, conflict, filesystem and recovery validation.

Inspection-only, transactional and autonomous-trusted become supported modes in this item. Approval-required remains a reserved but startup-rejected value until the canonical receipt and exact-change approval are delivered together by the next item.

Acceptance bar:

- policy is resolved and validated before the fixed tool catalogue is composed and cannot be widened by an MCP request;
- the default resolves to inspection-only, while autonomous trusted behaviour requires explicit selection;
- inspection-only publication omits transaction and mutation operations, while lower-level policy enforcement prevents invocation bypass;
- external plugin loading defaults to disabled, requires an explicit startup switch and publishes only plugin queries under inspection-only policy;
- lightweight transaction preview remains the only review contract for confirmation and autonomous operation;
- transactional confirmation supports approve once, approve transaction commits for this server session and decline, with session approval remaining memory-only;
- missing elicitation capability and unavailable, declined, cancelled, invalid or failed interaction outcomes fail closed without persistence and leave the transaction available;
- the migrated error-report workflow preserves its existing consent, submission-handle and failure behaviour;
- receipt review contracts are absent from `tools/list`, generated descriptions and examples unless receipt approval is both selected and fully supported; and
- `server-status` reports the effective non-sensitive policy primitives and operational profile.

### 5. Produce a canonical transaction review receipt and activate exact-change approval

Add one cohesive receipt-review operation for receipt-approval policy so one deterministic result describes the exact staged change, then activate approval-required mode with one-use approval bound to that receipt. Keep the existing lightweight `transaction-preview` contract for confirmation and autonomous operation. The receipt operation, its result contract and its supporting agent guidance must be conditionally composed only for receipt-approval operation and must not appear in any other mode's tool-list context.

The detailed design is recorded in [`transaction-review-receipts-and-exact-change-approval.md`](transaction-review-receipts-and-exact-change-approval.md).

The receipt should bind the Workspace identity, snapshot identity, transaction revision and a canonical change-set digest. It should include changed-document counts and line summaries, affected projects, mutation provenance, generated/intermediate classification, containment status and validation results. Detailed source diffs should remain explicitly requested and bounded.

Approval-required commit consumes the protocol-neutral interaction service introduced by the preceding item but never inherits transactional session approval. The control proves that Roslyn Workbench requested approval for a particular receipt; it does not claim that a user carefully understood the diff or that unrelated tools cannot modify files with the same operating-system authority.

Acceptance bar:

- identical staged content produces the same canonical digest under a documented algorithm;
- any content, path, project ownership or transaction revision change invalidates the receipt;
- the review projection is sufficient for a client or user to identify the exact proposed persistence set;
- receipt generation performs no source mutation or external network activity;
- no receipt projection or digest work occurs outside receipt-approval operation;
- repeated review of the same immutable Workspace epoch, snapshot and revision may reuse a bounded cached receipt without weakening final commit revalidation;
- receipt approval is one-use, short-lived and cannot be replayed for another Workspace, revision or content set;
- mutation or external-change detection after approval invalidates approval, while final filesystem and transaction revalidation still occurs after approval;
- missing capability and unavailable, declined, cancelled, invalid or failed interaction outcomes fail closed without persistence and return actionable client guidance; and
- failed approval leaves the active transaction available for inspection and deliberate retry.

### 6. Validate staged compiler impact

Add deterministic validation comparing baseline and staged compiler diagnostics for the same loaded project and target-framework evaluation. The first required rule is to identify newly introduced compiler errors rather than requiring an already imperfect Workspace to become error-free. The approved implementation is recorded in [Compiler-impact validation](compiler-impact-validation.md).

Compiler-and-analyser validation may be offered separately because third-party analysers can be expensive and execute inside the trusted Host boundary. Test-impact and public-API summaries may later contribute review information but are not initial commit gates.

Acceptance bar:

- diagnostic comparison uses a documented stable identity rather than error counts alone;
- incomplete compilations, skipped projects, load diagnostics and multi-targeting limitations are reported without false assurance;
- validation is snapshot- and revision-bound and is invalidated by staged or external changes; and
- independent Host policy can require successful no-new-compiler-error validation before commit without adding compiler work to deployments that leave the policy disabled.

## Priority 1: complete mutation policy and provenance

### 7. Expose Code Action provenance

Expose deterministic provenance that helps operators understand which component proposed a transformation. Include provider type and assembly identity, assembly version, action kind, diagnostic identifiers and equivalence key where appropriate. Expose package identity only when it can be established reliably; do not infer a NuGet package from an assembly path or name.

The approved implementation is recorded in [Code Action provenance](code-action-provenance.md). It keeps routine agent responses compact, publishes discovery provenance only when requested, deduplicates provider identities behind response-local dictionary keys, carries concise attribution into transaction review, and reserves detailed equivalence and Fix All data for structured local logging.

Provider allow-listing may follow once the exposed identity has proved stable across supported Roslyn and analyser versions.

### 8. Refine generated-source policy

Retain the existing rejection of `obj`, external and non-source mutations. Define how checked-in files that appear generated are classified, including generated filename conventions, auto-generated headers and repository-specific exceptions. Because these signals are heuristic and some generated source is intentionally checked in, the default may combine denial for deterministic intermediate/source-generated documents with warning or configurable policy for generated-looking source files.

The approved implementation is recorded in [Generated-source policy](generated-source-policy.md). It retains deterministic hard denials, centralises bounded generated-looking classification, defaults checked-in generated-looking source to a structured warning, provides an operator-selected deny policy and Workspace-relative exceptions, and reuses transaction confirmation or receipt approval without adding elicitation.

## Priority 1: ratify guarantees and distribution assurance

### 9. Publish final security invariants and evidence mapping

After the behavioural controls above have been implemented, reviewed and validated, publish the guarantees that the Host actually maintains, their limitations and the automated coverage that demonstrates them. At minimum, cover Workspace authority, query versus mutation effects, snapshot freshness, the sole persistence boundary, exact-change validation and approval, recovery behaviour, error-report network effects, startup-policy immutability and fail-closed handling of unsupported state.

Acceptance bar:

- every invariant identifies its actor, protected outcome, boundary and residual risk;
- every enforceable invariant maps to unit, contract, integration or acceptance evidence at the appropriate layer;
- the document describes current released behaviour rather than roadmap intent; and
- unsupported scenarios and controls delegated to the MCP client or operating environment remain explicit.

### 10. Complete supply-chain evidence

Build on the existing release checksums, pinned automation and OIDC publication by producing an SPDX or CycloneDX SBOM and build provenance for the exact released packages. Document dependency inventory, vulnerability review, build identity and the relationship between commit, CI run, package hash and SBOM.

Resolve signing and trust expectations separately for NuGet packages, tags, MSI, MSIX, Debian and RPM artefacts. Do not present unsigned installer formats as equivalent to authenticated package publication.

## Priority 2: make operations observable

### 11. Aggregate local operational health

Build operator-facing health summaries from the existing performance events before adding automatic enforcement. Candidate information includes Workspace load duration, query and cache metrics, memory pressure, cancellation rates, recovery state and analyser or Code Action provider latency.

Health reporting must be bounded and local, avoid source content and identifiers where possible, and distinguish slow providers from incomplete or failed semantic results. Automatic provider disabling is out of scope until correctness and policy semantics are defined.

## Deferred pending evidence

### Cross-instance mutation lease

The current operating model supports cooperating instances through advisory status, agent coordination, external-change detection and final commit locking. Add a durable single-writer lease only if the pilot demonstrates regular competing transactions or if deterministic multi-agent mutation becomes a supported scenario. Any design must address stale owners, process death, lease recovery, read availability and interaction with durable commit recovery.

### Snapshot-bound continuation tokens

Retain deterministic recomputation for bounded result sets until large-solution profiling shows material latency or resource cost. If introduced, tokens must be opaque, expiring and bound to Workspace, snapshot, complete query identity, ordering and next position.

### Public API compatibility enforcement

Use the existing API-surface and change-impact capabilities as review inputs. Consider a transaction-level binary/source compatibility gate only after compiler validation is established and concrete library-maintainer demand demonstrates the required compatibility model.

### Expanded policy profiles

Named profiles are operator conveniences over explicit policy settings, not independent execution paths. Add further profiles only after the underlying authority, validation and approval primitives are stable.

## Pilot operating guidance

Until the roadmap controls are implemented, an enterprise pilot should apply compensating deployment policy:

- use only fully trusted repositories already approved for local MSBuild development;
- pin the exact Roslyn Workbench package version and verify it through an approved package source and recorded checksum;
- leave external plugin loading disabled unless each configured package has been reviewed and deliberately approved;
- select `never` or `prompt` error-report consent according to organisational policy;
- use an enterprise-approved MCP client and model data-handling configuration;
- restrict generic shell and filesystem mutation tools through client or endpoint policy when Roslyn Workbench is intended to provide the safer semantic path;
- avoid multiple Roslyn Workbench instances mutating the same repository and treat `WorkspaceInUse` as query-only;
- require transaction preview before commit and normal source review, build and test gates after persistence; and
- do not use the supported pilot model with repositories or build inputs that are not fully trusted; less-trusted execution requires a separately threat-modelled sandbox deployment and support model rather than OS isolation alone.

## 1.0 readiness summary

The minimum proposed readiness bar is:

- supported deployment modes and security invariants are documented and tested;
- Workspace open, read and mutation authority is host-controlled;
- staged changes have a canonical review identity and compiler-impact validation;
- an operator can require fail-closed approval of that exact identity;
- MCP, recovery and plugin compatibility guarantees are documented and tested;
- Code Action provenance is inspectable;
- generated and intermediate source mutation policy is explicit;
- released packages include checksums, SBOM and provenance appropriate to their distribution channel; and
- supported runtime and platform combinations continue to receive installed-artifact acceptance coverage.

Cross-instance single-writer leases, generic continuation tokens, broad policy-profile catalogues and public-API compatibility gates are not part of the minimum bar without further evidence.
