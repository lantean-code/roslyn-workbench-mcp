# Tool catalogue publication

The [user-facing discovery guide](../content/tool-discovery.md) explains how clients discover and interpret tools. This document records composition details and curated defaults.

## Composition

Use MCP `tools/list` to discover the exact tools published by the running process. The surface is assembled once at startup from:

- server-owned status, workspace and transaction tools;
- bundled Roslyn inspection and mutation tools;
- the Host-owned `list-code-actions`, `prepare-fix-all` and `stage-code-action` tools; and
- `get-error-details`, plus the conditionally published `prepare-error-report` and `submit-error-report` tools; and
- enabled third-party plugins.

`get-error-details` is always published for local correlated failure inspection. The two external-reporting tools are published whenever startup consent is not `never`; the Host supplies the provider and destination as application configuration. Their names remain reserved against plugin collisions when omitted under `never`. Per-report prompt outcomes do not change `tools/list` or establish runtime overrides.

The startup operational mode also fixes the catalogue. `inspection-only` omits all five Host transaction tools, bundled plugin mutations and Code Action staging while retaining query operations such as Code Action discovery and preparation. External plugin loading is independently disabled by default and requires the valueless `--enable-plugins` switch; when enabled under inspection-only policy, external query tools are published but external mutation definitions are removed before schema preflight, handler resolution or runtime-wrapper materialisation. A plugin with no permitted tools creates no service provider. `transactional` and `autonomous-trusted` publish the supported mutation catalogue; transactional commit additionally requires client-mediated confirmation. Omitted Host names remain reserved against plugin collisions. The live `tools/list` response is authoritative, and a policy-omitted tool cannot be invoked through the dynamic plugin handler.

## Curated result defaults

Bundled collection defaults are curated by result shape and expected cost:

- 16 results: base types;
- 25 results: dependency cycles and duplicate-code groups;
- 32 results: partial declarations and control-flow regions;
- 50 results: async, disposable, nullability and unused-symbol findings, overloads, attributes, project references and analyzers;
- 64 results: implemented interfaces and control-flow blocks;
- 100 results: symbol searches, API symbols, metrics, callers, callees, references, implementations, overrides, derived types, hierarchy-derived types, dependencies, dependents, members, change-impact locations, tests, projects and metadata references;
- 200 results: diagnostics, project documents, solution folders and dependency graph nodes; and
- 400 results: dependency graph edges.

Other bounded numeric inputs also publish their effective defaults: hierarchy, derived-type and callee depth is 3; operation-tree depth is 8; duplicate-code matching requires at least 3 statements; code-context windows are 10 lines on each side; transaction diff context is 3 lines; Code Action discovery returns at most 50 leaves; Fix All preparation allows at most 50 changed source documents and returns at most 20 affected document identities unless the caller supplies other values.
