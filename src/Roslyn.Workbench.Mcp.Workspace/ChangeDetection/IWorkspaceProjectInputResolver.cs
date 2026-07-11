namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceProjectInputResolver
{
    IReadOnlyList<string> GetEvaluatedInputPaths(string? projectPath);
}
