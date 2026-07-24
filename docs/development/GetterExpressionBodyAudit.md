# Production Getter Expression-Body Audit

## Baseline

The 2026-07-11 production scan found five explicitly implemented getters that use block bodies. Auto-properties and properties with `set` or `init` accessors are outside this audit because they cannot be represented as expression-bodied properties.

| Project | Property | Location | Assessment |
| --- | --- | --- | --- |
| `Roslyn.Workbench.Mcp.Workspace` | `WorkspaceTransaction.CurrentSolution` | `Transactions/WorkspaceTransaction.cs` | Simple conditional value selection; convert to an expression-bodied property. |
| `Roslyn.Workbench.Mcp.Workspace` | `WorkspaceSelectionResult.HasError` | `Selection/WorkspaceSelectionResult.cs` | Single return expression with nullable-flow attributes; convert without changing the attributes. |
| `Roslyn.Workbench.Mcp.Workspace` | `WorkspaceRootResolver.PathComparison` | `Loading/WorkspaceRootResolver.cs` | Single return expression currently compressed into a one-line accessor block; convert to an expression-bodied property. |
| `Roslyn.Workbench.Mcp.CodeActions` | `CodeActionResolution<T>.HasRejection` | `Resolution/CodeActionResolution.cs` | Single return expression with nullable-flow attributes; convert without changing the attributes. |
| `Roslyn.Workbench.Mcp.CodeActions` | `CodeActionApplyResult.HasRejection` | `Execution/CodeActionApplyResult.cs` | Single return expression with nullable-flow attributes; convert without changing the attributes. |

All five getters are simple get-only properties under the repository guidance. None requires a block body for branching, local variables, exception handling, or multiple operations.

## Completed Remediation

The 2026-07-11 remediation converted all five simple getters to expression-bodied properties. The production baseline is now zero.

## Remediation Strategy

1. Convert the three discriminated-state properties to expression bodies while retaining their `MemberNotNullWhen` attributes exactly as written.
2. Convert `WorkspaceTransaction.CurrentSolution` to a multiline conditional expression body.
3. Convert `WorkspaceRootResolver.PathComparison` to a normal expression-bodied property rather than retaining its one-line accessor block.
4. Format only the five changed production files, run their affected tests, and then run the full suite.

## Enforcement

Extend the production syntax audit to report get-only properties whose getter contains exactly one `return` statement and has no `set` or `init` accessor. The enforcement must inspect C# syntax rather than rely on line layout. It must not reject genuinely complex getters merely because an expression body is syntactically possible.

The target baseline for simple block-bodied getters is zero. Any retained block getter must contain logic that materially benefits from the block form; an allow-list should not be introduced for formatting preference alone.
