# DOGFOOD-003 — Concise server instructions

## Purpose

DOGFOOD-003 keeps the essential server-wide guidance within Codex's documented first 512 characters while retaining the minimum information an agent needs before following the version-specific Agent Guide.

The current initialisation instructions contain approximately 967 characters and 138 words. The Git commit distinction and version-specific Agent Guide link occur after the first 512 characters, so the current text does not provide a self-contained decision window for Codex.

## Discovery

`RoslynWorkbenchMcpServerOptionsConfiguration.CreateInstructions` is the only production construction point. It derives a source tag from assembly metadata, builds the version-specific Agent Guide URL and assigns the resulting text to `McpServerOptions.ServerInstructions`. Dependency injection registers that configurator once; no other production code rewrites or appends to the instructions.

The installed ModelContextProtocol 1.4.1 API documentation describes `ServerInstructions` as initialisation guidance that should help models use the server effectively without duplicating tool, prompt or resource descriptions. OpenAI's [Codex MCP documentation](https://developers.openai.com/codex/mcp/#supported-mcp-features) confirms that Codex uses this field as server-wide guidance alongside the server's tools. It recommends cross-tool workflows and constraints here and requires the first 512 characters to be self-contained. This is intentional Codex behaviour rather than an MCP 2 statelessness consequence.

Codex source analysis supplied during design discovery indicates that Codex copies the instructions into each internal `ToolInfo` as namespace metadata so independently handled and lazily discovered tools retain the server guidance. Before direct tools are sent to the model, `merge_into_namespaces()` coalesces tools from the same MCP server into one namespace with one description; deferred search results are similarly coalesced. The copies are therefore an internal representation and do not demonstrate repeated instructions in normal model context. Whether the backend later flattens a namespace is not observable from the open-source client, so DOGFOOD-003 does not claim a model-context saving.

The linked `docs/AgentGuide.md` already contains the detail proposed for removal:

- complete snapshot and structured-next-action handling, including stale selectors and locations;
- transaction isolation, query placement and unexpectedly broad previews;
- user coordination during commit application;
- reload, retry and recovery behaviour; and
- the distinction between Workbench source writes, validation and a Git commit.

Current automated coverage reaches both relevant boundaries:

- `HostToolCompositionIntegrationTests` resolves the configured `McpServerOptions` through the complete Host container; and
- `PublishedHostProtocolIntegrationTests` reads the instructions returned by a published Host during the MCP initialisation handshake.

No public contract, tool schema, request handler, Agent Guide content or product operating-model boundary needs to change.

## Operating-model assessment

- **Actor:** the authorised MCP client and agent using the published Host.
- **Action:** initialise the server and use its tools through Codex, which relies on the first 512 characters as self-contained server guidance.
- **Plausibility:** this occurs on every ordinary Codex connection; it is not a theoretical concurrency or hostile-input scenario.
- **Existing control:** the version-specific Agent Guide provides the detailed workflow guidance, but its link and the Git commit distinction currently fall after the documented self-contained window.
- **Impact:** an agent may make its initial tool-use decision without the complete essential guidance or the route to detailed instructions. This is an operational clarity risk rather than a runtime correctness or security failure.
- **Decision:** retain the essential trust and mutation guidance inline, remove duplicated detail and protect the concise boundary with semantic assertions plus a size budget.

## Proposed instructions

Replace only the instruction body with:

```text
Open only fully trusted C# workspaces; build logic and analysers run unsandboxed with Host permissions.

Prefer queries before mutations. Start transactions only when ready; keep each to one coherent change or tightly related set, inspect transaction-preview, then call transaction-commit or transaction-rollback promptly.

transaction-commit writes source files but does not create a Git commit.
Guide: <version-specific Agent Guide URL>
```

With the current `v1.0.0` source tag, this is 497 characters and 55 words: 470 fewer characters (48.6%) and 83 fewer words (60.1%). All essential guidance ends within the first 396 characters, and the complete current versioned URL remains inside the documented 512-character decision window.

## Retained guidance

The concise text retains every requirement approved in the DOGFOOD worklist:

- only fully trusted C# workspaces may be opened;
- project build logic and analysers execute unsandboxed with the Host's permissions;
- queries should precede mutations;
- a transaction starts only when the agent is ready, contains one coherent change or tightly related set, receives preview inspection and is committed or rolled back promptly;
- `transaction-commit` writes source files but does not create a Git commit; and
- the Agent Guide URL remains derived from the Host source tag.

## Removed detail

Remove the inline statements about unrelated queries during transactions, broad solution-wide mutations, workspace epochs, transaction revisions, structured next actions, stale spans and retry behaviour that are already covered by the linked Agent Guide. These rules remain in the version-matched Agent Guide and in structured continuations returned by the Host.

This does not weaken runtime enforcement. Snapshot validation, transaction admission, mutation ownership, stale detection, reload requirements and commit coordination are unchanged.

## Alternatives considered

### Retain the current instructions

This avoids wording changes but leaves essential guidance and the Agent Guide link outside Codex's documented self-contained window. It does not address DOGFOOD-003.

### Publish only the Agent Guide URL

This produces the smallest prefix but removes the trust warning and the minimum safe mutation sequence before an agent elects to read the guide. The worklist explicitly requires those safeguards to remain inline.

### Generate per-tool instruction variants

This could tailor guidance by tool family but would add catalogue-generation complexity and duplicate responsibilities already expressed by individual tool descriptions and schemas. DOGFOOD-003 concerns cross-cutting initialisation guidance, so one concise server-level message remains the correct boundary.

## Test design

Update the existing Host composition integration assertion and published-Host acceptance assertion. Each boundary should verify semantic fragments rather than the full instruction string:

- fully trusted Workspace and unsandboxed execution guidance;
- query-first guidance;
- one coherent change or tightly related set;
- `transaction-preview` followed by prompt `transaction-commit` or `transaction-rollback`;
- the Git commit distinction; and
- the exact version-specific Agent Guide URL.

Assert that the `v1.0.0` test instructions do not exceed 512 characters. This protects Codex's documented decision window without fixing the complete wording or punctuation. Do not add absence assertions for every removed sentence. The semantic assertions ensure the critical guidance remains self-contained even if a future source tag makes the complete URL longer than the current test value.

No new test class or production seam is required. The existing integration and acceptance flows already exercise configuration, source-tag interpolation and the real initialisation protocol.

## Validation plan

After implementation:

1. Format the production, integration-test and acceptance-test files only.
2. Build the solution normally with the WSL artefacts path.
3. Run `latest-all` analyser builds for the Host, Host integration-test and acceptance-test projects.
4. Run the affected Host integration-test project.
5. Because an acceptance-test asset changes, run the complete acceptance suite through `test/Roslyn.Workbench.Mcp.AcceptanceTest/run-acceptance-tests.sh` without filtering.
6. Verify changed CRLF-governed files use CRLF and both staged and unstaged diffs pass `git diff --check` at their respective process gates.

After the independently reviewed change is committed, prepare a new dogfood candidate from that exact commit, smoke-test it and promote it only after the user requests the restart. Validate that the raw initialisation instructions are complete within 512 characters and confirm that Codex exposes the retained server-wide guidance through the tool namespace.

## Approval and scope gates

This document is a proposal only. Do not change production or test code until the user explicitly approves it.

Implementation scope is limited to:

- `RoslynWorkbenchMcpServerOptionsConfiguration.CreateInstructions`;
- the existing Host composition integration assertions;
- the existing published-Host initialisation assertions; and
- DOGFOOD-003 design, usage and final worklist documentation required by the remediation process.

Do not change the Agent Guide, MCP SDK version, tool descriptions, transaction behaviour, stale-state behaviour, schemas or selector contracts as part of DOGFOOD-003.
