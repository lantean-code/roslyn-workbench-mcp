namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Identifies the Roslyn provider family that produced a discovered action.
/// </summary>
internal enum DiscoveredActionKind
{
    /// <summary>
    /// The action was produced by a refactoring provider.
    /// </summary>
    Refactoring = 0,

    /// <summary>
    /// The action was produced by a Code Fix provider.
    /// </summary>
    CodeFix = 1,
}
