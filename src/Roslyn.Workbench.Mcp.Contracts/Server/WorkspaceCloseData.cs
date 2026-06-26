namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when a workspace is closed.
/// </summary>
public sealed record WorkspaceCloseData
{
    /// <summary>
    /// Gets the path that was closed.
    /// </summary>
    public string ClosedPath { get; init; } = string.Empty;
}
