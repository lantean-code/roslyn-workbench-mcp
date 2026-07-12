namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IWorkspaceLoadWorkflow
{
    ValueTask<ValidatedWorkspaceLoadResult> LoadAsync(
        string loadedPath,
        string workspaceRoot,
        CancellationToken cancellationToken);
}
