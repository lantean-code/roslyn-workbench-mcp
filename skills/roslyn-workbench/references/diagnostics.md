# Investigate diagnostics and focused findings

## Choose the evidence

| Need | Tool and coverage |
| --- | --- |
| Compiler and project-analyzer findings | `get-diagnostics`; severity and ID filters apply before the result cap. |
| Focused async findings | `analyze-async`: bundled `AsyncFixer01`–`AsyncFixer06` plus compiler `CS4014`. |
| Focused nullable findings | `analyze-nullability`: compiler IDs beginning `CS86`. |
| Local disposal candidates | `analyze-disposables`: recognized `IDisposable`/`IAsyncDisposable` patterns. |

## Diagnose and correct

1. Query the affected document, then read diagnostic messages and source with `get-code-context`.
2. Widen to project scope for reference, configuration or compilation failures. Document scope only retains diagnostics whose **primary source location** belongs to that document; project-wide findings without source locations disappear.
3. For unexpected rule enablement or language behavior, inspect `get-document-options` and the project's referenced analyzers through `get-project-details`; see [structure.md](structure.md).
4. Use `list-code-actions` for an applicable fix and [code-actions.md](code-actions.md) for staging. Requery relevant diagnostics after the staged change is visible.

Empty `ids` or `severities` arrays mean **no filter**, not no findings.

## Interpret focused results

`analyze-async` runs its bundled rules even when the project does not reference AsyncFixer, while respecting analyzer configuration. Use `get-diagnostics` for rules outside its seven-ID coverage.

For `analyze-nullability`, a supplied `location` overrides `scope` and retains findings intersecting that span. Nullable-related IDs outside `CS86*` require `get-diagnostics`.

`analyze-disposables` examines local declarations, skips using declarations, and recognizes supported direct or finally-block `Dispose`/`DisposeAsync` calls. Review ownership transfers and exits in source: interprocedural lifetime and ownership transfer are not resolved. Use [flow-analysis.md](flow-analysis.md) when branching or variable flow needs closer inspection.
