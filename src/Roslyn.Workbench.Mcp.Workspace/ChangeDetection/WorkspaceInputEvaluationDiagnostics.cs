namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Converts project-input evaluation failures into Workspace lifecycle diagnostics.
/// </summary>
internal static class WorkspaceInputEvaluationDiagnostics
{
    /// <summary>
    /// Creates one actionable diagnostic for each project whose inputs could not be evaluated.
    /// </summary>
    /// <param name="failures">The project-input evaluation failures.</param>
    /// <returns>The corresponding Workspace diagnostics.</returns>
    public static IReadOnlyList<DiagnosticInfo> Create(IReadOnlyList<WorkspaceProjectInputFailure> failures)
    {
        var diagnostics = failures
            .Select(static failure => new DiagnosticInfo
            {
                Id = "WorkspaceInputEvaluationFailed",
                Severity = Results.DiagnosticSeverity.Error,
                Message = $"Could not evaluate inputs for '{failure.ProjectPath}': {failure.Message}",
            })
            .ToArray();

        return diagnostics;
    }
}
