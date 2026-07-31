# Deep dive 3 — Plugin platform

Date: 2026-07-31

Status: Complete

## Scope and dependency map

The review covered the public Plugins and Abstractions authoring surface, fluent registration and result contracts, Workspace query and mutation context adaptation, package discovery and physical containment, PE metadata inspection, per-package assembly load contexts, shared dependency identity, MEF composition, plugin-local and global collision handling, handler construction, Host dependency-injection materialisation, MCP schema publication, query-cache partitioning and invalidation, the C# authoring analysers, NuGet package contents, bundled consumers and external fixture consumers. Dependency direction remains coherent: Abstractions owns neutral Workspace selectors and services; Plugins owns the third-party contract and adapters; Workspace owns leases, staging and cache state; Host alone owns discovery, loading, MCP publication and status; Plugins.Core is a bundled consumer.

The Host registrations use singleton lifetimes consistently with the documented catalogue model. Handler instances are constructed once only after plugin-local validation and global collision checks. Invocation contexts and leases are created per call; the plugin query-cache store, its lifecycle observer and stateless execution services are singleton owners of synchronised state. No captive-lifetime or cross-plugin DI registration defect survived validation.

## Representative traces

### Valid external package loading

Startup resolves and validates configured plugin search roots, enumerates immediate package directories, physically contains each top-level DLL, reads PE metadata without executing plugin code and requires exactly one marked entry point. Metadata identity, SemVer and exact API compatibility are validated before a non-collectible package load context is created. The load context shares Plugins, Abstractions, System.Composition and Microsoft.CodeAnalysis identities from the default context while resolving other managed and native dependencies within the package. MEF composes exactly one `IRoslynPlugin`, configuration freezes on return, runtime preparation validates handler contracts and metadata, collision policy runs before handler construction, and enabled registrations are projected into Host DI.

The ordinary path is deterministic and package-contained. Package, load-context, composition and clean packaged-consumer tests exercise this path. RWMCP-023 identifies a startup resource defect before loading: discovery reads every managed or native DLL fully into one managed byte array merely to inspect its PE metadata, so a legitimate package carrying a large native dependency causes an avoidable working-set spike.

### Incompatible and conflicting packages

Malformed metadata, unsupported API versions, missing or multiple marked entry points, composition failures, invalid handler contracts, constructor failures, duplicate external plugin IDs and reserved or cross-plugin tool-name collisions produce disabled plugin status rather than partial registration. Bundled collisions with Host-owned names fail startup because they are an internal invariant; external collisions disable every participant deterministically. Load contexts created before later configuration or global collision failure remain non-collectible for the process lifetime, which is consistent with the stated no-unload model rather than an independently actionable leak.

RWMCP-020 shows that name validation stops at non-blank and uniqueness checks. A name containing whitespace or more than 128 characters is enabled and copied directly into `Protocol.Tool`, bypassing the C# MCP SDK factory's `^[A-Za-z0-9_.-]{1,128}$` validation. The plugin can therefore appear enabled in `server-status` while publishing a protocol-invalid, client-rejected tool identifier.

### Query and mutation invocation

Host adapters bind the public request, acquire the appropriate Workspace lease, build a plugin context from the immutable effective Solution and invoke the singleton typed handler. Both adapters run unexpected-Workspace-change detection in a `finally`, including when the handler throws or cancellation propagates. Query results are serialised through the plugin result envelope. Mutation handlers receive no stager; a successful candidate is returned to the Host and staged through the Workspace-owned transaction boundary with the invocation token. Handler cancellation propagates as cancellation rather than being remapped to an internal plugin error.

A retained-context candidate was rejected as RWMCP-024. A cooperative plugin can retain an immutable Solution or resolver because it is trusted in-process code, but the query-cache scope becomes inactive at lease completion, mutation staging remains Host-only, and the next Workspace acquisition rechecks underlying Workspace identity. Preventing an adversarial or deliberately suppressed plugin from retaining ordinary CLR references would require process isolation, which the documented trust model explicitly excludes.

### Query-cache scope and invalidation

Each query invocation creates a cache scope keyed by exact `WorkspaceSnapshotIdentity`, plugin ID and registered tool name. Entry identity additionally includes key and value CLR types plus the plugin key. Identical misses coalesce; caller cancellation releases only that waiter and cancels shared work only when the last waiter leaves. Null, disposable and async-disposable values are returned but not admitted. Snapshot, transaction and Workspace lifecycle observers invalidate partitions and reject late stores from old generations. The public scope throws after lease disposal, while matching entries remain reusable through later valid scopes. Focused tests cover scope identity, plugin/tool/snapshot isolation, coalescing, recursion, cancellation, non-admission, invalidation and retained-scope rejection; no cross-plugin or stale-snapshot reuse defect survived validation.

### Authoring package and analyser/runtime parity

The NuGet project packs the C# analyser under `analyzers/dotnet/cs` and the matching Abstractions assembly under `lib/net10.0`; the clean external consumer restores and builds using only the package. The analysers cover direct Workspace mutation, live Workspace reads, asynchronous configuration, escaped startup configuration, handler shape and lifetime, transport visibility, state warnings, cancellation observation, bounded result contracts, entry metadata and cache key/value shape. Runtime preparation independently enforces most objective binary and package checks.

Two runtime-authority gaps remain. RWMCP-021 records that a public request or response type is accepted without exercising the actual Host serializer/schema provider. A request containing an unsupported `System.Type` or delegate member therefore reaches DI publication and throws while the Host resolves its complete `IEnumerable<McpServerTool>`; in full-schema mode an unsupported response has the same effect. RWMCP-022 records that an `async void IRoslynPlugin.Configure` implementation is rejected only by RWMCP003. If the diagnostic is suppressed or the binary was built without the analyser, MEF calls it as an ordinary `void` method, Host freezes and may enable a partial catalogue at its first suspension, and its continuation can fail outside startup containment or terminate the process.

## Configuration, contracts and tests

Plugin directories, cache entry limits, cache expiration and output-schema mode are declared, bounded and projected into the services that consume them. The public API exposes no Host service provider, filesystem writer, transaction stager or Code Action composition surface. External handler request and response types are checked for effective public accessibility; construction eligibility is enforced by generic constraints and captured typed factories. Package discovery and dependency resolution use physical containment rather than lexical prefix checks.

Focused current-source evidence passed: 54 analyser tests, 107 Plugins unit tests, 86 Host plugin-loading/adapter/protocol tests and 31 package, load-context, metadata, MEF and Host-composition integration tests. The package integration test proves analyser delivery and a clean package-only build, but it does not build a transport with an unsupported JSON member. Host composition resolves all MCP tool services, but only from bundled contracts already known to be schema-compatible. Runtime fixtures do not suppress RWMCP003, use protocol-invalid names or include large native package DLLs, so the passing suites do not contradict the retained findings.

## Findings and limitations

Independent source validation retained RWMCP-020 through RWMCP-023 and rejected RWMCP-024. No P0 or P1 plugin-platform issue was substantiated; all four retained findings are P2, with RWMCP-023 retained at medium confidence because impact depends on package dependency size. No production or test code was modified. Acceptance, Code Action audit and external-repository scenarios were not run under repository policy. The configured plugin model is trusted in-process and non-collectible, so the review does not claim security or failure isolation against malicious plugin code. Roslyn MCP tooling was unavailable; local source inspection was used for symbol and call-site navigation, and Microsoft Learn plus the installed C# MCP SDK contract were used for serializer and tool-name compatibility evidence.
