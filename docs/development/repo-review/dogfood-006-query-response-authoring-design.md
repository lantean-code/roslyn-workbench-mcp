# DOGFOOD-006 — Query-response authoring warnings

## Purpose

DOGFOOD-006 applies the existing query-response authoring rule to the bundled core plugin, fixes the response contracts it currently identifies and moves the runtime fallback warning out of agent-facing plugin load diagnostics into operator logging.

The analyser remains a warning. An unbounded collection does not prevent a plugin from loading or a tool from running, but it can place an uncontrolled amount of data into an agent's context.

## Discovery

`RWMCP014` already detects raw arrays, lists, sets, dictionaries and enumerable shapes published by query responses. The `Roslyn.Workbench.Mcp.Plugins` package includes the analyser assembly under `analyzers/dotnet/cs`, so normal third-party package consumers receive the rule. The bundled `Roslyn.Workbench.Mcp.Plugins.Core` project references the Plugins runtime assembly but does not reference the analyser project as an analyser, so its own contracts are not checked during compilation.

The Host also has a reflection-based `QueryResponseContractInspector`. It runs after tool materialisation and finds the same broad contract problem for plugins built without the current analyser or with the rule suppressed. Its warnings are currently appended to `PluginStatus.Diagnostics`, even though that property describes plugin discovery, loading, composition, schema and enablement state. Consequently, `server-status` reports an enabled plugin as having load diagnostics for a non-fatal authoring recommendation.

The current bundled core plugin has seven affected response DTOs:

| Tool | Raw top-level collections |
| --- | --- |
| `get-code-context` | `EnclosingSymbols`, `Diagnostics` |
| `resolve-symbol` | `Declarations` |
| `get-symbol-info` | `Modifiers`, `Parameters`, `Declarations` |
| `go-to-definition` | `Definitions` |
| `analyze-control-flow` | `Exits`, `Returns` |
| `analyze-data-flow` | `VariablesDeclared`, `ReadInside`, `WrittenInside`, `DataFlowsIn`, `DataFlowsOut`, `Captured` |
| `get-control-flow-graph` | `Blocks`, `Regions` |

`get-control-flow-graph` already limits both top-level collections and publishes separate truncation booleans. The other six tools currently project complete collections and expose no caller-controlled collection limits.

## Operating-model assessment

- **Actor:** a trusted plugin author or maintainer, and an authorised agent consuming an enabled query tool.
- **Action:** publish or invoke a query whose response contains one or more result collections.
- **Plausibility:** every listed bundled tool already exposes this shape, and third-party plugins can be compiled with the analyser suppressed or absent.
- **Existing control:** `RWMCP014` warns package consumers and the Host reflection fallback detects the broad runtime shape, but the bundled project does not run the analyser and the fallback is routed to plugin load diagnostics.
- **Impact:** first-party violations can be introduced without build feedback; a large result can unnecessarily consume agent context; and an operator-only authoring warning appears to agents as plugin health information even though the plugin remains enabled.
- **Decision:** activate the existing warning for the bundled build, remediate the known contracts and emit fallback findings once through structured operator logging without changing plugin enablement.

## Build-time analyser wiring

Add a direct project reference from `Roslyn.Workbench.Mcp.Plugins.Core` to `Roslyn.Workbench.Mcp.Plugins.Analyzers` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`. This makes the analyser participate in the bundled plugin compilation without adding its assembly as a runtime dependency. Scope that project to `RWMCP014`: bundled contracts are intentionally internal and therefore must not activate external-plugin rules such as `RWMCP008`, while package consumers continue to receive the complete analyser rule set.

Do not change the descriptor severity, promote `RWMCP014` through `WarningsAsErrors`, or add a project-specific severity override. Its existing warning severity accurately reflects an authoring and context-efficiency problem rather than an invalid executable contract.

The existing analyser unit tests continue to own detection semantics, and the existing package integration test continues to own third-party package activation. The direct Core analyser reference is validated by building the remediated Core project with no `RWMCP014` warnings; no source fixture or acceptance asset is required solely to test MSBuild item metadata.

## Runtime fallback and logging

Retain `QueryResponseContractInspector`, including its exclusions for mutation tools and the Host-owned `list-code-actions` query. Simplify its result to one optional authoring-warning message because one tool currently produces either no finding or one message containing every offending top-level property.

Make `PluginCatalogEntryMaterializer` a source-generated logging owner by injecting `ILogger<PluginCatalogEntryMaterializer>`. After a plugin's tools are materialised, inspect each registered tool once and emit one warning event for each offending tool. Honour property-level `UnconditionalSuppressMessage` metadata for `RWMCP014` so a documented, genuinely fixed collection remains an ordinary list without producing a runtime fallback warning. The structured log event contains:

- the stable rule identifier `RWMCP014`;
- `PluginId`;
- `ToolName`; and
- the actionable warning message.

The materializer is invoked once for each prepared plugin during the Host's single startup catalogue load, so this emits each fallback finding once per Host process. The warning does not disable the plugin and is not copied into `PluginStatus.Diagnostics`.

Continue to place genuine materialisation diagnostics returned by `IPluginToolRegistrationMaterializer` into the enabled plugin status. Continue to place discovery, metadata, loading, composition, schema-preflight, collision and unexpected materialisation failures into plugin status through their existing paths. DOGFOOD-006 changes only the response-authoring fallback channel.

Use `LoggerMessageAttribute` with a stable event ID and structured Pascal-case placeholders. [Microsoft Learn's high-performance logging guidance](https://learn.microsoft.com/dotnet/core/extensions/logging/high-performance-logging#define-logger-messages-with-source-generation) identifies source-generated `LoggerMessage` methods as the current compile-time logging pattern and notes that placeholders become structured log properties.

## Bundled contract remediation

Each independently useful result collection becomes `BoundedCollection<TItem>`. Each new caller-controlled bound is a nullable integer, publishes a positive default with `[DefaultValue]`, accepts explicit zero through `[Range(0, int.MaxValue)]`, and resolves through `ResultLimit.GetEffectiveValue`. The tool applies the effective limit before avoidable per-item projection and returns a deterministic prefix.

| Tool | Request change | Response change and count semantics |
| --- | --- | --- |
| `get-code-context` | Add `enclosingSymbolsLimit?: int = 16` and `diagnosticsLimit?: int = 50`. | Return bounded enclosing symbols and diagnostics. Complete candidate counts are cheap after following the containing-symbol chain or obtaining and de-duplicating the selected-span diagnostics, so publish `TotalCount`. An excluded optional branch returns an empty complete collection. |
| `resolve-symbol` | Add `declarationsLimit?: int = 32`, matching `get-partial-declarations`. | Return bounded, path-and-span ordered declarations. Project only the bounded prefix; use `HasMore` without forcing projections solely to calculate a total when locations cannot be represented. |
| `get-symbol-info` | Add `parametersLimit?: int = 64` and `declarationsLimit?: int = 32`. | Return bounded parameters and declarations. Retain modifiers as an ordinary list because Roslyn exposes a fixed, small language-defined set; suppress `RWMCP014` on that property with the justification recorded in source. Parameters use Roslyn's cheap parameter count. Declarations follow the same projection and count rule as `resolve-symbol`; `Parameters` remains null for non-callable symbols. |
| `go-to-definition` | Add `definitionsLimit?: int = 32`. | Return bounded, path-and-span ordered definitions. Preserve the existing single metadata-definition fallback. Use `HasMore` when source-location projection prevents a cheap authoritative total. |
| `analyze-control-flow` | Add `exitsLimit?: int = 100` and `returnsLimit?: int = 100`. | Return bounded exits and returns. Roslyn already supplies complete exit and return candidate collections, so publish their total counts while projecting only the requested prefix. |
| `analyze-data-flow` | Add `symbolsPerCategoryLimit?: int = 50`. | Apply the named limit independently to each of the six bounded symbol categories. One explicitly named per-category limit avoids six repetitive request properties while preventing one category from consuming another category's allowance. Roslyn supplies each complete category count cheaply, so publish `TotalCount` for every category. |
| `get-control-flow-graph` | Retain nullable `maxBlocks`, `maxRegions` and `maxOperationsPerBlock` with their current defaults, resolving each through its corresponding `Effective*` property. | Change `Blocks` and `Regions` to bounded collections. Remove the redundant `BlocksTruncated` and `RegionsTruncated` properties; their exact meaning moves to `Blocks.HasMore` and `Regions.HasMore`. Publish the cheap complete block count. Preserve `hasMore`-only region reporting when recursive traversal stops at the requested limit rather than traversing the rest of the graph merely to count it. |

The per-category data-flow limit is not a shared total budget. A shared budget would make the result depend on category evaluation order and could starve later categories; the proposed name states that the same independent cap applies to every category.

The control-flow graph request names remain unchanged because they already publish explicit bounds and defaults. Their nullable values now use the same `Effective*` pattern as other defaulted limits so omitted and explicit zero values retain distinct request semantics. Removing its separate truncation booleans avoids two competing representations of the same state; this is an intentional response-contract replacement within the DOGFOOD remediation rather than an additive alias.

## Readability and implementation shape

Keep request default constants beside their request properties and expose one clearly named effective-value property per new request member. Use small projection helpers where a tool has more than one collection. Helpers receive the effective limit explicitly and return the completed `BoundedCollection<TItem>` rather than returning an intermediate list plus separate truncation state.

Use ordinary loops when they make early bounding, skipped projections and `HasMore` handling explicit. Do not add dense LINQ pipelines that enumerate a complete collection and then obscure where the bound is applied. Existing ordering rules remain unchanged: source locations sort by path and span, while Roslyn-defined semantic order is retained where the current tool already relies on it.

## Test design

Add or update focused non-acceptance coverage:

- Each affected request test verifies omitted limits resolve to the declared defaults and explicit zero remains zero.
- Each affected tool test reaches the normal `ExecuteAsync` path with more candidates than its bound and verifies the deterministic prefix, `HasMore`, and `TotalCount` or intentional absence of `TotalCount`.
- Optional branches verify that disabled code-context collections remain empty and non-callable symbol information retains `Parameters = null`.
- `analyze-data-flow` verifies the per-category limit is applied independently rather than as a shared budget.
- `get-control-flow-graph` verifies complete and truncated block and region results through the bounded shape, including the existing per-block operation bound.
- `QueryResponseContractInspectorTests` retain coverage for excluded, bounded, explicitly suppressed fixed and representative raw response shapes using the simplified warning result.
- `PluginCatalogEntryMaterializerTests` verify that a runtime fallback finding produces exactly one warning log with plugin and tool identity, does not appear in `PluginStatus.Diagnostics`, does not disable the plugin, and does not remove genuine materialisation diagnostics.

No acceptance-test source or fixture changes are proposed. The existing analyser tests already prove `RWMCP014` semantics, and the runtime routing and collection behaviour are reachable through unit tests. Published dogfood validation supplies the executable-boundary evidence after the reviewed change is committed.

## Documentation changes

Update `RoslynMcpToolContracts.md` for the new request limits and bounded response shapes. Retain the existing general plugin-authoring guidance and `RWMCP014` documentation because they already describe nullable limits, explicit zero, early bounding and `BoundedCollection<TItem>` correctly.

After published validation, update the DOGFOOD worklist and usage ledger with the absence of response-authoring diagnostics from `server-status` and representative calls showing the new bounded wire shapes.

## Validation plan

After implementation:

1. Format only the changed C# source and test files.
2. Build the solution normally with the WSL artefacts path.
3. Build the affected Core, Host and test projects with `AnalysisLevel=latest-all` and code-style enforcement, retaining `RWMCP014` as a warning.
4. Run the Plugins.Core unit-test project and the Host unit-test project using the repository's preferred non-acceptance test command.
5. Verify the Core build reports no `RWMCP014` warning and that the Host tests prove runtime warnings are logged rather than returned through plugin status.
6. Verify every changed CRLF-governed file uses CRLF and both unstaged and staged diffs pass `git diff --check` at their respective process gates.

After the reviewed change is committed, publish that exact `HEAD` to a new dogfood candidate. Restart Codex, confirm `server-status` no longer contains `QueryResponseContract` plugin diagnostics, invoke representative affected tools with deliberately low limits, and verify their `items`, `hasMore` and optional `totalCount` shapes through the published setup.

## Approval and scope gates

The user approved this design before implementation began.

Implementation scope is limited to:

- enabling the existing analyser for the bundled Core build without changing its severity;
- routing runtime query-response authoring findings to one structured operator warning per plugin tool;
- bounding the seven identified bundled response contracts and adding their named request limits;
- focused Core and Host unit tests;
- the affected tool-contract, design, usage and final worklist documentation; and
- published dogfood validation after the independently reviewed change is committed.

Do not change plugin admission, plugin enablement, global response byte handling, nested collections not identified by `RWMCP014`, unrelated analyser rules, the MCP SDK version, acceptance-test assets or DOGFOOD-007 scope behaviour as part of DOGFOOD-006.
