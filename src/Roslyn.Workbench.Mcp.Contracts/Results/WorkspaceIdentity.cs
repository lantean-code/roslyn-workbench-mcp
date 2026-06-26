namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents the identity of the currently loaded workspace.
/// </summary>
public sealed record WorkspaceIdentity
{
    /// <summary>
    /// Gets the workspace epoch for the loaded baseline.
    /// </summary>
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the absolute path of the loaded workspace.
    /// </summary>
    public string LoadedPath { get; init; } = string.Empty;
}
