namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one exported API symbol.
/// </summary>
internal sealed record ApiSymbolInfo
{
    /// <summary>
    /// Gets the exported symbol.
    /// </summary>
    [Description("The exported symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the declared accessibility.
    /// </summary>
    [Description("The declared accessibility.")]
    public string Accessibility { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the symbol is marked obsolete.
    /// </summary>
    [Description("Whether the symbol is marked obsolete.")]
    public bool IsObsolete { get; init; }
}
