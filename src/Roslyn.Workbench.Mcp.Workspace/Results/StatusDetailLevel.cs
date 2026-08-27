namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Defines the response detail level for status-oriented tools.
/// </summary>
public enum StatusDetailLevel
{
    /// <summary>
    /// Returns only the smallest broadly useful status projection.
    /// </summary>
    Minimal,

    /// <summary>
    /// Returns the standard operational status projection.
    /// </summary>
    Standard,

    /// <summary>
    /// Returns the full status projection including heavier optional branches.
    /// </summary>
    Full,
}
