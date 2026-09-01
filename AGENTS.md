# AGENTS.md (root)

> Scope: This file gives high-level context and guardrails for the repository root. Deeper folders may add their own `AGENTS.md` files which take precedence for their subtrees (for example `./src/AGENTS.md` and `./test/AGENTS.md`).

## Project overview

- Roslyn Workbench is a local, stdio-based MCP server for Roslyn-powered code inspection and transactional refactoring workflows.
- Primary goals: precise MCP contracts, safe transactional source changes, stable workspace semantics, and a plugin-based query/mutation tool model.
- Third-party query and mutation tools are plugins; internal Code Action tools use a separate, Host-published catalogue.
- Non-goals: direct-write source mutations outside the transaction pipeline, dynamic tool discovery after process startup, and silent reinterpretation of stale spans or symbols against newer workspace snapshots.

## Repository layout

- Solution: `Roslyn.Workbench.Mcp.slnx`
- Release docs: `./docs`
- Development plans, audits and evidence: `./docs/development`
- Projects:
  - `Roslyn.Workbench.Mcp.Abstractions` — minimal public Workspace selectors, result models, resolver contracts, and project/query service contracts shared with third-party plugins.
  - `Roslyn.Workbench.Mcp` — executable host, bootstrap, and server-owned core MCP lifecycle tools.
  - `Roslyn.Workbench.Mcp.Workspace` — workspace loading, neutral execution leases, transaction coordination, and commit/reload infrastructure.
  - `Roslyn.Workbench.Mcp.CodeActions` — internal Code Action contracts, catalogue, workflows, and Workspace adapters.
  - `Roslyn.Workbench.Mcp.Plugins` — public third-party plugin contracts, registration, and Workspace adapters.
  - `Roslyn.Workbench.Mcp.Plugins.Core` — bundled inspection contracts and first-party plugins.
  - `*.Test` and `*.TestSupport` projects under `./test` — unit and integration tests plus shared test helpers.
- Config/conventions: `.editorconfig`, `nuget.config`, `global.json`, and the `AGENTS.md` files in this repository.

## Build, test, publish

- Prerequisites: .NET 10 SDK (use the version pinned by `global.json`).
  - Agents must verify the pinned SDK is available in the current environment.
  - If `dotnet --info` does not list the required version, install it before running restore/build/test commands.
  - When operating under WSL, agents must append `--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` to repository `dotnet` commands, including `dotnet format`, to keep generated artifacts off the shared Windows filesystem. Microsoft.Testing.Platform's .NET 10 `dotnet test` driver does not accept that SDK switch, so test commands must instead append the equivalent MSBuild property `-p:ArtifactsPath=/tmp/artifacts/roslyn-workbench-mcp`.
  - When operating directly on Windows or Linux, agents must omit `--artifacts-path`; the WSL-specific routing is not needed.
- After modifying code or tests:
  - Run `dotnet format --include <changed files>` for the files changed in the current task only, applying the WSL-specific artifacts path above when required.
  - Do not format unrelated files.
- Restore and build:
  - `dotnet restore`
  - `dotnet build`
- Run the SDK `latest-all` .NET analyzer set after modifying C# source or tests:
  - Run `dotnet build <affected-project> --no-restore -p:AnalysisLevel=latest-all -p:EnforceCodeStyleInBuild=true -p:CodeAnalysisTreatWarningsAsErrors=false`, applying the WSL-specific artifacts path above when required.
  - This analyzer build is additional to the normal build. `AnalysisLevel=latest-all` exposes default-disabled `CAxxxx` diagnostics that the IDE can show even when the normal build reports no warnings.
  - Review analyzer diagnostics for every C# file changed in the current task. Fix diagnostics that apply, or record a concise rationale when retaining the code is intentional.
  - Do not expand a scoped change into repository-wide analyzer cleanup merely because referenced projects report existing diagnostics.
  - The current solution baseline, known `latest-all` exclusions and remediation policy are recorded in `./docs/development/Analyzer Inventory.md`.
- Run tests:
  - Run the affected non-acceptance test projects, or use the preferred fast-loop command defined in `./test/AGENTS.md`.
  - Do not run the acceptance-test project automatically merely because production code participates in published-Host scenarios. Run acceptance tests when the user explicitly requests them or when the current task adds or modifies any acceptance-test source, fixture, project configuration, wrapper script or checked-in acceptance asset.
  - Changing an acceptance-test artifact is itself authorisation and an explicit requirement to run the complete acceptance suite through the platform wrapper before finishing; no separate user request is required. Do not substitute a build, analyzer run, direct `dotnet test` invocation or filtered subset. If the complete wrapper cannot run, report the validation as incomplete rather than claiming the change is ready for confirmation.
  - Do not run the external-repository scenario runner automatically for ordinary development. When scenario-runner code or a checked-in scenario definition is changed, run every affected scenario through the platform wrapper before finishing; shared runner changes require representative coverage of every affected scenario family or repository.
- After each behavior-affecting set of changes:
  - Run the relevant non-acceptance tests, applying the WSL-specific artifacts path above when required.
  - Behavior-affecting includes edits to production code, test code, project/package/build configuration, tool contracts, plugin registration, or other runtime-impacting assets.
  - Docs-only or markdown-only edits do not require restore/build/test unless explicitly requested.

## Dogfood usage logging

- Record every request sent to the configured published Roslyn Workbench dogfood server in [`./docs/development/repo-review/dogfood-improvement-usage.md`](./docs/development/repo-review/dogfood-improvement-usage.md) until the user explicitly ends this requirement.
- This applies to all repository work, not only implementation of the DOGFOOD improvement worklist, and includes lifecycle, query and mutation requests.
- Record calls in execution order with the related work item or activity, tool name, purpose, request and outcome. Retain failed calls, retries, blank client projections and abandoned approaches because they are usage evidence.
- Redact machine-specific repository roots, temporary paths and other incidental local values where their exact value is not material; retain contract fields, response codes, continuations and other evidence needed to understand the interaction.
- Do not add shell commands, Microsoft Learn queries or other non-dogfood tool use to this ledger.

## Coding and test standards

- Source code rules and generation constraints live in `./src/AGENTS.md` and are authoritative for code style, architecture boundaries, and documentation.
- Unit and integration test rules live in `./test/AGENTS.md` and are authoritative for test structure, naming, mocks, and coverage.
- Repository C# style uses file-scoped namespaces by default; keep `.editorconfig` and generated code aligned with that preference unless a specific file genuinely requires block-scoped namespaces.
- If rules conflict, the deeper file wins for its subtree; otherwise follow both.

## Line endings

- Use CRLF line terminators for any files you write or modify.
- After editing any file that is expected to use CRLF, run `unix2dos <changed files>` to normalize the entire file and eliminate any LF or mixed endings introduced by patching tools.
- Do not run `unix2dos` on files that are intentionally LF per `.gitattributes` or repository convention (for example `*.sh`, `*.bash`, `*.py`, and `justfile`).
- Before finishing, verify every changed CRLF-governed file is `crlf` and not `mixed`.

## Markdown formatting

- Do not manually hard-wrap Markdown prose for visual sizing. Keep each paragraph, list item and table row on one physical line and rely on the Markdown renderer for visual wrapping; introduce line breaks only where Markdown structure or meaning requires them.

## Git permissions

- Agents must not perform git write operations unless the user gives explicit permission in the current conversation.
- Git write operations include (but are not limited to): `commit`, `push`, `pull`, `merge`, `rebase`, `cherry-pick`, `reset`, `revert`, `checkout`/`switch` that changes branch or files, tag creation/deletion, and branch creation/deletion.
- Until explicit permission is granted, only read-only git commands are allowed.

## How to work in this repo (for agents)

1. Read this file, then the relevant folder `AGENTS.md` (`src` or `test`).
2. Read the relevant design docs under `./docs/development` before making structural or contract changes.
   - Use [`./docs/development/ProductOperatingModel.md`](./docs/development/ProductOperatingModel.md) when assessing supported actors, concurrency, trust boundaries, failure scenarios and whether a review finding represents a product defect.
3. When working on C#, .NET SDK, Roslyn, or MCP contract behaviour:
   - Use Microsoft Learn for current official .NET guidance.
   - Use Roslyn-backed tooling for solution inspection, symbol lookup, and safe refactor planning where available.
   - Prefer Roslyn-aware refactors over text search/replace for symbol-level changes.
4. Before modifying code:
   - Confirm the target framework, nullable context, analyzer settings, and `.editorconfig` rules.
   - Keep public contract and snapshot semantics consistent with the design docs.
   - Preserve the server-owned versus plugin-owned boundary.
5. When generating code:
   - Follow `./src/AGENTS.md` exactly.
   - Prefer minimal, maintainable changes; avoid churn to unrelated files.
6. When writing tests:
   - Follow `./test/AGENTS.md` exactly.
7. Before opening a PR:
   - Build succeeds and tests are green.
   - Public XML docs added or updated where required.
   - PR summary explains what changed, why, risks, and testing.
   - Prefer the repository PR template in `.github/PULL_REQUEST_TEMPLATE.md` when present.

## Review Agent isolation

- The initial Review Agent pass for each independently confirmed work item must be executed by a fresh subagent, not by the primary working agent. Re-review turns for corrections within that same work item must reuse the same review subagent.
- Spawn the initial review subagent without conversation context so its conclusions are not influenced by implementation discussion or earlier review conclusions. Give it only the review target, the applicable repository instructions and the requirement to read and follow the Review Agent skill.
- The primary agent must include the exact validation commands already run, their results and whether the reviewed files changed afterwards in the review handoff. The review subagent must treat current successful validation evidence as reusable and must not repeat an equivalent command against the unchanged reviewed baseline merely to reproduce the same result.
- The review subagent may run additional or repeated validation only when the supplied evidence is missing or stale, a suspected defect requires reproduction, the existing coverage does not exercise the reviewed behaviour, or a materially different command is needed. Its report must distinguish validation evidence supplied by the primary agent from commands the review subagent ran itself.
- The review subagent must remain read-only and return its findings to the primary agent. It must not modify files, stage changes or delegate the review again.
- After review-driven corrections, return the remediation and current validation evidence to the same Review Agent in a follow-up turn. The repeat review must verify its previous findings and inspect the correction; it must not restart the complete baseline review unless the correction materially changes the approved architecture or direction.
- Use a new fresh Review Agent for the next independently confirmed work item, not for each correction within the current item. Replace the current item's reviewer only when it is unavailable or an approved architectural redirection establishes a new review baseline, and state that replacement explicitly.

## PR and review checklist

- [ ] Change is scoped and well-justified; no unrelated edits.
- [ ] Code adheres to `./src/AGENTS.md` standards.
- [ ] Tests adhere to `./test/AGENTS.md` and achieve required coverage.
- [ ] No secrets, tokens, or user-specific paths committed.
- [ ] Builds with the pinned SDK; `dotnet restore`, `dotnet build`, and the relevant non-acceptance tests succeed when required by the change.
- [ ] Acceptance tests have been run only when explicitly requested by the user or required because an acceptance-test artifact changed; every changed acceptance artifact has received a complete platform-wrapper run.
- [ ] Error messages and logs are clear and actionable.

## Communication and assumptions

- Do not guess. If any requirement, API contract, or behaviour is unclear, ask for clarification.
- Evaluate implementation and review concerns against [`./docs/development/ProductOperatingModel.md`](./docs/development/ProductOperatingModel.md). For each finding, identify the actor, concrete action, plausibility, existing controls and user-visible impact; do not promote a theoretical interleaving outside the supported operating model into a product defect without a realistic failure scenario.
- When reviewing pull request feedback, only unresolved review threads/comments are actionable by default unless the user explicitly asks to revisit resolved items.
- Prefer concise diffs and explicit rationale in commit messages and PR descriptions.
- When generating PR summaries, commit messages, review replies, release notes, or other GitHub-facing repository text, use British English spelling and phrasing.
- Keep PR `Summary` focused on user-visible or architectural intent, `What Changed` focused on the code/design changes, `Testing` focused on the added or updated coverage and validated scenarios, and `Notes` for risks, migration details, or reviewer guidance.
- Do not include local-worktree status, unrelated modified files, or other agent-only bookkeeping in PR text unless it materially affects the diff under review.

## GitHub interactions

- When interacting with GitHub repository state (for example PRs, PR comments, reviews, issues, or release metadata), prefer the `gh` CLI where possible instead of manual browsing or ad-hoc API calls.
