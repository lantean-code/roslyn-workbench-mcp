namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal static class WorkspaceInputEvaluationDiagnostics
{
    public static IReadOnlyList<DiagnosticInfo> Create(IReadOnlyList<WorkspaceProjectInputFailure> failures)
    {
        return failures
            .Select(static failure => new DiagnosticInfo
            {
                Id = "WorkspaceInputEvaluationFailed",
                Severity = Contracts.Results.DiagnosticSeverity.Error,
                Message = $"Could not evaluate inputs for '{failure.ProjectPath}': {failure.Message}",
            })
            .ToArray();
    }
}
