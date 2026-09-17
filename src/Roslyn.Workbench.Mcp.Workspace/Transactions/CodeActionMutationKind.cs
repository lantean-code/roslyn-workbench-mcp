namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies the Code Action family that produced a staged mutation.
/// </summary>
internal enum CodeActionMutationKind
{
    /// <summary>
    /// A Code Fix associated with one or more diagnostics.
    /// </summary>
    CodeFix = 0,

    /// <summary>
    /// A source refactoring offered for a document, selection or caret.
    /// </summary>
    Refactoring = 1,

    /// <summary>
    /// A prepared Fix All operation derived from a Code Fix.
    /// </summary>
    FixAll = 2,
}
