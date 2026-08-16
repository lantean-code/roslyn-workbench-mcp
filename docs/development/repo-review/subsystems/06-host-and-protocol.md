# Unit 6 — Host and protocol

Date: 2026-08-16

**Report status:** Completed.

## Scope and evidence

This review covered executable bootstrap, Host and DI composition, configuration, startup services, MSBuild registration, plugin and Code Action catalogue publication, MCP SDK binding, request schemas, structured envelopes, server-owned lifecycle and transaction tools, all four adapter families, cancellation and exception handling, error attribution, stdio lifetime, shutdown and current unit, integration and acceptance tests. It used current source, current normative documentation, installed package metadata and current official System.Text.Json documentation. No files changed and no tests ran.

## Composition, publication and lifecycle

The Host redirects ordinary console output away from stdout before composition, configures MCP stdio and publishes a fixed startup catalogue of server-owned, internal Code Action and enabled plugin tools. SDK DI registrations augment rather than replace the dynamic handler; dynamic dispatch handles only names absent from the static collection. Collision validation and catalogue publication complete before requests are served.

Hosted services stop in the correct reverse order, so MCP transport shutdown precedes Workspace draining. Workspace shutdown attempts all session cleanup before surfacing failures. Plugin catalogue disposal supports async-only plugin services through `PluginServiceProviderLifetime.Dispose()` bridging to `ServiceProvider.DisposeAsync()`, so the suspected synchronous-disposal defect was rejected.

## Protocol, binding and adapters

Common envelope mapping consistently distinguishes success, rejection, conflict, fault and unexpected correlated failure. Deliberate protocol exceptions and caller-requested cancellation pass through the top-level filter; other exceptions are sanitised and retained by correlation ID.

Plugin and Code Action adapters acquire invocation-scoped Workspace leases, preserve cancellation, map typed results and converge mutations on Workspace staging. They attach authoritative Workspace context to unexpected post-acquisition exceptions. Server-owned tools delegate through `ServerOwnedToolBase` and lack an equivalent context hand-off, producing `RWMCP3-015`.

Schema generation and binding agree for required members, nullability, enums, component-model validation, cross-member rules and the explicitly closed `WorkspaceMsBuildProperties` object. They disagree for undeclared top-level members: published schemas are closed, but normal request records silently accept and discard unknown members, producing `RWMCP3-014`.

## Candidates

### RWMCP3-014 — Undeclared top-level request members are silently discarded before optional defaults

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/Protocol/ToolRequestBinder.cs:20-81,361-370`; `src/Roslyn.Workbench.Mcp.Workspace/Selection/WorkspaceSelectorService.cs:20-31`

With one Workspace open, a caller can misspell the top-level `workspace` member on `workspace-close` or `transaction-rollback`. System.Text.Json's default handling discards the member and binds the selector as null; the selector service interprets omission as the sole loaded Workspace and targets it. Unknown optional limits likewise silently use defaults despite a closed published schema. Reject undeclared request members during binding, preserving only explicitly extensible contracts, and add protocol tests for misspelled selectors and other unknown top-level properties.

### RWMCP3-015 — Server-owned lifecycle failures can lose authoritative Workspace attribution

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/Tools/ServerOwnedToolBase.cs:41-64`; `src/Roslyn.Workbench.Mcp/ToolExecution/UnhandledToolExceptionFilter.cs:64-81,99-120`; `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:225-265`; `src/Roslyn.Workbench.Mcp/ErrorReporting/Capture/ErrorCaptureService.cs:50-116`

`workspace-close` can resolve and remove a session, then fail during cleanup. The server-owned call attaches no retained Workspace context, and fallback reconstruction against the post-failure store cannot find the removed session. Correlated details consequently omit Workspace identity and state, and Workspace-scoped consent is evaluated without the affected Workspace. Retain immutable authoritative context after server-owned target resolution and attach it to unexpected failures while preserving cancellation/protocol pass-through; add a protocol-level cleanup-failure test.

## Cross-unit conclusions

- `RWMCP3-003` is corroborated: published calls are concurrent and Host adds no serialisation around transaction start.
- `RWMCP3-004` is corroborated: public snapshot preconditions pass unchanged at the initial request boundary.
- `RWMCP3-006` is corroborated: malformed recovery normalisation escapes as a generic correlated failure.
- `RWMCP3-001`, `RWMCP3-002`, `RWMCP3-005` and `RWMCP3-007` receive no compensating Host check.
- Unit 5 sibling-root recipes are serialised without a uniqueness check.
- Unit 5 provider exceptions become one correctly attributed but whole-call generic failure; the Host does not preserve unaffected provider results.

## Test gaps and limitations

Current tests substantiate catalogue composition, schema generation, required and nested validation, adapter mapping and attribution, cancellation, protocol pass-through, correlated failures, concurrency, startup, stdout isolation, EOF lifetime and shutdown ordering. Material gaps are runtime protocol coverage for unknown top-level members, actual server-owned failures after session removal or replacement, published EOF with open resources and async-only plugin services, and provider-failure acceptance coverage. No other defect was substantiated in DI, configuration precedence, catalogue merging, envelope semantics, cancellation, shutdown ordering or plugin async disposal.

