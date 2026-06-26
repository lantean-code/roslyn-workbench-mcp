using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by go-to-definition.
/// </summary>
public sealed record DefinitionData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the resolved definitions.
    /// </summary>
    public IReadOnlyList<DefinitionLocation> Definitions { get; init; } = [];
}
