namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal sealed class WorkspaceResolverFactory : IWorkspaceResolverFactory
{
    private readonly IAddressableDocumentEligibility _addressableDocumentEligibility;
    private readonly IWorkspacePathComparison _workspacePathComparison;
    private readonly IWorkspacePathServiceFactory _workspacePathServiceFactory;
    private readonly IWorkspaceSelectorFactory _workspaceSelectorFactory;

    public WorkspaceResolverFactory(
        IAddressableDocumentEligibility addressableDocumentEligibility,
        IWorkspacePathComparison workspacePathComparison,
        IWorkspacePathServiceFactory workspacePathServiceFactory,
        IWorkspaceSelectorFactory workspaceSelectorFactory)
    {
        _addressableDocumentEligibility = addressableDocumentEligibility;
        _workspacePathComparison = workspacePathComparison;
        _workspacePathServiceFactory = workspacePathServiceFactory;
        _workspaceSelectorFactory = workspaceSelectorFactory;
    }

    public IWorkspaceResolver Create(
        Solution solution,
        WorkspaceIdentity? workspaceIdentity,
        WorkspaceProjectTargetFrameworkMap projectTargetFrameworks,
        SnapshotPrecondition? snapshot)
    {
        return new WorkspaceResolver(
            solution,
            snapshot,
            projectTargetFrameworks,
            _addressableDocumentEligibility,
            _workspaceSelectorFactory,
            _workspacePathComparison,
            _workspacePathServiceFactory.Create(workspaceIdentity));
    }
}
