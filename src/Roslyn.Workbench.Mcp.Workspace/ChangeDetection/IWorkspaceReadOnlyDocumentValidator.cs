namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceReadOnlyDocumentValidator
{
    ValueTask<WorkspaceReadOnlyDocumentValidationStatus> ValidateAsync(
        Solution solution,
        string workspaceRoot,
        CancellationToken cancellationToken);
}
