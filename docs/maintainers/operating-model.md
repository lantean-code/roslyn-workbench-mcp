# Product operating model

This document defines the real operating scenarios that implementation and review work must use when deciding whether behaviour is a product defect, a necessary hardening measure or an accepted residual risk. It complements the release-facing trust guidance; it is not a substitute for tracing current source and proving a concrete failure path.

## Actors and authority

Roslyn Workbench runs locally under the operating-system identity of the person who started it. Two actors can affect a Workspace during one server lifetime:

- The **user** owns the machine, repository and server process. They may edit files through an IDE, Git, a shell or another development tool while the server is running.
- The **agent** is the authorised MCP client acting on the user's codebase. One agent is expected to use a particular MCP server. It may issue concurrent protocol requests, but it is not a hostile remote tenant.

The user and agent operate with the same effective filesystem authority. The product must assume that either can make mistakes, use stale information or overlap ordinary development activity. It does not claim to isolate them from each other or sandbox code already running as the same operating-system user.

## Expected usage

Most operations are queries. Mutations are expected to be small, coherent changes followed promptly by `transaction-commit` or `transaction-rollback`; a typical sequence is rename a symbol, inspect the preview, commit the transaction and then create a Git commit outside Roslyn Workbench.

The user may continue ordinary development while the agent is querying or preparing work. Filesystem change detection, snapshot preconditions, transaction revisions, commit revalidation and recovery must make ordinary overlap fail safely rather than silently applying a stale interpretation. An agent can then reload, resolve its targets again and retry.

The non-cancellable filesystem application phases of `transaction-commit` and automatic startup recovery are short coordinated boundaries. While `transaction-commit` is in progress, the user and other development tools must not edit the source paths shown in its preview or perform structural repository operations such as switching branches, checking out or resetting paths, moving directory trees, or replacing directories with links. The agent must not start a commit when it knows the user is editing an affected path or performing such an operation. After starting or restarting the Host, callers must avoid Workspace writes and structural repository operations until MCP initialisation completes and recovery status has been checked. Edits completed before these application phases remain subject to change detection and revalidation; no local pathname-based writer can guarantee safe arbitration with another tool writing the same target or replacing its directory topology during the final mutation itself.

The user or their tools may intentionally change solution and project structure. Roslyn Workbench must detect relevant changes, but it does not own project-file policy or silently reinterpret a requested source mutation to repair project membership.

Multiple Roslyn Workbench processes can inspect the same repository. Cross-instance status and commit coordination are advisory signals for cooperating instances; an agent must treat a Workspace reported as in use elsewhere as query-only until mutation ownership is coordinated.

## Trusted execution boundary

Opening a Workspace evaluates its MSBuild logic. Project analysers, Code Action providers and configured plugins can execute in the Host process with the user's permissions. Only fully trusted repositories and extensions belong inside this boundary.

The Host must still validate external contracts, reject malformed requests, protect transaction invariants, avoid accidental writes outside the selected Workspace, preserve recoverability and report failures accurately. Trusted execution is not permission to ignore ordinary correctness, data-loss or stale-state failures.

The Host is not required to defend against interference by code or another process already exercising the same user's authority outside the coordinated boundaries above. Examples outside the product model include structural repository changes during commit application or startup recovery, hostile mutation of Host memory, and an extension intentionally writing around the transaction pipeline. Supporting less-trusted execution in future would require a separate sandbox and threat model rather than incremental pathname checks.

## Scenario assessment for findings

Every implementation or review finding must identify:

1. **Actor:** user, authorised agent, cooperating Host instance, trusted extension or an actor outside the model.
2. **Action:** the concrete sequence needed to trigger the failure.
3. **Plausibility:** whether the sequence arises during normal querying, small transactional mutation, ordinary concurrent editing or recovery.
4. **Existing control:** the watcher, snapshot precondition, transaction gate, revalidation, recovery boundary or user-visible diagnostic that already handles the scenario.
5. **Impact:** the observable incorrect result, data loss, security boundary violation, compatibility failure or operational cost.
6. **Decision:** remediate, document an intentional product boundary, or accept a residual risk with a specific rationale.

Do not retain a finding solely because a theoretical interleaving exists. Equally, do not dismiss a plausible user or agent mistake merely because the repository is trusted. The decision must follow the operating model and the demonstrated failure scenario.

When remediation discussion establishes a new supported scenario or clarifies an existing boundary, update this document as part of that work so later reviews start from the accepted product model rather than reconstructing it from Git history or conversation context.

## Structural-write boundary

Structural repository operations that replace directory topology while `transaction-commit` or automatic startup recovery is applying files are outside the supported concurrency model, whether initiated directly or by Git. Existing containment checks remain required for ordinary and pre-existing link escapes. Native handle-relative mutation is not justified without a requirement to support concurrent structural changes during those application phases, a less-trusted Workspace model or a reproduced real-world failure.
