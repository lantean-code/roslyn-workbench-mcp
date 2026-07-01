using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Requests a descriptor and preflight context for one discovered code action.
/// </summary>
public sealed record DescribeCodeActionRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the opaque action token to describe.
    /// </summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
