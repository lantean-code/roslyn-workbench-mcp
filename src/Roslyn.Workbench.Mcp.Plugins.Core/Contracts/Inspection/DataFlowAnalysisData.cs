using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by analyze-data-flow.
/// </summary>
public sealed record DataFlowAnalysisData
{
    /// <summary>
    /// Gets the analyzed region.
    /// </summary>
    public ResolvedLocation? Region { get; init; }

    /// <summary>
    /// Gets the variables declared within the region.
    /// </summary>
    public IReadOnlyList<SymbolReference> VariablesDeclared { get; init; } = [];

    /// <summary>
    /// Gets the symbols read within the region.
    /// </summary>
    public IReadOnlyList<SymbolReference> ReadInside { get; init; } = [];

    /// <summary>
    /// Gets the symbols written within the region.
    /// </summary>
    public IReadOnlyList<SymbolReference> WrittenInside { get; init; } = [];

    /// <summary>
    /// Gets the symbols flowing into the region.
    /// </summary>
    public IReadOnlyList<SymbolReference> DataFlowsIn { get; init; } = [];

    /// <summary>
    /// Gets the symbols flowing out of the region.
    /// </summary>
    public IReadOnlyList<SymbolReference> DataFlowsOut { get; init; } = [];

    /// <summary>
    /// Gets the captured symbols.
    /// </summary>
    public IReadOnlyList<SymbolReference> Captured { get; init; } = [];
}
