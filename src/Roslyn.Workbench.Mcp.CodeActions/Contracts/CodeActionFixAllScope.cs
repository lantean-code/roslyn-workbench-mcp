namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Identifies a Fix All scope supported by a discovered code fix.
/// </summary>
internal enum CodeActionFixAllScope
{
    /// <summary>
    /// Applies the fix to the originating document.
    /// </summary>
    Document,

    /// <summary>
    /// Applies the fix to the originating project.
    /// </summary>
    Project,

    /// <summary>
    /// Applies the fix to the complete solution.
    /// </summary>
    Solution,
}
