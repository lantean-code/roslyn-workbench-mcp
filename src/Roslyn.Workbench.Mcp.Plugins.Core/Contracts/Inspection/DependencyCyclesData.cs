namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-dependency-cycles.
/// </summary>
public sealed record DependencyCyclesData
{
    /// <summary>
    /// Gets the returned cycles.
    /// </summary>
    public BoundedCollection<DependencyCycle> Cycles { get; init; } = BoundedCollection<DependencyCycle>.Empty();
}
