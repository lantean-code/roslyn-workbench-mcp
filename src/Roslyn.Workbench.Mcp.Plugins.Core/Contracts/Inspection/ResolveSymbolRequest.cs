namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to resolve a symbol from a source location.
/// </summary>
internal sealed record ResolveSymbolRequest : WorkspaceBoundRequest
{
    private const int _defaultDeclarationsMaxResults = 32;

    /// <summary>
    /// Gets the location selector.
    /// </summary>
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the optional declarations limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDeclarationsMaxResults)]
    public int? DeclarationsLimit { get; init; } = _defaultDeclarationsMaxResults;

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDeclarationsLimit => ResultLimit.GetEffectiveValue(DeclarationsLimit, _defaultDeclarationsMaxResults);
}
