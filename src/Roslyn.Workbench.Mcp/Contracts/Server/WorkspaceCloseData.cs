namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when a workspace is closed.
/// </summary>
internal sealed record WorkspaceCloseData
{
    /// <summary>
    /// Gets the path that was closed.
    /// </summary>
    [Description("The path that was closed.")]
    public required string ClosedPath { get; init; }
}
