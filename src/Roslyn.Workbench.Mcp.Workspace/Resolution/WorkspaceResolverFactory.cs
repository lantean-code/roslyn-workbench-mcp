namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class WorkspaceResolverFactory : IWorkspaceResolverFactory
{
    public IWorkspaceResolver Create(
        Solution solution,
        WorkspaceIdentity? workspaceIdentity,
        int? transactionRevision)
    {
        return new WorkspaceResolver(solution, workspaceIdentity, transactionRevision);
    }
}
