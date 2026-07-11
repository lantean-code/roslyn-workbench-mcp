namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

/// <summary>
/// Represents the identity of the currently loaded workspace.
/// </summary>
public sealed record WorkspaceIdentity
{
    /// <summary>
    /// Gets the stable server-generated workspace identifier.
    /// </summary>
    public string WorkspaceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional caller-friendly alias.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Gets the workspace epoch for the loaded baseline.
    /// </summary>
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the absolute path of the loaded workspace.
    /// </summary>
    public string LoadedPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the absolute repository or workspace root used for coordination and transaction boundaries.
    /// </summary>
    public string WorkspaceRoot { get; init; } = string.Empty;
}
