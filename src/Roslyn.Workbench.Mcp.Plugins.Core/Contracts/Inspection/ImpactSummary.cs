namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a bounded impact summary for a symbol change.
/// </summary>
public sealed record ImpactSummary
{
    /// <summary>
    /// Gets the number of direct source references found.
    /// </summary>
    public int ReferenceCount { get; init; }

    /// <summary>
    /// Gets the number of direct callers found.
    /// </summary>
    public int CallerCount { get; init; }

    /// <summary>
    /// Gets the number of overrides found.
    /// </summary>
    public int OverrideCount { get; init; }

    /// <summary>
    /// Gets the number of implementations found.
    /// </summary>
    public int ImplementationCount { get; init; }

    /// <summary>
    /// Gets the number of directly exposed API surface entries represented by the symbol.
    /// </summary>
    public int PublicSurfaceCount { get; init; }
}
