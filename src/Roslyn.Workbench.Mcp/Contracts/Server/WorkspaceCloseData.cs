namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents the structured payload returned when a workspace is closed.
/// </summary>
internal sealed record WorkspaceCloseData
{
    /// <summary>
    /// Gets the path that was closed.
    /// </summary>
    public string ClosedPath { get; init; } = string.Empty;
}
