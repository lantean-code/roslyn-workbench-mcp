using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Selection;

/// <summary>
/// Represents either a selected workspace session or a structured selection error.
/// </summary>
internal sealed class WorkspaceSelectionResult
{
    private WorkspaceSelectionResult(
        WorkspaceSelection? selection,
        WorkspaceOperationError? error)
    {
        Selection = selection;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the result contains an error.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Selection))]
    public bool HasError => Error is not null;

    /// <summary>
    /// Gets the structured error when selection failed.
    /// </summary>
    public WorkspaceOperationError? Error
    {
        get;
    }

    /// <summary>
    /// Gets the selected workspace and session when selection succeeded.
    /// </summary>
    public WorkspaceSelection? Selection
    {
        get;
    }

    /// <summary>
    /// Creates a successful workspace selection.
    /// </summary>
    /// <param name="selection">The workspace selection produced by successful resolution.</param>
    /// <returns>A result containing the selected workspace.</returns>
    public static WorkspaceSelectionResult Success(WorkspaceSelection selection)
    {
        return new WorkspaceSelectionResult(
            selection: selection,
            error: null);
    }

    /// <summary>
    /// Creates a failed workspace selection.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result containing the structured selection error.</returns>
    public static WorkspaceSelectionResult Failure(WorkspaceOperationError error)
    {
        return new WorkspaceSelectionResult(
            selection: null,
            error: error);
    }
}
