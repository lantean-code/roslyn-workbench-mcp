namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents the outcome of resolving a workspace selector.
/// </summary>
public enum SelectorResolveStatus
{
    /// <summary>
    /// Exactly one match was found.
    /// </summary>
    Resolved,

    /// <summary>
    /// No match was found.
    /// </summary>
    NotFound,

    /// <summary>
    /// More than one match was found.
    /// </summary>
    Ambiguous,
}
