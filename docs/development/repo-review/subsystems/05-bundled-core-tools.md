# Subsystem review: bundled core inspection and mutation tools

## Scope and relationships

This unit covers `Roslyn.Workbench.Mcp.Plugins.Core`: the bundled plugin catalogue, inspection/navigation/analysis tools, bounded response contracts, and the format/rename mutation handlers. It consumes the public Plugins API and Workspace services exposed through execution contexts.

## Implementation and boundary review

- `BundledCorePlugin` publishes the fixed first-party catalogue. Query handlers resolve selectors/snapshots through shared services and project stable bounded DTOs; mutation handlers return candidate solutions that still pass Workspace validation/staging.
- Navigation tools use Roslyn `SymbolFinder`, semantic models, syntax/operation trees and Workspace projection rather than parsing display strings. Result ordering and truncation are generally deterministic.
- Diagnostic and nullability tools use compiler diagnostics with cancellation. Reference/caller/callee operations operate on the acquired solution snapshot and use shared reference discovery where appropriate.
- Advisory async and disposable analysers currently use syntax-wide descendant scans instead of respecting nested executable/control-flow boundaries. Metrics line counting uses the Host newline rather than source text line structure.
- Format and rename stay inside the proposal pipeline; linked documents and filesystem constraints are rechecked by Workspace before staging.

## Consumers, DI and configuration

The Host loads this assembly as a bundled plugin and protects its names from external collision. Per-request result limits derive from `DefaultMaxResults` and request-specific bounded overrides. No bundled tool owns persistent mutable global state.

## Tests and findings

The core unit project has broad per-tool Roslyn-object coverage and passed 266 fast-loop tests. Its component integration project cannot currently initialise because of RWMCP-004. Validated production findings are RWMCP-002 (source/Host newline mismatch corrupts logical-line metrics), RWMCP-003 (async analysis crosses nested-function boundaries) and RWMCP-006 (any syntactic disposal call suppresses a lifetime finding without all-path proof).
