namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one source call site and its optional context.
/// </summary>
internal sealed record CallerSiteInfo
{
    /// <summary>
    /// Gets the exact call-site location.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the optional source context requested for the call site.
    /// </summary>
    public string? Context { get; init; }
}
