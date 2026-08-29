namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a bounded impact summary for a symbol change.
/// </summary>
internal sealed record ImpactSummary
{
    /// <summary>
    /// Gets the number of direct source references found.
    /// </summary>
    [Description("The number of direct source references found.")]
    public int ReferenceCount { get; init; }

    /// <summary>
    /// Gets the number of direct callers found.
    /// </summary>
    [Description("The number of direct callers found.")]
    public int CallerCount { get; init; }

    /// <summary>
    /// Gets the number of overrides found.
    /// </summary>
    [Description("The number of overrides found.")]
    public int OverrideCount { get; init; }

    /// <summary>
    /// Gets the number of implementations found.
    /// </summary>
    [Description("The number of implementations found.")]
    public int ImplementationCount { get; init; }

    /// <summary>
    /// Gets the number of directly exposed API surface entries represented by the symbol.
    /// </summary>
    [Description("The number of directly exposed API surface entries represented by the symbol.")]
    public int PublicSurfaceCount { get; init; }
}
