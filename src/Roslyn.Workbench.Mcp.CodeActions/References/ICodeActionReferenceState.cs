using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Maintains cached Code Action references and invalidates them with their owning workspace state.
/// </summary>
internal interface ICodeActionReferenceState
{
    /// <summary>
    /// Attempts to cache a replay recipe and create a temporary reference for it.
    /// </summary>
    /// <param name="recipe">The replay recipe used to reconstruct the Code Action.</param>
    /// <param name="expiresAt">The time after which the stored value is no longer valid.</param>
    /// <param name="reference">The created reference when the cache admits the recipe.</param>
    /// <returns><see langword="true"/> when the cache admits the reference; otherwise, <see langword="false"/>.</returns>
    bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference);

    /// <summary>
    /// Attempts to retrieve an unexpired Code Action reference.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="reference">The matching unexpired reference, when found.</param>
    /// <returns><see langword="true"/> when an unexpired reference exists; otherwise, <see langword="false"/>.</returns>
    bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference);

    /// <summary>
    /// Determines whether the reference identifies a prepared Fix All operation.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <returns><see langword="true"/> when the reference exists and represents a prepared Fix All operation; otherwise, <see langword="false"/>.</returns>
    bool IsPreparedFixAll(Guid actionId);

    /// <summary>
    /// Removes a Code Action reference from the cache and its invalidation indexes.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    void Remove(Guid actionId);

    /// <summary>
    /// Invalidates references owned by the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch that the operation expects.</param>
    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);

    /// <summary>
    /// Invalidates references owned by the specified transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch that the operation expects.</param>
    /// <param name="transactionId">The transaction identifier.</param>
    void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    /// <summary>
    /// Invalidates references whose workspace snapshots are stale.
    /// </summary>
    /// <param name="snapshots">The snapshot identities whose Code Action references must be invalidated.</param>
    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
