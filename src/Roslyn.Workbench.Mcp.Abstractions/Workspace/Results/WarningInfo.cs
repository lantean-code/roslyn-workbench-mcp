namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a structured warning emitted by a tool.
/// </summary>
public sealed record WarningInfo
{
    /// <summary>
    /// Gets the stable machine-readable warning code.
    /// </summary>
    [Description("The stable machine-readable warning code.")]
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable warning message.
    /// </summary>
    [Description("The human-readable warning message.")]
    public required string Message { get; init; }
}
