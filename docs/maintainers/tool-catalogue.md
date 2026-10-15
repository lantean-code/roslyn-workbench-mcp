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

## Offline definition quality checks

The `MCP tool definition quality` workflow runs deterministic [TDQS lint](https://tdqs.dev/cli) on pull requests and pushes to the maintained branches. It builds the Host once, then starts a separate stdio Host for every combination of the four operational modes (`inspection-only`, `transactional`, `approval-required` and `autonomous-trusted`) and output-schema settings (`Omit` and `Full`). Each catalogue comes from that process's complete, paginated MCP `tools/list` response. The documentation generator's combined reference catalogue is not used.

Captures use fresh temporary state directories, default SDK environment inheritance and no external plugins or loaded workspaces. Error-reporting consent is explicitly `prompt` so the conditional reporting tool definitions are included; capture only initialises the connection and lists tools, without invoking tools or submitting reports. `Omit` represents the default output-schema setting; missing output schemas in that profile are informational findings, not failures.

The reporting utility runs the pinned TDQS CLI with `--fail-on error`. Lint errors fail the check; warnings, including unconfirmed shadowing candidates, remain advisory. Capture failures, invalid input and CLI failures also fail the check. All remaining profiles are still attempted so one failure does not hide their results. No model, model key or hosted scoring service is used.

The GitHub job summary includes counts and expandable findings for each profile. The `tool-quality` artefact retains each captured `catalogue.json`, `lint.json`, `lint.md`, Host stderr log and any failure diagnostic, plus a combined `summary.md` and run metadata containing the Host assembly hash, TDQS package version and CI source revision. Reports identify the rubric version. Dependencies, including transitive dependencies, are pinned in `tools/tool-quality/requirements.txt`; update and validate those pins together.

To run locally with Python 3.12 or newer, create an isolated Python environment, install `tools/tool-quality/requirements.txt`, build the Host with the pinned .NET SDK, then run:

```text
python -m pip install --requirement tools/tool-quality/requirements.txt
dotnet build src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj
python -m unittest discover -s tools/tool-quality -p test_report.py
python tools/tool-quality/report.py --host src/Roslyn.Workbench.Mcp/bin/Debug/net10.0/Roslyn.Workbench.Mcp.dll --output artifacts/tool-quality
```

The local report remains under `artifacts/tool-quality`; exit code 0 means every profile passed the error threshold, 1 means lint errors were found, and 2 means at least one profile could not be captured or linted. The utility bounds capture and CLI execution times and retains diagnostics for failed profiles.

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
