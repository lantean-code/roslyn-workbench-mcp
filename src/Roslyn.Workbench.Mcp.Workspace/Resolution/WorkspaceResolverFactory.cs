namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Creates selector resolvers bound to an immutable workspace snapshot.
/// </summary>
internal sealed class WorkspaceResolverFactory : IWorkspaceResolverFactory
{
    private readonly IAddressableDocumentEligibility _addressableDocumentEligibility;
    private readonly IWorkspacePathComparison _workspacePathComparison;
    private readonly IWorkspacePathServiceFactory _workspacePathServiceFactory;
    private readonly IWorkspaceSelectorFactory _workspaceSelectorFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceResolverFactory"/> class.
    /// </summary>
    /// <param name="addressableDocumentEligibility">The policy that excludes generated and otherwise non-addressable documents.</param>
    /// <param name="workspacePathComparison">The comparison rules used for workspace path.</param>
    /// <param name="workspacePathServiceFactory">The factory used to create the required workspace path service.</param>
    /// <param name="workspaceSelectorFactory">The factory used to create the required workspace selector.</param>
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

    /// <summary>
    /// Creates a resolver for selectors and canonical references within a solution snapshot.
    /// </summary>
    /// <param name="solution">The immutable solution snapshot to resolve against.</param>
    /// <param name="workspaceIdentity">The identity of the workspace being processed.</param>
    /// <param name="projectTargetFrameworks">The target-framework metadata used to resolve the project scope.</param>
    /// <param name="snapshot">The workspace snapshot against which the operation runs.</param>
    /// <returns>A resolver bound to the supplied workspace snapshot.</returns>
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
