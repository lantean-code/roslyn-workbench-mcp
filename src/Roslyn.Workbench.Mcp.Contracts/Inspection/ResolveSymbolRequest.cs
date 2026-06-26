using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to resolve a symbol from a source location.
/// </summary>
public sealed record ResolveSymbolRequest
{
    /// <summary>
    /// Gets the location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
