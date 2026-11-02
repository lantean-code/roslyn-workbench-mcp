# Maintaining compatibility

The [published compatibility policy](../content/compatibility.md) owns the user-facing 1.x guarantees. This guide records the implementation and validation mechanisms.

## SDK discovery and cold startup

The MCP SDK 2.3.0 has a [known discovery fallback race](https://github.com/modelcontextprotocol/csharp-sdk/pull/1943): a cold Host can exceed the separate five-second discovery-probe budget, causing the client to attempt a legacy handshake after selecting the modern protocol. Acceptance and scenario clients connect to a known discovery-capable Host, so they disable the separate probe timeout with `DiscoverProbeTimeout = Timeout.InfiniteTimeSpan` while retaining their existing overall initialisation deadlines and caller cancellation. This client configuration does not fix the upstream fallback race or change external clients; legacy and modern protocol handling remain covered separately by integration tests.

`McpColdStartProtocolIntegrationTests` holds the composed Host in its recovery startup prerequisite and uses `FakeTimeProvider` to expire the client's discovery budget before allowing the transport to start. The default-client case characterises the SDK 2.3.0 failure: the queued discovery selects the modern protocol, then the queued `initialize` fallback is rejected with `MethodNotFound`. This expected-failure assertion must be revisited when adopting an SDK fix. The configured-client cases prove delayed modern discovery succeeds with the published catalogue and caching hints intact, while the overall deadline and caller cancellation still apply during recovery. These tests use the real Host startup pipeline and SDK transports rather than a scripted server response.

The SDK checks this protocol boundary before invoking Host message or request filters; there is no supported Host timeout setting that extends an external client's discovery probe. Do not bypass recovery or catalogue publication to meet the probe deadline, or rewrite protocol metadata to accept a mid-session downgrade. Removing the external-client risk requires an SDK negotiation fix; reducing startup time can only reduce its frequency.

## Catalogue baselines

The repository records canonical `tools/list` baselines for both supported output-schema publication modes. Each baseline preserves the exact built-in catalogue published by inspection-only, transactional, approval-required, autonomous-trusted and compiler-validation configurations, including intentional mode-specific contracts such as receipt-based `transaction-commit`. Tools are ordered by name and JSON object properties are ordered recursively before comparison; array order is retained because arrays can carry contract meaning. The baselines cover each built-in tool's name, title, description, annotations, input schema and, when enabled, output schema. Externally installed plugin tools are deliberately excluded because their contracts belong to their publishers.

## Recovery format mappings

`Current` identifies the format written by a Host; a separate supported-format set identifies every format it can read. The Host probes the common version field first and dispatches supported evidence through immutable, version-specific persisted models and explicit mappings into the version-neutral runtime recovery model. Writing follows the reverse mapping into the current persisted model. Advancing `Current` therefore requires adding both mappings for the new format while retaining every earlier 1.x reader and keeping the V1 recovery fixtures green; a contract test fails when the current version and emitted version diverge.

## Plugin API evidence

The repository maintains shipped public-API analyser baselines for both assemblies and focused plugin contract tests. Once the first stable plugin package is published, release validation also compares later packages with that 1.0 package by using .NET package/API compatibility tooling. Passing that binary check complements, but does not replace, behavioural contract tests and review of semantic changes.

## Protocol response caching

The Host supplies private caching hints for its startup-fixed tool and resource catalogue pages (one minute) and successful reads in the embedded `skill://roslyn-workbench/` namespace (five minutes). Continuations carrying `inputResponses` or `requestState` are immediately stale. Resource failures and workspace-dependent tool results receive no positive freshness hint. The Host applies these hints only for protocol revisions that support caching; legacy responses retain their existing shape. These hints allow supporting clients to reuse responses; they do not add a server-side response cache or guarantee reduced model context usage. Clients must rediscover catalogues when reconnecting to a Host with different configuration or plugins.
