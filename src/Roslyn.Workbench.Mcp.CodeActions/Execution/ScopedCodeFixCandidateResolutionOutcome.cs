namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal enum ScopedCodeFixCandidateResolutionOutcome
{
    Resolved,
    NoDiagnostics,
    Unavailable,
    Ambiguous,
}
