namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-dependency-cycles.
/// </summary>
internal sealed record DependencyCyclesData : IQueryResponse
{
    /// <summary>
    /// Gets the returned cycles.
    /// </summary>
    [Description("The returned cycles.")]
    public BoundedCollection<DependencyCycle> Cycles { get; init; } = BoundedCollection.Empty<DependencyCycle>();
}
