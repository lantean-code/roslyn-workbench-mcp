using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one exported API symbol.
/// </summary>
public sealed record ApiSymbolInfo
{
    /// <summary>
    /// Gets the exported symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the declared accessibility.
    /// </summary>
    public string Accessibility { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the symbol is marked obsolete.
    /// </summary>
    public bool IsObsolete { get; init; }
}
