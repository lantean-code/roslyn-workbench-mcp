namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Identifies the kind of a discovered Code Action.
/// </summary>
internal enum CodeActionKind
{
    /// <summary>
    /// A Roslyn code fix associated with one or more diagnostics.
    /// </summary>
    CodeFix,

    /// <summary>
    /// A Roslyn refactoring for a document, selection or caret.
    /// </summary>
    Refactoring,
}
