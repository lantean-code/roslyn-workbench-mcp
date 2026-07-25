namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceProjectCompatibilityInspector : IWorkspaceProjectCompatibilityInspector
{
    public (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) Inspect(string projectPath)
    {
        try
        {
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);
            var root = project.Xml;

            var isSdkStyle = !string.IsNullOrWhiteSpace(root.Sdk)
                || root.Children.OfType<Microsoft.Build.Construction.ProjectSdkElement>().Any();

            return (isSdkStyle, []);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return (false, [CreateLoadDiagnostic(exception.Message)]);
        }
    }

    private static DiagnosticInfo CreateLoadDiagnostic(string message)
    {
        return new DiagnosticInfo
        {
            Id = "WorkspaceLoad",
            Severity = Results.DiagnosticSeverity.Error,
            Message = message,
        };
    }
}
