using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one caller and its call sites.
/// </summary>
public sealed record CallerInfo
{
    /// <summary>
    /// Gets the calling symbol.
    /// </summary>
    public SymbolReference? Caller { get; init; }

    /// <summary>
    /// Gets the call-site locations.
    /// </summary>
    public IReadOnlyList<ResolvedLocation> Locations { get; init; } = [];

    /// <summary>
    /// Gets optional source snippets for the call sites.
    /// </summary>
    public IReadOnlyList<string> Contexts { get; init; } = [];
}
