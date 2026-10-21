# Navigate symbols and calls

Start from a known source location with `resolve-symbol`, or use `search-symbols` when only a name is known. Check the project and signature before following an overload. Reuse `resolve-symbol`'s `data.selector` for subsequent symbol queries; see [selectors.md](selectors.md) for binding locations and snapshots.

## Choose the query

| Need | Tool |
| --- | --- |
| Find source types or members | `search-symbols`; narrow by project, kind or namespace. |
| Bind a source occurrence to its symbol | `resolve-symbol`. |
| Inspect signature, modifiers, declarations or XML documentation | `get-symbol-info`. |
| Locate the declaration | `go-to-definition`; metadata results contain no decompiled source. |
| Read selected source and surrounding lines | `get-code-context`. |
| Find usages and access classifications | `find-references`. |
| Trace incoming calls grouped by caller | `find-callers`. |
| Trace outgoing method/constructor targets | `find-callees`. |
| Compare signatures in the same containing type | `find-overloads`. |

## Find the right declaration

`search-symbols` uses Roslyn name patterns, not regular expressions or full-text search. `metadataName` is a case-insensitive substring filter; `namespace` matches the complete namespace exactly. Locals and namespace declarations are excluded.

Read source with `get-code-context` after finding the symbol. Its optional diagnostics cover compiler findings intersecting the **selected span**, not the expanded context window or project analyzers.

`find-overloads` may include the selected callable. It neither resolves supplied arguments to an overload nor collects inherited overloads. For overrides and interface implementations, use [structure.md](structure.md).

## Trace usages and calls

Set `includeDefinitions: false` when counting usages with `find-references`; declarations otherwise share the result cap.

For `find-callers`, inspect both the callers collection and each caller's call-site truncation: either can hide relevant calls.

Start `find-callees` with direct calls; enable `includeIndirect` for transitive exploration. Results are distinct targets, not call sites or runtime dispatch targets. Each scanned body skips nested lambdas and local-function bodies. Traversal depth is independent of result limits, so an untruncated result can still omit deeper calls.
