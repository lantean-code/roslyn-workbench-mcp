# Subsystem review: tests, fixtures and scenario runner

## Scope and relationships

This unit covers all `*.Test`, `*.IntegrationTest`, `*.AuditTest`, `AcceptanceTest` and test-support projects plus `tools/Roslyn.Workbench.Mcp.ScenarioRunner`. Tests span every production subsystem; the runner launches a published stdio Host against pinned external repositories and measures workflows, concurrency and recovery.

## Implementation and boundary review

- Unit tests use mocked production collaborators plus real in-memory Roslyn data where semantic behaviour matters. Contract tests lock JSON schema/public plugin surface. Integration support builds real Workspace/Plugin/CodeAction components over temporary directories.
- Acceptance tests own published-process MCP behaviour and are intentionally manual. Audit tests own built-in provider/replay catalogues and are excluded from the fast loop. The scenario runner is also explicitly release/scenario-only.
- The runner pins repository commits, isolates NuGet packages, validates clean tracked state, launches/terminates Host processes, restores exact repository changes and validates recovery/status cleanup. Repository definitions are trusted scenario inputs because preparation commands and workspace loading execute code.
- Process/repository cleanup uses non-cancellable tokens after a measurement so cancellation cannot abandon a mutated checkout. Paths derived from git output are checked to remain under the repository before deletion.

## Validation and findings

The six fast-loop projects passed 1,925 tests. The Workspace integration project ran 73 tests but 32 failed during DI validation because of RWMCP-004; the remaining 41 passed. The Host integration project passed 59 of 62 tests, with two RWMCP-004 failures and the independent RWMCP-005 catalogue failure. Plugins.Core and CodeActions integration projects share the same broken fixture and could not provide meaningful additional results without modifying test support. Acceptance, audit and external-repository scenarios were not run, in accordance with repository policy and the absence of explicit user authorisation for acceptance execution.
