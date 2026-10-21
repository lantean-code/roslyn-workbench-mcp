# Inspect structure, configuration and API shape

## Find the relevant project and document

Start with `get-solution-structure` for project identities, target frameworks and references. Drill into one project with `get-project-details` for compilation options, references and analyzers. Request `includeDocuments` when document identities are needed; it returns no file text.

Use project identity and target framework to disambiguate multi-target projects or linked files. `get-document-options` explains effective language, preprocessor and analyzer configuration. Its nullable setting is project-level; source `#nullable` directives can override it.

Use `get-document-outline` to find declarations in a file, then `get-code-context` to read them. A truncated outline can omit declarations or deeper members; an unavailable syntax/semantic model can produce a null root.

## Choose a symbol relationship

| Need | Tool |
| --- | --- |
| Declared members, including non-public members | `get-symbol-members`. |
| Applied or inherited attribute metadata | `get-symbol-attributes`. |
| Interface implementations | `find-implementations`. |
| Overriding methods, properties or events | `find-overrides`. |
| Scoped descendants of a type or interface | `find-derived-types`. |
| Ancestors and descendants together | `get-type-hierarchy`. |
| Source parts of one resolved symbol | `get-partial-declarations`. |
| Accessible API inventory | `get-api-surface`, using the intended scope and `minimumAccessibility`. |

## Interpret inclusion correctly

- `get-symbol-members`: `includeInherited` excludes private, implicit, constructor and destructor members. **`includeExplicitInterface` adds declarations from all implemented/inherited interfaces**, not just explicit implementations on the selected type.
- `get-symbol-attributes`: inherited traversal follows base types and overridden methods/properties/events, respecting `Inherited` and `AllowMultiple`; it does not traverse interfaces.
- `find-implementations`, `find-overrides` and `find-derived-types`: document scope expands to the containing project. For interface targets, derived types include derived interfaces and implementing types. Depth limits can exclude descendants independently of collection limits.
- `get-type-hierarchy`: descendant search covers the loaded solution. Use `find-derived-types` when the search must be scoped.
- `get-partial-declarations`: partial-method definition and implementation can be separate symbols; resolve each when both parts are needed.

Follow API entries with `get-symbol-info`; use [impact-analysis.md](impact-analysis.md) to inspect dependencies before changing them.
