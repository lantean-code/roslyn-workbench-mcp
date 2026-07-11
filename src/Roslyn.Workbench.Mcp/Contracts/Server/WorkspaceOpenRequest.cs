namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents a request to load a writable workspace.
/// </summary>
public sealed record WorkspaceOpenRequest
{
    /// <summary>
    /// Gets the optional caller-friendly alias for the workspace.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Gets the absolute solution or project path to load.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional absolute repository or workspace root used for coordination and transaction boundaries.
    /// </summary>
    public string? WorkspaceRoot { get; init; }
}
