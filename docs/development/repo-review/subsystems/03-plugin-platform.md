# Unit 3 — Plugin platform

**Status:** Completed with no substantiated candidate. No earlier unit reopened.

## Implementation evidence

- The authoring package contains matching Plugins and Abstractions assemblies, the analyser and guide; Workspace remains private.
- Discovery physically contains immediate package directories/assemblies, inspects PE metadata before execution, requires one marker and validates identity/version/API before load-context creation.
- Each external package uses a non-collectible package load context. Public contract/Roslyn/Composition identities are shared; private managed/native dependencies use `AssemblyDependencyResolver` with containment rechecks.
- MEF composes exactly one plugin. Synchronous configuration is frozen, and handler/lifetime/import/metadata/name/behaviour/dependency/transport diagnostics precede atomic admission.
- Collision policy is deterministic. Schema preflight runs even when output schemas are omitted; query responses must serialize as objects before provider construction.
- Each enabled plugin receives an isolated validated singleton provider. Services/handlers and closed typed adapters are resolved once; execution uses no reflection.
- Query/mutation adapters acquire snapshot-bound leases. Plugin caches include exact snapshot, plugin and tool identity and expire with invocation scope. Mutation handlers cannot stage directly; Host verifies live Workspace integrity and invokes common staging.
- Catalogue publication is single-assignment. Startup failure cleans provisional providers; catalogue disposal is reverse-order, asynchronous and best-effort across failures.
- Bundled core follows the same preparation/materialisation path as external plugins.

## Test and executable evidence

Plugins, analyser, Host loading/adapters and Host integration/package-consumer suites claim the package, API, discovery, identity, MEF, schema, DI, cache, execution and disposal boundaries. Published acceptance additionally claims process/package execution and failure isolation.

The reviewer ran Plugins 147/147, analyser 70/70, focused Host 122/122 and focused Host integration 33/33 successfully. The integration run exercised package-only restore/build/analyser activation, discovery, metadata, load contexts, MEF and plugin protocol boundaries. No acceptance rerun was needed for this unchanged baseline.

## Limits and follow-up

Native-library resolution and Windows reparse/path behaviour remain Unit 8 evidence. Arbitrary trusted plugin behaviour is an explicit boundary. `RWMCP3-005` reaches plugins only through common Workspace staging; Unit 3 adds no bypass, so ownership remains Unit 2.

## Exported assumptions

Startup-only discovery and trusted code are intentional. Shared identities must match Host. Handlers/services are thread-safe singletons. Host staging is the sole mutation boundary, and candidate graph changes are not automatically persistable. Private dependencies require sufficient dependency metadata.

**Candidates:** None.

**Reopenings:** None. Units 4 and 6 consume typed publication assumptions; Unit 8 assesses native/Windows evidence.
