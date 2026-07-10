# AGENTS.md (root)

> Scope: This file gives high-level context and guardrails for the repository root. Deeper folders may add their own `AGENTS.md` files which take precedence for their subtrees (for example `./src/AGENTS.md` and `./test/AGENTS.md`).

## Project overview
- Roslyn Workbench is a local, stdio-based MCP server for Roslyn-powered code inspection and transactional refactoring workflows.
- Primary goals: precise MCP contracts, safe transactional source changes, stable workspace semantics, and a plugin-based query/mutation tool model.
- Third-party query and mutation tools are plugins; internal Code Action tools use a separate, Host-published catalogue.
- Non-goals: direct-write source mutations outside the transaction pipeline, dynamic tool discovery after process startup, and silent reinterpretation of stale spans or symbols against newer workspace snapshots.

## Repository layout
- Solution: `Roslyn.Workbench.Mcp.slnx`
- Docs: `./docs`
- Projects:
  - `Roslyn.Workbench.Mcp` — executable host, bootstrap, and server-owned core MCP lifecycle tools.
  - `Roslyn.Workbench.Mcp.Workspace` — workspace contracts, loading, neutral execution leases, transaction coordination, and commit/reload infrastructure.
  - `Roslyn.Workbench.Mcp.CodeActions` — internal Code Action contracts, catalogue, workflows, and Workspace adapters.
  - `Roslyn.Workbench.Mcp.Plugins` — public third-party plugin contracts, registration, and Workspace adapters.
  - `Roslyn.Workbench.Mcp.Plugins.Core` — bundled inspection contracts and first-party plugins.
  - `*.Test` and `*.TestSupport` projects under `./test` — unit and integration tests plus shared test helpers.
- Config/conventions: `.editorconfig`, `nuget.config`, `global.json`, and the `AGENTS.md` files in this repository.

## Build, test, publish
- Prerequisites: .NET 10 SDK (use the version pinned by `global.json`).
  - Agents must verify the pinned SDK is available in the current environment.
  - If `dotnet --info` does not list the required version, install it before running restore/build/test commands.
  - Agents must include `--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` on all `dotnet` commands, including `dotnet format`.
- After modifying code or tests:
  - Run `dotnet format --include <changed files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` for the files changed in the current task only.
  - Do not format unrelated files.
- Restore and build:
  - `dotnet restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
  - `dotnet build --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- Run tests:
  - `dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- After each behavior-affecting set of changes:
  - Run `dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`.
  - Behavior-affecting includes edits to production code, test code, project/package/build configuration, tool contracts, plugin registration, or other runtime-impacting assets.
  - Docs-only or markdown-only edits do not require restore/build/test unless explicitly requested.

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

## Git permissions
- Agents must not perform git write operations unless the user gives explicit permission in the current conversation.
- Git write operations include (but are not limited to): `commit`, `push`, `pull`, `merge`, `rebase`, `cherry-pick`, `reset`, `revert`, `checkout`/`switch` that changes branch or files, tag creation/deletion, and branch creation/deletion.
- Until explicit permission is granted, only read-only git commands are allowed.

## How to work in this repo (for agents)
1. Read this file, then the relevant folder `AGENTS.md` (`src` or `test`).
2. Read the relevant design docs under `./docs` before making structural or contract changes.
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

## PR and review checklist
- [ ] Change is scoped and well-justified; no unrelated edits.
- [ ] Code adheres to `./src/AGENTS.md` standards.
- [ ] Tests adhere to `./test/AGENTS.md` and achieve required coverage.
- [ ] No secrets, tokens, or user-specific paths committed.
- [ ] Builds with the pinned SDK; `dotnet restore`, `dotnet build`, and `dotnet test` succeed when required by the change.
- [ ] Error messages and logs are clear and actionable.

## Communication and assumptions
- Do not guess. If any requirement, API contract, or behaviour is unclear, ask for clarification.
- When reviewing pull request feedback, only unresolved review threads/comments are actionable by default unless the user explicitly asks to revisit resolved items.
- Prefer concise diffs and explicit rationale in commit messages and PR descriptions.
- When generating PR summaries, commit messages, review replies, release notes, or other GitHub-facing repository text, use British English spelling and phrasing.
- Keep PR `Summary` focused on user-visible or architectural intent, `What Changed` focused on the code/design changes, `Testing` focused on the added or updated coverage and validated scenarios, and `Notes` for risks, migration details, or reviewer guidance.
- Do not include local-worktree status, unrelated modified files, or other agent-only bookkeeping in PR text unless it materially affects the diff under review.

## GitHub interactions
- When interacting with GitHub repository state (for example PRs, PR comments, reviews, issues, or release metadata), prefer the `gh` CLI where possible instead of manual browsing or ad-hoc API calls.
