namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one source call site and its optional context.
/// </summary>
internal sealed record CallerSiteInfo
{
    /// <summary>
    /// Gets the exact call-site location.
    /// </summary>
    [Description("The exact call-site location.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the optional source context requested for the call site.
    /// </summary>
    [Description("The optional source context requested for the call site.")]
    public string? Context { get; init; }
}
