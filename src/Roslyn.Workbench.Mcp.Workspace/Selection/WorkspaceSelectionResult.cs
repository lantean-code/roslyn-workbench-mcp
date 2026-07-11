using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal sealed class WorkspaceSelectionResult
{
    private WorkspaceSelectionResult(
        WorkspaceSelection? selection,
        WorkspaceOperationError? error)
    {
        Selection = selection;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Selection))]
    public bool HasError => Error is not null;

    public WorkspaceOperationError? Error
    {
        get;
    }

    public WorkspaceSelection? Selection
    {
        get;
    }

    public static WorkspaceSelectionResult Success(WorkspaceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return new WorkspaceSelectionResult(
            selection: selection,
            error: null);
    }

    public static WorkspaceSelectionResult Failure(WorkspaceOperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new WorkspaceSelectionResult(
            selection: null,
            error: error);
    }
}
