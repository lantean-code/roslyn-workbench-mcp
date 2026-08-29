namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one caller and its call sites.
/// </summary>
internal sealed record CallerInfo
{
    /// <summary>
    /// Gets the calling symbol.
    /// </summary>
    [Description("The calling symbol.")]
    public SymbolReference? Caller { get; init; }

    /// <summary>
    /// Gets the bounded call sites for the caller.
    /// </summary>
    [Description("The bounded call sites for the caller.")]
    public BoundedCollection<CallerSiteInfo> CallSites { get; init; } = BoundedCollection.Empty<CallerSiteInfo>();
}
