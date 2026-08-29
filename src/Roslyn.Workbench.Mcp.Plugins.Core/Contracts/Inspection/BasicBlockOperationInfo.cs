namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one operation projected within a control-flow basic block.
/// </summary>
internal sealed record BasicBlockOperationInfo
{
    /// <summary>
    /// Gets the Roslyn operation kind.
    /// </summary>
    [Description("The Roslyn operation kind.")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operation type display name, when available.
    /// </summary>
    [Description("The operation type display name, when available.")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets the exact source location of the operation.
    /// </summary>
    [Description("The exact source location of the operation.")]
    public ResolvedLocation? Location { get; init; }
}
