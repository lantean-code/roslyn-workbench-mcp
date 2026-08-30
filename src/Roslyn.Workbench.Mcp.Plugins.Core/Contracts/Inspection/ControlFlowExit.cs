namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one exit point in a control-flow analysis result.
/// </summary>
internal sealed record ControlFlowExit
{
    /// <summary>
    /// Gets the exit kind.
    /// </summary>
    [Description("The exit kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the exit location, when available.
    /// </summary>
    [Description("The exit location, when available.")]
    public ResolvedLocation? Location { get; init; }
}
