namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class WorkspaceResolverFactory : IWorkspaceResolverFactory
{
    private readonly IWorkspacePathComparison _workspacePathComparison;

    public WorkspaceResolverFactory(IWorkspacePathComparison workspacePathComparison)
    {
        _workspacePathComparison = workspacePathComparison;
    }

    public IWorkspaceResolver Create(
        Solution solution,
        WorkspaceIdentity? workspaceIdentity,
        int? transactionRevision)
    {
        return new WorkspaceResolver(
            solution,
            workspaceIdentity,
            transactionRevision,
            _workspacePathComparison);
    }
}
