namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal enum ScopedCodeFixCandidateResolutionOutcome
{
    Resolved,
    NoDiagnostics,
    Unavailable,
    Ambiguous,
}
