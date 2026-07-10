namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

/// <summary>
/// Represents a structured warning emitted by a tool.
/// </summary>
public sealed record WarningInfo
{
    /// <summary>
    /// Gets the stable machine-readable warning code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable warning message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
