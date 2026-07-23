# Repository Readability Audit

Date: 2026-07-23

## Purpose

This audit extends the completed tool readability pass across all production and test code. It identifies code that does not follow the general readability rules in `src/AGENTS.md` and `test/AGENTS.md`, without treating syntax density alone as proof that an implementation should change.

The audit is a cleanup inventory, not a performance analysis. Any proposed change to a hot path must remain separate and must be supported by performance evidence.

## Method

All C# files under `src` and `test` were parsed with Roslyn. Generated output and `bin` or `obj` content were excluded. The syntax pass reported:

- consecutive statements where the first statement spans more than one line and the second starts on the immediately following line;
- conditional expressions where both alternatives invoke methods, construct objects, await work, assign values or contain another conditional;
- null-coalescing expressions where both operands perform work;
- invocation chains containing at least three common LINQ operators; and
- object construction inside return wrappers or other object construction.

The results were then sampled and classified against the source rules. The counts below are candidate counts except for statement separation, which is an exact syntax-level violation of the explicit blank-line rule.

This distinction matters:

- a three-stage `Where`/`Select`/`ToArray` pipeline can remain the clearest implementation;
- `value ?? throw ...` is a concise invariant check, not automatically a complex coalescing expression;
- a test object constructed with one nested dependency is not automatically difficult to read; and
- nested JSON schema construction and result construction deserve manual review because their readability depends on the surrounding method.

## Inventory

| Area | Missing separation | Complex conditional | Complex coalescing | Dense LINQ | Nested return construction | Nested object construction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Host | 27 | 6 | 4 | 5 | 7 | 42 |
| CodeActions | 69 | 1 | 1 | 0 | 2 | 8 |
| Plugins | 11 | 11 | 0 | 5 | 3 | 4 |
| Plugins.Core | 0 | 3 | 0 | 24 | 9 | 5 |
| Workspace | 61 | 14 | 11 | 10 | 14 | 17 |
| Production total | 168 | 35 | 16 | 44 | 35 | 76 |
| Acceptance and shared integration infrastructure | 45 | 0 | 0 | 2 | 0 | 16 |
| Host tests | 245 | 1 | 6 | 9 | 2 | 121 |
| CodeActions tests | 251 | 2 | 3 | 12 | 16 | 223 |
| Plugins tests | 688 | 6 | 4 | 8 | 19 | 292 |
| Workspace tests | 290 | 27 | 2 | 0 | 4 | 171 |
| Shared test support and fixtures | 12 | 0 | 9 | 3 | 8 | 7 |
| Test total | 1,531 | 36 | 24 | 34 | 49 | 830 |
| Repository total | 1,699 | 71 | 40 | 78 | 84 | 906 |

`TestFixtures` files that are compiled as scenario inputs account for six nested-return candidates and are included in the shared-fixture total. The nested-object category is retained as a review signal and must not be used as a mechanical replacement count.

## Confirmed findings

### Statement separation

The missing blank-line findings are genuine rule violations. Production has 168 and tests have 1,531. The concentration is uneven:

- `BundledCodeActionToolRegistrar` accounts for 42 production findings because adjacent multiline registrations have no separation;
- Plugins.Core production has no findings because its earlier tool pass already applied the rule;
- Plugins.Core unit tests account for 630 findings; and
- Workspace, CodeActions and Host unit tests account for a further 700 findings.

These changes are mechanical, but their volume warrants batches so that review is not obscured by whitespace.

### Conditional and null-coalescing expressions

The conditional scan found real instances of work on both alternatives in production and tests. Representative production examples include:

- choosing between two Roslyn `GetOperation` calls in `DependencyAnalysisService`;
- constructing different resolution results on both branches throughout `ToolRequestResolver`;
- awaiting either `OpenProjectAsync` or `OpenSolutionAsync` in `WorkspaceLoader`; and
- serialising different result shapes on both branches in `McpPublishedResultSerializer`.

These should use a named local or ordinary branching.

Most coalescing candidates are acceptable `value ?? throw ...` invariant checks. The confirmed complex cases are the chained validation calls in Workspace, declared-symbol versus symbol-info resolution, reflection-name fallback, schema-name fallback and the repository-root search in test infrastructure. They should be separated so evaluation order and fallback work are explicit.

### Dense LINQ

The scan found 44 production and 34 test candidates. It also demonstrated why a numeric chain threshold must not become an automated style rule:

- simple ordering followed by `ToArray` is often clearer as LINQ;
- a `StringBuilder.Append` chain was a semantic false positive because the syntax-only detector cannot prove that a method is LINQ; and
- several Plugins.Core findings are deterministic ordering pipelines already reviewed during performance work.

The strongest production candidates combine several responsibilities:

- `PluginCollisionPolicy` combines projection, filtering, grouping, duplicate detection and set construction;
- `WorkspaceProjectInputResolver` combines MSBuild import projection, path validation, normalisation, distinctness and materialisation;
- `WorkspaceLoadWorkflow` searches projects and documents through a nested pipeline;
- `WorkspaceCommitPlanner` builds its baseline path set through six stages;
- `InspectionProjectionFactory` combines conditional construction, repeated option lookup, filtering, projection, ordering and result construction; and
- several diagnostic and symbol tools mix filtering, projection, ordering and limiting in one expression.

Each project batch must retain short, single-purpose pipelines and simplify only the candidates where named stages or loops make control flow easier to verify. Plugins.Core tool changes must remain separate because implementation choices there may affect measured performance.

### Nested construction

Simple nested construction is common in test setup and contract projection and is usually clear. It must not be expanded mechanically.

Manual review is warranted where:

- a result is constructed directly inside one or more result factories;
- a large object initializer contains additional collection pipelines or object graphs;
- the JSON schema builders construct several nested `JsonObject` and `JsonArray` values in one return expression; or
- a switch expression both maps an outcome and constructs its complete response graph.

The audit therefore treats the 84 nested-return findings as focused review candidates and the broader nested-object count only as supporting evidence.

## Delivery plan

### Batch 1 — Production statement separation

**Status:** Complete

Insert the 168 missing blank lines in Host, CodeActions, Plugins and Workspace. This is one low-risk mechanical batch. Plugins.Core requires no production changes for this rule.

Validation: format the changed files, run affected-project analyzer builds, build the solution and run the fast unit and contract suite. No production behaviour change is expected.

Completed evidence:

- inserted 168 separators across 57 production files;
- a repeat Roslyn syntax scan found zero remaining production violations of the statement-separation rule;
- the solution and all four affected projects built with zero analyzer warnings; and
- the fast unit and contract suite passed 1,886 tests.

### Batch 2 — CodeActions and Plugins expression cleanup

**Status:** Complete

Review and simplify the conditional, coalescing, nested-return and dense-LINQ candidates in CodeActions and Plugins. These projects have relatively small inventories and no transaction or protocol persistence risk.

Validation: focused unit tests for both projects, affected-project analyzer builds and the fast suite.

Completed evidence:

- replaced all 12 complex conditional candidates with explicit branches or responsibility-focused helpers;
- replaced the CodeActions coalescing candidate with a value-only fallback followed by one type lookup;
- renamed 58 private constants across 42 CodeActions handlers to the required `_camelCase` convention;
- separated result data, error data, registration data and refactoring requests from their enclosing method calls;
- replaced the two genuinely multi-stage dependency pipelines with explicit collection stages;
- retained three short deduplicate/order/materialise LINQ pipelines because they remain clearer than expanded loops;
- retained the two exhaustive result-mapping switch expressions; their remaining nested-return signals come from invariant throws rather than nested response construction;
- a repeat Roslyn syntax scan found zero complex conditional, complex coalescing or statement-separation candidates in both projects;
- both affected projects built with zero `latest-all` analyzer warnings;
- all 588 focused tests passed; and
- the fast unit and contract suite passed 1,886 tests.

### Batch 3 — Workspace expression cleanup

**Status:** Complete

Review Workspace resolution, loading, lifecycle, change detection and transaction code. Keep transaction and commit changes especially narrow even when a readability rewrite is behaviour-preserving.

Validation: Workspace unit and integration tests, transaction and recovery acceptance scenarios where touched, analyzer build and the fast suite.

Completed work:

- replaced complex conditional expressions and non-trivial coalescing with explicit failure-first control flow;
- simplified multi-stage collection searches and builders while preserving ordering, ambiguity and first-failure behaviour;
- separated result, outcome, context and commit-plan construction from enclosing return statements;
- retained six concise invariant guards and five single-purpose LINQ pipelines where the existing form remains clearer; and
- reduced the Workspace syntax scan to zero complex conditional, non-trivial coalescing and multiline statement-separation candidates.

Validation evidence:

- the Workspace project built with zero `latest-all` analyzer warnings;
- all 741 Workspace unit tests and 70 Workspace integration tests passed; and
- all 10 published-host acceptance tests passed, including transaction commit, recovery and rollback workflows; and
- the fast unit and contract suite passed 1,886 tests.

### Batch 4 — Host expression cleanup

**Status:** Complete

Review Host protocol/schema construction, result serialisation and plugin loading. Schema construction should be split into named schema fragments without changing the published contract.

Validation: Host unit and protocol integration tests, schema contract tests, analyzer build and the fast suite.

Completed work:

- split response, mutation, transaction and nullable schema construction into named JSON fragments while retaining the published property names, required members and union shapes;
- changed published-result and Workspace-result mapping to explicit outcome-first control flow;
- replaced multi-stage plugin collision and response-contract LINQ with loops that expose ownership, duplicate and protected-name decisions;
- separated plugin bootstrap, catalogue status and server status construction into named values; and
- reduced the Host syntax scan from 64 candidates to zero across all audited categories.

Validation evidence:

- the Host project built with zero `latest-all` analyzer warnings;
- all 270 Host unit tests and 44 Host integration tests passed, including schema contract tests;
- all 10 published-host acceptance tests passed; and
- the fast unit and contract suite passed 1,886 tests.

### Batch 5 — Plugins.Core tool review

Revisit only the 41 production candidates in Plugins.Core. Retain simple deterministic ordering pipelines. Replace LINQ only where it combines multiple stages or obscures bounded work, and do not claim a performance improvement without measurement.

Validation: focused tool tests with coverage, Plugins.Core analyzer build, the fast suite and scenario measurement only when a hot-path implementation changes.

### Batch 6 — Test statement separation

Apply the 1,531 mechanical test-only separation fixes in owned project groups:

1. CodeActions tests;
2. Workspace tests;
3. Host, acceptance and shared integration tests; and
4. Plugins and Plugins.Core tests.

Plugins.Core tests form the largest group and should remain last so that their 648 mechanical separation fixes are not mixed with the production tool review.

Validation: format changed files, affected-project analyzer builds and the relevant test project after each group.

### Batch 7 — Test expression cleanup

Review the remaining test conditional, coalescing, LINQ and nested-return candidates. Keep parameterised test setup concise when a conditional selects simple values, but replace branches that construct or invoke work on both sides. Retain readable Roslyn-object and mock construction.

Validation: affected-project analyzer builds and the relevant test projects.

## Recommended first delivery

Start with Batch 1. It is a complete, low-risk production cleanup that establishes the formatting baseline before any control-flow or collection logic changes. The later batches can then focus on substantive readability without carrying unrelated whitespace in the same diff.
