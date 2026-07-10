using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one direct symbol dependency.
/// </summary>
public sealed record DependencyInfo
{
    /// <summary>
    /// Gets the dependent symbol reference.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the symbol kind for the dependency.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the containing assembly name when requested.
    /// </summary>
    public string? AssemblyName { get; init; }
}
