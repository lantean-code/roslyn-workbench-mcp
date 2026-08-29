namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the identity of the currently loaded workspace.
/// </summary>
public sealed record WorkspaceIdentity
{
    /// <summary>
    /// Gets the stable server-generated workspace identifier.
    /// </summary>
    [Description("The stable server-generated workspace identifier.")]
    public Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the optional caller-friendly alias.
    /// </summary>
    [Description("The optional caller-friendly alias.")]
    public string? Alias { get; init; }

    /// <summary>
    /// Gets the workspace epoch for the loaded baseline.
    /// </summary>
    [Description("The workspace epoch for the loaded baseline.")]
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the absolute path of the loaded workspace.
    /// </summary>
    [Description("The absolute path of the loaded workspace.")]
    public string LoadedPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the absolute repository or workspace root used for coordination and transaction boundaries.
    /// </summary>
    [Description("The absolute repository or workspace root used for coordination and transaction boundaries.")]
    public string WorkspaceRoot { get; init; } = string.Empty;
}
