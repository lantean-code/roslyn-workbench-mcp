namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one operation projected within a control-flow basic block.
/// </summary>
internal sealed record BasicBlockOperationInfo
{
    /// <summary>
    /// Gets the Roslyn operation kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operation type display name, when available.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Gets the exact source location of the operation.
    /// </summary>
    public ResolvedLocation? Location { get; init; }
}
