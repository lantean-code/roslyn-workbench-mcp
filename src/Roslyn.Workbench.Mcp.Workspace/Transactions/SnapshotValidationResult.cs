using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record SnapshotValidationResult
{
    private static readonly SnapshotValidationResult _valid = new(isValid: true, error: null);

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid { get; }

    public WorkspaceOperationError? Error { get; }

    private SnapshotValidationResult(bool isValid, WorkspaceOperationError? error)
    {
        IsValid = isValid;
        Error = error;
    }

    public static SnapshotValidationResult Valid()
    {
        return _valid;
    }

    public static SnapshotValidationResult Invalid(WorkspaceOperationError error)
    {
        return new SnapshotValidationResult(isValid: false, error);
    }
}
