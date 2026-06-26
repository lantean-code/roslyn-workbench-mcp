namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to load a writable workspace.
/// </summary>
public sealed record WorkspaceOpenRequest
{
    /// <summary>
    /// Gets the absolute solution or project path to load.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
