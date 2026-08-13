# Review Unit 6: Host and Protocol

Date: 2026-08-13

**Status:** Complete

## Evidence boundary

This review used only the current checked-out source, tests, project and configuration files, the current normative review programme and current Host/protocol documentation, plus current official Microsoft Learn documentation where .NET cancellation and serialisation semantics required confirmation. It did not use Git history, diffs, changed-file discovery, commits, branches, tags, stashes, reflogs, deleted or renamed artefacts, external backups, historical audits or previous review findings as evidence.

## Scope completed

The review covered the executable entry point, generic Host lifetime, stdout protection, startup composition and prerequisite ordering; all Host DI registrations, aliases and singleton lifetimes; command-line/environment resolution, precedence, validation and projection into subsystem options; MSBuild registration, state-directory initialisation and recovery; MCP options, stdio binding, direct and custom tool publication; input schemas, binding, recursive validation and output schemas; structured result envelopes and continuations; all four plugin/Code Action adapter families; every server-owned Workspace, transaction and status tool; unexpected exceptions, protocol exceptions, cancellation and stdin shutdown; and the current Host unit, contract, integration and relevant acceptance claims.

Complete consumer traces were followed into Units 1–5 and their filesystem, Roslyn, plugin, Code Action and transaction boundaries. No production code was modified.

## Startup, composition and configuration

`Program` redirects `Console.Out` to `Console.Error` before constructing the generic Host, so ordinary console writes and all configured console logging avoid the MCP stdout stream. `HostStartupComposer` independently resolves the supported Workbench arguments and environment variables, validates the resulting typed options and creates the fixed Code Action catalogue. The composition root then registers Workspace, plugin, Code Action, protocol, status and error-reporting services; direct MCP tools; hosted prerequisites; the fallback/plugin call filter; and stdio transport.

Startup lifecycle ordering is registration ordered: configuration warnings are reported first; MSBuild registration, secure state-directory initialisation and recovery run next; plugin discovery/materialisation publishes the immutable runtime catalogue last. State-directory or recovery failure aborts startup. MSBuild locator failure is deliberately converted to unavailable component status so the status/catalogue surface can remain available while Workspace opening fails through its normal boundary. Invalid supported scalar values fall back to defaults with warnings, repeated command-line values have the documented precedence, plugin directories are additive and de-duplicated, and error-reporting consent does not accept ambient environment elevation. A direct executable probe with a value-less `--default-max-results` confirmed that the generic Host and Workbench resolver coexist: the Host started, emitted the fallback warning on stderr and exited normally on stdin EOF.

The complete container is singleton-based; invocation isolation is provided by explicit Workspace/query/mutation leases rather than DI scopes. The current composition integration test builds with `ValidateOnBuild` and `ValidateScopes`, resolves every direct MCP tool, materialises the bundled plugin catalogue, checks unique names and verifies the principal singleton aliases. Plugin catalogue state owns and disposes the loaded catalogue and plugin service providers. Bounded stores, caches, the instance-status publisher and optional Sentry client are likewise Host-owned disposable singletons. Open Workspace sessions remain the exception described under shutdown.

## MCP publication and routing

Server-owned and Code Action tools are registered directly as `McpServerTool` services. Plugin tools are materialised at startup and exposed through the custom list/call handlers. The current MCP SDK tool collection augments those custom handlers, so the first `tools/list` page combines direct and plugin tools; subsequent custom pages are empty because the direct collection owns its own continuation. Reserved-name and collision policy prevents overlap among server-owned, Code Action and plugin tools. `ServerStatusService` counts the same three catalogue families, and current published-process source asserts that the status count equals the actual list count.

The Host publishes 12 base server tools, optionally two submission tools, three Code Action tools and the enabled plugin catalogue. Server-owned Workspace operations map Unit 1 lifecycle results into the common envelope; transaction start/preview/history/commit/rollback map Unit 2 outcomes; status projects MSBuild, Code Action, plugin, recovery and effective configuration state. Plugin query/mutation and Code Action query/mutation adapters remain distinct closed-generic families. Queries acquire shared execution contexts, mutations acquire exclusive transaction-bound contexts and successful candidates reach `IMutationStagingService`; every lease is asynchronously disposed. The Host does not create a direct source-write bypass.

## Binding, validation and schemas

All typed tools pass the incoming argument dictionary through `ToolRequestBinder`. It preserves web-style case-insensitive property names, detects omitted required members, rejects invalid scalar and nested enum values, applies top-level data annotations and then recursively validates the complete object graph, including selectors, collections and `IValidatableObject` constraints. Binding and validation failures become structured `InvalidRequest` tool errors without invoking a handler. Type/null mismatches become the same handled result after `JsonException` sanitisation.

Input schemas are generated through the current MCP SDK serializer contract, normalised, and extended with nullable, default, conditional, exact/at-least-one, prohibited and non-empty GUID constraints. A narrow current-source schema probe confirmed that object schemas intentionally do not set `additionalProperties: false`; System.Text.Json's default acceptance of unmapped members therefore agrees with the published schema rather than creating a schema/runtime mismatch. Optional full output mode publishes the success/failure envelope and bounded collection/continuation variants; omit mode changes publication only, not runtime serialisation.

Success results use `{ "ok": true, "data": ... }`; failures use `{ "ok": false, "error": ... }` with optional required action, diagnostics and warnings. Mutation success additionally records whether a candidate was staged. Continuation schemas and runtime mapping distinguish reload, retry, start/rollback transaction and narrower-scope guidance. Ordinary tool errors set `IsError`; content remains structured and no source or exception details are copied into unexpected-failure responses. Unit 3's `RWMCP2-006` remains valid because plugin admission can still accept a response type which this Host serializer cannot project as the required object.

## Cancellation, protocol failures and exception containment

Normal request cancellation is passed through binding-adjacent execution, Workspace/context acquisition, plugin and Code Action handlers, mutation staging, transaction operations and lease disposal. The published acceptance source sends a cancellation notification to a held plugin query and then proves the Workspace/query/transaction paths remain usable. Focused adapter tests cover cancellation during context and handler/staging paths.

The call-tool filter correctly captures ordinary escaping tool exceptions, logs to stderr, stores bounded diagnostic detail and returns a sanitised correlated `UnhandledException` envelope. The current MCP SDK first builds a dispatcher that selects a direct registered tool or the fallback handler and then wraps that combined dispatcher with registered filters. Published-process acceptance covers the plugin-fallback case and subsequent Host usability. Two exception-boundary defects remain; the earlier direct-tool bypass candidate is rejected by the verified SDK composition.

First, the filter rethrows every `OperationCanceledException` solely by runtime type. Current .NET cancellation semantics treat an exception as cooperative cancellation only when the associated token is actually cancelled; an unmatched or uncancelled exception is an ordinary fault. Any direct or fallback tool can throw `new OperationCanceledException()` during a non-cancelled call and bypass Workbench logging, correlation, bounded capture and the documented error envelope. The outer SDK dispatcher then converts the still-uncancelled exception to its generic error result. This is `RWMCP2-014`.

Second, the custom plugin router deliberately throws `McpProtocolException(InvalidParams)` for an unknown tool or unsupported task-augmented invocation, but the same call filter catches it under the general `Exception` clause and converts it to `UnhandledException`. Direct router and stream tests expect `InvalidParams`, but the stream test omits the production filter and the composition test exercises only a successful filtered delegate. The production pipeline therefore disagrees with its focused protocol tests. This is `RWMCP2-015`.

Final validation rejected `RWMCP2-016`. Inspection of the installed `ModelContextProtocol.Core` 1.4.1 implementation showed that direct/fallback selection occurs inside the base request handler and `BuildFilterPipeline` then wraps that complete handler. Direct lifecycle, transaction, status and Code Action failures therefore do reach `UnhandledToolExceptionFilter`; the lack of a filtered direct-tool failure test is a coverage gap, not evidence of bypass.

## Shutdown and resource ownership

Closing stdin stops the stdio transport and generic Host cleanly; the published lifetime source asserts exit code zero and logs remain on stderr. Host disposal releases plugin catalogues/service providers, caches, bounded stores, instance-status resources and the optional dispatcher/client. There is no shutdown lifecycle service for Workspace sessions, however, and `WorkspaceSessionStore` is neither disposable nor drainable. A client which leaves a Workspace open before EOF therefore retains its Roslyn Workspace and input watcher until process teardown. Unit 1's `RWMCP2-002` is revalidated at the final Host ownership boundary; the current lifetime acceptance opens no Workspace and cannot refute it.

## Representative outcomes

| Trace | Current outcome | Evidence assessment |
| --- | --- | --- |
| Clean startup and stdin EOF | Options compose, prerequisites run in order, catalogue publishes, stdio starts and EOF produces a zero exit. | Unit/integration composition and published lifetime source cover the boundary; a narrow current executable probe also exited cleanly. |
| Invalid/fallback configuration | Supported malformed values fall back with warnings; invalid state/recovery prerequisites abort; MSBuild discovery failure publishes unavailable status. | Resolver/validator/reporter and prerequisite tests cover the branches. |
| Tool list and status | SDK direct tools and immutable plugin tools form one catalogue; names are unique and status reports the same count. | Container integration and published protocol source cross the composition boundary. |
| Missing/type/null/enum/object-invalid input | Binding rejects before handler invocation with structured `InvalidRequest`. | Binder, graph-validator, schema and every adapter family have focused coverage. |
| Extra input member | Schema permits it and System.Text.Json skips it. | Current generated schema probe and official serializer semantics agree; no candidate recorded. |
| Plugin and Code Action queries | Shared lease, typed handler, structured projection and async disposal. | Unit and Host integration coverage cross both families. |
| Plugin and Code Action mutations | Exclusive context, direct-write containment, candidate processing and transaction staging. | Adapter and integration coverage crosses Unit 2; Unit 5's Fix All identity defect remains before staging. |
| Expected handler failure | Family result maps to a failure envelope and continuation without throwing. | Focused tests cover rejected, busy, stale, no-change and staging outcomes. |
| Unexpected plugin handler exception | Fallback filter captures, sanitises, correlates and Host stays usable. | Focused and published-process source cover ordinary plugin exceptions. |
| Unexpected server-owned/Code Action exception | The combined SDK dispatcher passes it through the Workbench filter for correlation and capture. | Current MCP SDK pipeline inspection establishes the route; no current filtered direct-tool failure test exists. |
| Genuine request cancellation | `OperationCanceledException` propagates and held resources are released. | Focused and published cancellation source cover a cancelled request token. |
| Uncancelled cancellation exception | Filter rethrows it, skips Workbench capture, and the outer SDK dispatcher returns its generic error result. | `RWMCP2-014`; no current test covers this distinction. |
| Unknown/task-augmented plugin call | Router creates `InvalidParams`, then production filter remaps it to correlated `UnhandledException`. | `RWMCP2-015`; existing stream test omits the filter. |
| Shutdown with open Workspace | Generic Host has no owner which drains session resources. | Revalidated `RWMCP2-002`; EOF test opens no Workspace. |

## Candidate findings

| ID | Severity | Confidence | Summary |
| --- | --- | --- | --- |
| `RWMCP2-002` | P2 | High | Revalidated: generic Host shutdown does not drain open Workspace sessions or dispose their Roslyn/input-watcher resources. |
| `RWMCP2-006` | P2 | High | Revalidated: Host runtime success serialisation requires object-shaped plugin query data, while default plugin admission does not enforce it. |
| `RWMCP2-014` | P2 | High | An uncancelled or unrelated `OperationCanceledException` bypasses Workbench correlation/capture before the outer SDK dispatcher returns a generic error. |
| `RWMCP2-015` | P2 | High | The fallback unexpected-exception filter remaps deliberate MCP `InvalidParams` exceptions to correlated `UnhandledException` tool results. |

No additional candidate was recorded for configuration precedence, object-graph validation, schema generation, output-schema mode, catalogue count, direct/plugin publication, adapter staging, stdout integrity or ordinary stdin shutdown because current source and focused tests establish internally consistent behaviour for the reviewed scenarios.

## Test and executable evidence

| Evidence | Result | Boundary established |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Test` | 480/480 passed | Configuration, composition, plugin loading, contracts/schemas/binding, envelopes, four adapters, server-owned tools, status and exception filter. |
| `Roslyn.Workbench.Mcp.IntegrationTest` | 68/68 passed | Full DI graph, plugin packages/load contexts, protocol routing, adapters, Workspace containment, recovery status and external boundaries. |
| Narrow Host startup probe | Exit 0 | A value-less known option falls back with a stderr warning and stdin EOF shuts the current Host down cleanly. |
| Narrow generated-schema probe | Current schema inspected | The transaction commit request schema permits additional properties, matching runtime unmapped-member handling. |

The pinned .NET 10 SDK was available. WSL commands used the required `/tmp/artifacts/roslyn-workbench-mcp` artefact routing. The acceptance source was inspected for published catalogue/schema, Workspace/transaction lifecycles, cancellation, failure containment, console-output isolation and EOF shutdown, but the acceptance suite was not executed because no acceptance artefact was changed and repository policy does not authorise an automatic run for this docs-only review.

The repository-required Roslyn MCP was not available in the active tool set, so solution and call-site navigation used current local source inspection. This is a tooling limitation, not an evidence-boundary expansion. Current official Microsoft Learn documentation was used to confirm that System.Text.Json ignores unmapped members by default and that an `OperationCanceledException` represents cooperative task cancellation only when the relevant token is cancelled.

## Earlier-unit revisits

Unit 1's selectors, snapshots, lifecycle result mapping and leases were rechecked through every server-owned and typed adapter. `RWMCP2-001` remains visible in the Host schema because `list-code-actions` still has no expected snapshot. `RWMCP2-002` was revalidated at generic Host shutdown; `RWMCP2-003` remains on explicit close beneath the Host mapper.

Unit 2's start/preview/history/commit/rollback registrations and mappings were traced through the server-owned tools, and every successful plugin/Code Action mutation still routes through staging. The Host adds no protection that changes `RWMCP2-004` or `RWMCP2-005` at their filesystem/recovery boundaries.

Unit 3's immutable runtime catalogue, reserved names, plugin-scoped lifetime and typed adapters were rechecked. The complete Host serializer path revalidates `RWMCP2-006`; plugin catalogue disposal is Host-owned and deterministic for published catalogues.

Unit 4's 39 bundled registrations flow through the plugin query/mutation adapters and common envelope. Host serialisation supplies no aggregate byte ceiling or nested collection bound, so `RWMCP2-009` and `RWMCP2-010` retain their process-availability impact; the Host does not alter the result semantics behind `RWMCP2-007` or `RWMCP2-008`.

Unit 5's three direct registrations, contexts and staging adapter were rechecked. The Host preserves exact action IDs and expected snapshots it receives but cannot repair the missing list snapshot in `RWMCP2-001`, the prepared-candidate identity gap in `RWMCP2-011`, or the audit-only coverage defects `RWMCP2-012` and `RWMCP2-013`. Final SDK inspection established that unexpected Code Action failures do cross the combined call-tool filter, rejecting `RWMCP2-016`.

The architecture map was corrected to describe the combined direct/fallback exception filter, its actual exception classification and the absence of a Workspace-session shutdown drain. No project, dependency, entry-point, composition-root, contract, boundary or extension-mechanism omission was otherwise found.

## Unit conclusion

The current Host has a coherent composition and transport structure: startup prerequisites are ordered, stdout is protected, catalogues are immutable, schemas and typed binding agree on the inspected contracts, and all four adapter families preserve Workspace/transaction boundaries. Four relevant defects remain: the previously recorded open-Workspace shutdown leak and plugin admission/runtime response mismatch, plus uncancelled cancellation exceptions bypassing Workbench diagnostic capture and deliberate MCP protocol errors being remapped as unexpected tool failures. Final SDK inspection rejected the earlier direct-tool bypass candidate. Review unit 6 is complete.
