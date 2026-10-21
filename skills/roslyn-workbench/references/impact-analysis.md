# Assess dependencies, change impact and cleanup candidates

## Choose the analysis

| Need | Tool |
| --- | --- |
| Outgoing distinct types/members in signatures and supported operations | `get-symbol-dependencies`; metadata dependencies can appear. |
| Enclosing source symbols referencing a target | `get-symbol-dependents`. |
| Coupling at project, namespace, type or symbol granularity | `get-dependency-graph`. |
| Mutually dependent project, namespace or type groups | `find-dependency-cycles`. |
| Reference/caller/override/implementation counts and public-surface status | `get-change-impact`. |
| Candidate tests with supporting reasons | `get-test-impact`. |
| Compiler-reported unused locals or members | `find-unused-symbols`. |
| Repeated whole executable blocks | `find-duplicate-code`. |

## Review a prospective change

Use `get-change-impact`, then follow concrete dependents or [references](navigation.md). Its location cap samples supporting references independently of counts. Caller counts cover method callers; implementation counts apply to interface types.

For `get-test-impact`, set `testScope` and `includeReasons: true`. Candidates require type names ending `Test`, `Tests`, `Spec` or `Specs`, or method names starting `GIVEN_` or containing `_THEN_Should`; **test attributes alone do not qualify**. Matching uses direct references to the target or its owning type. It can overinclude another member's tests and miss helper-call dependencies. Inspect candidates alongside the repository's test guidance.

## Explore coupling

Graph edges run **dependent → dependency**. Project granularity uses project references; other granularities inspect source documents. Omitted nodes also remove connected edges.

Query `find-dependency-cycles` directly rather than inferring absence from a truncated graph. It returns strongly connected groups, not ordered cycle paths. Analysis budgets are separate from the result cap; budget exhaustion returns `AnalysisLimitExceeded` instead of partial cycle analysis. Narrow the scope or raise the relevant budget.

## Review cleanup candidates

`find-unused-symbols` covers only **CS0168, CS0169 and CS0219**. `includeInternal` broadens the accessibility filter; it does not add whole-program dead-code analysis.

`find-duplicate-code` matches whole blocks after whitespace normalization, retaining identifiers and literals. It covers block-bodied methods, constructors, local functions and accessors; expression bodies and arbitrary statement subsequences are excluded. Read the matched occurrences before choosing an extraction.
