namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one direct symbol dependency.
/// </summary>
internal sealed record DependencyInfo
{
    /// <summary>
    /// Gets the dependent symbol reference.
    /// </summary>
    [Description("The dependent symbol reference.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the symbol kind for the dependency.
    /// </summary>
    [Description("The symbol kind for the dependency.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the containing assembly name when requested.
    /// </summary>
    [Description("The containing assembly name when requested.")]
    public string? AssemblyName { get; init; }
}
