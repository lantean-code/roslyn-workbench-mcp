using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed record TransactionAdmissionResult
{
    private static readonly TransactionAdmissionResult _admitted = new(isAdmitted: true, existingOwnerWorkspaceId: null);

    [MemberNotNullWhen(false, nameof(ExistingOwnerWorkspaceId))]
    public bool IsAdmitted { get; }

    public Guid? ExistingOwnerWorkspaceId { get; }

    private TransactionAdmissionResult(bool isAdmitted, Guid? existingOwnerWorkspaceId)
    {
        IsAdmitted = isAdmitted;
        ExistingOwnerWorkspaceId = existingOwnerWorkspaceId;
    }

    public static TransactionAdmissionResult Admitted()
    {
        return _admitted;
    }

    public static TransactionAdmissionResult Rejected(Guid existingOwnerWorkspaceId)
    {
        return new TransactionAdmissionResult(isAdmitted: false, existingOwnerWorkspaceId);
    }
}
