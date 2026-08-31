namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Describes a structured workspace error and the action required to recover from it.
/// </summary>
internal sealed class WorkspaceOperationError
{
    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable explanation of the error.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the action a caller must take before retrying.
    /// </summary>
    public RequiredAction? RequiredAction { get; init; }
}
