namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceProjectInputResolver : IWorkspaceProjectInputResolver
{
    public IReadOnlyList<string> GetEvaluatedInputPaths(string? projectPath)
    {
        return MsBuildProjectUtilities.GetEvaluatedInputPaths(projectPath);
    }
}
