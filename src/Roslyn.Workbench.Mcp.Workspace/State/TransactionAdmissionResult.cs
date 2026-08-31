using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Represents whether the process-wide single-transaction slot was acquired.
/// </summary>
internal sealed record TransactionAdmissionResult
{
    private static readonly TransactionAdmissionResult _admitted = new(isAdmitted: true, existingOwnerWorkspaceId: null);

    /// <summary>
    /// Gets whether the transaction was admitted.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ExistingOwnerWorkspaceId))]
    public bool IsAdmitted { get; }

    /// <summary>
    /// Gets the Workspace that already owns the transaction slot when admission is rejected.
    /// </summary>
    public Guid? ExistingOwnerWorkspaceId { get; }

    private TransactionAdmissionResult(bool isAdmitted, Guid? existingOwnerWorkspaceId)
    {
        IsAdmitted = isAdmitted;
        ExistingOwnerWorkspaceId = existingOwnerWorkspaceId;
    }

    /// <summary>
    /// Returns the shared successful admission result.
    /// </summary>
    /// <returns>An admitted result.</returns>
    public static TransactionAdmissionResult Admitted()
    {
        return _admitted;
    }

    /// <summary>
    /// Creates a rejection identifying the Workspace that owns the transaction slot.
    /// </summary>
    /// <param name="existingOwnerWorkspaceId">The existing transaction owner's Workspace identifier.</param>
    /// <returns>A rejected admission result.</returns>
    public static TransactionAdmissionResult Rejected(Guid existingOwnerWorkspaceId)
    {
        return new TransactionAdmissionResult(isAdmitted: false, existingOwnerWorkspaceId);
    }
}
