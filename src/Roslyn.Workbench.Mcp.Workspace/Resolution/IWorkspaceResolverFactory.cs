namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

/// <summary>
/// Creates selector resolvers bound to an immutable workspace snapshot.
/// </summary>
internal interface IWorkspaceResolverFactory
{
    /// <summary>
    /// Creates a resolver for selectors and canonical references within a solution snapshot.
    /// </summary>
    /// <param name="solution">The immutable solution snapshot to resolve against.</param>
    /// <param name="workspaceIdentity">The identity of the workspace being processed.</param>
    /// <param name="projectTargetFrameworks">The target-framework metadata used to resolve the project scope.</param>
    /// <param name="snapshot">The workspace snapshot against which the operation runs.</param>
    /// <returns>A resolver bound to the supplied workspace snapshot.</returns>
    IWorkspaceResolver Create(
        Solution solution,
        WorkspaceIdentity? workspaceIdentity,
        WorkspaceProjectTargetFrameworkMap projectTargetFrameworks,
        SnapshotPrecondition? snapshot);
}
