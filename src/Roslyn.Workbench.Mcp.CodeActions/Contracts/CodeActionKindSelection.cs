namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Selects the kinds of code actions to discover.
/// </summary>
internal enum CodeActionKindSelection
{
    /// <summary>
    /// Discovers code fixes only.
    /// </summary>
    CodeFixes = 1,

    /// <summary>
    /// Discovers refactorings only.
    /// </summary>
    Refactorings = 2,

    /// <summary>
    /// Discovers code fixes and refactorings.
    /// </summary>
    All = 3,
}
