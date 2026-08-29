namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by go-to-definition.
/// </summary>
internal sealed record DefinitionData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved definitions.
    /// </summary>
    [Description("The resolved definitions.")]
    public BoundedCollection<DefinitionLocation> Definitions { get; init; } = BoundedCollection.Empty<DefinitionLocation>();
}
