namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Provides the workspace selector and snapshot precondition required by mutation requests.
/// </summary>
public abstract record WorkspaceMutationRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the expected workspace snapshot for the mutation.
    /// </summary>
    [Description("The expected workspace snapshot for the mutation.")]
    public required SnapshotPrecondition ExpectedSnapshot { get; init; }
}
