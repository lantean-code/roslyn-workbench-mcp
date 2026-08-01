using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal interface ICodeActionReferenceState
{
    bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference);

    bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference);

    bool IsPreparedFixAll(Guid actionId);

    void Remove(Guid actionId);

    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);

    void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId);

    void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots);
}
