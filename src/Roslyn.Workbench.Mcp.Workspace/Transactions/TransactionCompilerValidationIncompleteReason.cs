namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies why compiler-impact validation could not provide complete assurance.
/// </summary>
internal enum TransactionCompilerValidationIncompleteReason
{
    /// <summary>
    /// Validation was complete.
    /// </summary>
    None = 0,

    /// <summary>
    /// A Workspace input changed after the transaction baseline was captured.
    /// </summary>
    ExternalWorkspaceInputsChanged = 1,

    /// <summary>
    /// The loaded Workspace contains an error-level load diagnostic.
    /// </summary>
    WorkspaceLoadError = 2,

    /// <summary>
    /// An affected project was unavailable in one of the compared snapshots.
    /// </summary>
    ProjectUnavailable = 3,

    /// <summary>
    /// An affected project compilation could not be evaluated.
    /// </summary>
    CompilationUnavailable = 4,
}
