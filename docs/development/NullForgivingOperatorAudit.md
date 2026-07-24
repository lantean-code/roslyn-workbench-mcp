# Production Null-Forgiving Operator Audit

## Historical Baseline

The 2026-07-11 production scan found 167 null-forgiving operators across 46 files:

| Project                             | Operators |
| ----------------------------------- | --------: |
| `Roslyn.Workbench.Mcp.Plugins.Core` |        85 |
| `Roslyn.Workbench.Mcp.Workspace`    |        45 |
| `Roslyn.Workbench.Mcp`              |        17 |
| `Roslyn.Workbench.Mcp.CodeActions`  |        12 |
| `Roslyn.Workbench.Mcp.Plugins`      |         8 |

The largest concentrations are resolution results and inspection projections, Workspace commit entries, result mappers, and schema/reflection boundaries. Four uses initialised non-nullable model properties with `null!`.

## Completed Remediation

The 2026-07-11 remediation reduced production use to zero. Shared result types now expose nullable-flow evidence for successful states, invalid model states use required members, transaction artifacts are accessed through validated operations, nullable Roslyn results are checked at their boundaries, and nullable projections use checked locals or `OfType<T>`.

## Remediation Strategy

1. Fix shared discriminated results first. Give successful and failed states validated factories plus `MemberNotNullWhen` or `NotNullWhen` annotations. This removes repeated suppressions from Host, Plugin, Code Action and Workspace result mappers.
2. Replace `null!` model initialisers with required members or validated constructors. Invalid model states must not be constructible.
3. Encode operation-specific transaction and recovery entry invariants through validated factories or operation-specific types. Commit application must not infer that staged, backup or marker paths are present from an enum alone.
4. Improve shared Roslyn resolution result types so resolved states prove that their value, document, syntax node, semantic model and location are present. Update inspection tools after these central types are corrected.
5. Replace nullable LINQ projections with `OfType<T>`, explicit pattern matching, or checked locals. A preceding `Where` clause is not sufficient justification for a later null-forgiving operator.
6. Validate reflection, JSON and Roslyn API results at their boundaries and throw a specific invariant exception when an expected value is absent. These are not automatic exceptions to the rule.

## Enforcement

`ProductionNullForgivingOperatorAuditTests` parses every C# file under `src` and fails when it finds a `SuppressNullableWarningExpression`. There is no baseline or allow-list: production code must remain at zero. Any proposed exception must first update the production guidance and this audit with a written justification. The enforcement test lives in the Host test project's `Architecture` area and runs in the default fast loop; it is source governance, not part of the Code Action compatibility audit.
