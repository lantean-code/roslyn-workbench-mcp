using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one exit point in a control-flow analysis result.
/// </summary>
public sealed record ControlFlowExit
{
    /// <summary>
    /// Gets the exit kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exit location, when available.
    /// </summary>
    public ResolvedLocation? Location { get; init; }
}
