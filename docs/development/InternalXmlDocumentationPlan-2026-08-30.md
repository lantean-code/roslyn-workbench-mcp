# Internal XML documentation implementation plan

Date: 2026-08-30

**Status:** Approved; declaration coverage complete and documentation-quality remediation in progress.

## Purpose

Extend the repository's XML-documentation policy from externally consumed public APIs to the complete non-private production surface under `src`. The additional documentation is intended to help open-source contributors understand component responsibilities, collaboration boundaries, invariants and non-obvious behaviour without requiring them to reconstruct every internal design from call sites.

## Governed surface

Documentation is required on explicitly declared public, internal, protected, protected-internal and private-protected production types and members whose containing types are also governed. This includes classes, records, structs, interfaces, enums, delegates, constructors, methods, operators, properties, indexers, events, fields, constants and enum members.

Private members, local functions, file-local types, accessors, implicit or compiler-generated members and explicit interface implementations that inherit their contract are excluded. Test projects, fixtures and the separate `tools` tree are outside this programme. Overrides and interface implementations may use `<inheritdoc/>` where the inherited contract accurately describes the implementation.

Every governed declaration must have an accurate `<summary>` or a justified `<inheritdoc/>`. Summary elements use the repository's established multiline layout. Every parameter and generic type parameter must be documented, and every non-void callable member must describe its returned value. A concise summary that closely follows a self-explanatory declaration name is acceptable XML documentation; expand it where responsibility, behaviour, constraints, ownership, units, side effects or non-obvious outcomes need clarification. The separate rule against name-restating text applies to agent-facing `Description` attributes, not XML documentation. Even where concise name-aligned wording is appropriate, summaries must describe the declaration's real role: factories create values, predicates describe the decision they make, and operations state their concrete behaviour rather than using generated phrases such as `Performs the ... operation`. Use `<value>`, `<exception>` and `<remarks>` where they add information needed to use or maintain the member correctly.

## Measured upper bound

Published dogfood inspection measured 4,298 symbols at the internal accessibility threshold across the seven production projects, compared with 451 public symbols. The difference of 3,847 declarations is an upper bound rather than the missing-documentation count because many internal declarations already have comments.

| Project | Internal threshold | Public threshold | Newly governed upper bound |
|---|---:|---:|---:|
| `Roslyn.Workbench.Mcp.Abstractions` | 232 | 221 | 11 |
| `Roslyn.Workbench.Mcp` | 933 | 0 | 933 |
| `Roslyn.Workbench.Mcp.Workspace` | 1,325 | 63 | 1,262 |
| `Roslyn.Workbench.Mcp.CodeActions` | 602 | 0 | 602 |
| `Roslyn.Workbench.Mcp.Plugins` | 397 | 150 | 247 |
| `Roslyn.Workbench.Mcp.Plugins.Core` | 703 | 2 | 701 |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers` | 106 | 15 | 91 |

## Enforcement design

Compiler warning `CS1591` covers publicly visible types and members only, so it cannot enforce the approved internal surface. A fast Roslyn syntax audit in `Roslyn.Workbench.Mcp.Test` will inspect explicit production declarations, calculate their effective source accessibility and require a `<summary>` or `<inheritdoc/>`.

The audit will begin with an exact, sorted baseline of existing violations. New undocumented declarations or changes to the governed surface must not expand or silently rewrite that baseline. Each remediation batch removes its documented declarations from the baseline; the baseline is deleted when the final batch reaches zero.

The foundation audit records 3,053 existing undocumented declarations:

| Project | Baseline declarations |
|---|---:|
| `Roslyn.Workbench.Mcp.Abstractions` | 6 |
| `Roslyn.Workbench.Mcp` | 748 |
| `Roslyn.Workbench.Mcp.Workspace` | 1,238 |
| `Roslyn.Workbench.Mcp.CodeActions` | 553 |
| `Roslyn.Workbench.Mcp.Plugins` | 202 |
| `Roslyn.Workbench.Mcp.Plugins.Core` | 200 |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers` | 106 |

The remediation batches document all 3,053 declarations in the original exact baseline across Abstractions, plugin analysers, plugin authoring and adaptation, Workspace, Code Actions, the Host and bundled Core plugins. The exact baseline is now empty, and the structural audit prevents new undocumented non-private production declarations.

The initial declaration-coverage pass did not meet the established quality of the existing public API documentation. A second structural audit therefore enforces multiline summaries, complete parameter and generic-type-parameter documentation, and return documentation for every non-void callable member. After normalising 2,232 generated single-line summaries, the quality inventory contains 1,869 missing parameter descriptions, 160 missing generic type-parameter descriptions and 619 missing return descriptions. In addition to clearing that structural inventory, each responsibility-based batch reviews generated wording against the implementation and replaces mechanical summaries with an accurate description of the declaration's role or result.

## Delivery order

1. Record this design, update `src/AGENTS.md`, implement the source audit and generate the exact baseline.
2. Document Abstractions and the plugin analyser project.
3. Document the plugin authoring and adaptation project.
4. Document Workspace in responsibility-based batches.
5. Document Code Actions.
6. Document the Host.
7. Document the bundled Core plugin contracts, projections and handlers.
8. Remove the baseline, run complete non-acceptance validation and complete the repository review process.

Projects may be remediated independently after the shared audit and writing rules are stable. Each batch must review documentation for factual accuracy and ensure non-obvious maintenance information is retained; concise name-aligned summaries are acceptable for self-explanatory declarations.

## Validation

The foundation requires focused unit coverage for inclusion, exclusion, partial declarations and accepted documentation forms. Each remediation batch runs the documentation audit, the affected fast non-acceptance suite, targeted formatting and the affected `latest-all` analyser build. The final batch runs the complete non-acceptance fast loop. Acceptance and external-repository scenarios are not required because this programme does not change runtime behaviour or published MCP contracts.

The audit's focused coverage has one approved defensive branch exception: `ParameterSyntax.Type` is nullable for malformed Roslyn syntax, so key generation retains an `unknown` fallback. Valid production source cannot exercise that branch, and Roslyn's parser supplies a missing type node even for the malformed parameter forms used during verification. Covering the null alternative would therefore require an artificial Roslyn syntax object or reflection. The focused audit coverage is 100% of lines and 98.41% of branches with only that defensive Roslyn-owned branch outstanding.
