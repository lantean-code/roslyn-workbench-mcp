namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class WorkspaceResolverFactory : IWorkspaceResolverFactory
{
    private readonly IWorkspacePathComparison _workspacePathComparison;
    private readonly IWorkspacePathServiceFactory _workspacePathServiceFactory;

    public WorkspaceResolverFactory(
        IWorkspacePathComparison workspacePathComparison,
        IWorkspacePathServiceFactory workspacePathServiceFactory)
    {
        _workspacePathComparison = workspacePathComparison;
        _workspacePathServiceFactory = workspacePathServiceFactory;
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
            _workspacePathComparison,
            _workspacePathServiceFactory.Create(workspaceIdentity));
    }
}
