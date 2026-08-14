namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one caller and its call sites.
/// </summary>
internal sealed record CallerInfo
{
    /// <summary>
    /// Gets the calling symbol.
    /// </summary>
    public SymbolReference? Caller { get; init; }

    /// <summary>
    /// Gets the bounded call sites for the caller.
    /// </summary>
    public BoundedCollection<CallerSiteInfo> CallSites { get; init; } = BoundedCollection.Empty<CallerSiteInfo>();
}
