# Maintaining compatibility

The [published compatibility policy](../content/compatibility.md) owns the user-facing 1.x guarantees. This guide records the implementation and validation mechanisms.

## SDK discovery and cold startup

The MCP SDK 2.4.0 [fixes the discovery fallback race](https://github.com/modelcontextprotocol/csharp-sdk/pull/1956) by preventing `server/discover` probes from committing the session's protocol version. A cold Host can therefore answer a late discovery probe and accept the legacy `initialize` fallback after the client's separate probe budget expires.

`McpColdStartProtocolIntegrationTests` holds the composed Host in its recovery startup prerequisite and uses `FakeTimeProvider` to expire the client's discovery budget before allowing the transport to start. A test relay holds the legacy fallback until a server message observer confirms discovery processing has begun, selecting the previously failing request order without relying on concurrent dispatch timing or synthesising responses. The default-client case now requires a successful legacy connection; the disabled-probe case requires a successful modern connection with private caching hints. Overall initialisation deadlines and caller cancellation remain enforced during recovery.

Acceptance and scenario clients connect to a known discovery-capable Host and retain `DiscoverProbeTimeout = Timeout.InfiniteTimeSpan` to prefer modern discovery during slow startup, alongside their existing overall initialisation deadlines and caller cancellation. This preference is no longer a workaround for the SDK 2.3.0 fallback failure. Host recovery and protocol validation remain intact.

## Bundled Skills extension

The Host intentionally consumes the experimental `ModelContextProtocol.Extensions.Skills` 2.4.0 APIs, with `MCPEXP005` suppressed only in the files using them. An SDK-authored package reads the embedded Markdown frontmatter and computes file digests and sizes from the UTF-8 text served by the existing resource handlers. Only its skill registration is added through `WithSkills`; the existing resource registrations retain their URIs, names, descriptions and text MIME types. Native `skills/list` and `skills/get` discovery supplements resource-only delivery in every operational mode without requiring a workspace or adding tools. Client metadata and logo assets remain in the separately installable bundle.

The protocol integration tests cover discovery, complete manifests, SDK-verified reads, private caching hints, continuation freshness and unknown-skill rejection on modern and legacy protocols. Published-Host acceptance tests verify discovery and every file's digest and size in all operational modes.

## Catalogue baselines

The repository records canonical `tools/list` baselines for both supported output-schema publication modes. Each baseline preserves the exact built-in catalogue published by inspection-only, transactional, approval-required, autonomous-trusted and compiler-validation configurations, including intentional mode-specific contracts such as receipt-based `transaction-commit`. Tools are ordered by name and JSON object properties are ordered recursively before comparison; array order is retained because arrays can carry contract meaning. The baselines cover each built-in tool's name, title, description, annotations, input schema and, when enabled, output schema. Externally installed plugin tools are deliberately excluded because their contracts belong to their publishers.

## Recovery format mappings

`Current` identifies the format written by a Host; a separate supported-format set identifies every format it can read. The Host probes the common version field first and dispatches supported evidence through immutable, version-specific persisted models and explicit mappings into the version-neutral runtime recovery model. Writing follows the reverse mapping into the current persisted model. Advancing `Current` therefore requires adding both mappings for the new format while retaining every earlier 1.x reader and keeping the V1 recovery fixtures green; a contract test fails when the current version and emitted version diverge.

## Plugin API evidence

The repository maintains shipped public-API analyser baselines for both assemblies and focused plugin contract tests. Once the first stable plugin package is published, release validation also compares later packages with that 1.0 package by using .NET package/API compatibility tooling. Passing that binary check complements, but does not replace, behavioural contract tests and review of semantic changes.

## Protocol response caching

The Host supplies private caching hints for its startup-fixed tool and resource catalogue pages and Skills list/get metadata (one minute) and successful reads in the embedded `skill://roslyn-workbench/` namespace (five minutes). Continuations carrying `inputResponses` or `requestState` are immediately stale. Resource failures and workspace-dependent tool results receive no positive freshness hint. The Host applies these hints only for protocol revisions that support caching; legacy responses retain their existing shape. These hints allow supporting clients to reuse responses; they do not add a server-side response cache or guarantee reduced model context usage. Clients must rediscover catalogues when reconnecting to a Host with different configuration or plugins.
