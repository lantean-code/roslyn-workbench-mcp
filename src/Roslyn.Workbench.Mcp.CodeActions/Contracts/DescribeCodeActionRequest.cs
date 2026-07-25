namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Requests a descriptor and preflight context for one discovered code action.
/// </summary>
internal sealed record DescribeCodeActionRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the opaque action reference to describe.
    /// </summary>
    public required Guid ActionId { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
