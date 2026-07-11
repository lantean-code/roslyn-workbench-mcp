# Production Null-Forgiving Operator Audit

## Baseline

The 2026-07-11 production scan found 167 null-forgiving operators across 46
files:

| Project | Operators |
|---|---:|
| `Roslyn.Workbench.Mcp.Plugins.Core` | 85 |
| `Roslyn.Workbench.Mcp.Workspace` | 45 |
| `Roslyn.Workbench.Mcp` | 17 |
| `Roslyn.Workbench.Mcp.CodeActions` | 12 |
| `Roslyn.Workbench.Mcp.Plugins` | 8 |

The largest concentrations are resolution results and inspection projections,
Workspace commit entries, result mappers, and schema/reflection boundaries.
Four uses initialise non-nullable model properties with `null!`.

## Remediation Strategy

1. Fix shared discriminated results first. Give successful and failed states
   validated factories plus `MemberNotNullWhen` or `NotNullWhen` annotations.
   This removes repeated suppressions from Host, Plugin, Code Action and
   Workspace result mappers.
2. Replace `null!` model initialisers with required members or validated
   constructors. Invalid model states must not be constructible.
3. Encode operation-specific transaction and recovery entry invariants through
   validated factories or operation-specific types. Commit application must not
   infer that staged, backup or marker paths are present from an enum alone.
4. Improve shared Roslyn resolution result types so resolved states prove that
   their value, document, syntax node, semantic model and location are present.
   Update inspection tools after these central types are corrected.
5. Replace nullable LINQ projections with `OfType<T>`, explicit pattern
   matching, or checked locals. A preceding `Where` clause is not sufficient
   justification for a later null-forgiving operator.
6. Validate reflection, JSON and Roslyn API results at their boundaries and
   throw a specific invariant exception when an expected value is absent.
   These are not automatic exceptions to the rule.

## Enforcement

Add a Roslyn-syntax architecture test that counts
`SuppressNullableWarningExpression` nodes under `src`. Initially ratchet against
an explicit reviewed baseline so no new operator can be introduced. Reduce the
baseline with each remediation group and remove it when the production count
reaches zero. Any eventual allow-list entry must identify the file, expression
and written justification; the target remains an empty allow-list.
