namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceErrorCodesTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_WorkspaceErrorCodes_WHEN_ComparingCompatibilityContract_THEN_ShouldRetainExactValues()
    {
        var actual = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(WorkspaceErrorCodes.CommitRecoveryCapacity)] = WorkspaceErrorCodes.CommitRecoveryCapacity,
            [nameof(WorkspaceErrorCodes.DocumentAmbiguous)] = WorkspaceErrorCodes.DocumentAmbiguous,
            [nameof(WorkspaceErrorCodes.DocumentNotFound)] = WorkspaceErrorCodes.DocumentNotFound,
            [nameof(WorkspaceErrorCodes.InvalidRequest)] = WorkspaceErrorCodes.InvalidRequest,
            [nameof(WorkspaceErrorCodes.LinkedDocumentConflict)] = WorkspaceErrorCodes.LinkedDocumentConflict,
            [nameof(WorkspaceErrorCodes.MutationCandidateChanged)] = WorkspaceErrorCodes.MutationCandidateChanged,
            [nameof(WorkspaceErrorCodes.SnapshotMismatch)] = WorkspaceErrorCodes.SnapshotMismatch,
            [nameof(WorkspaceErrorCodes.TransactionAlreadyActive)] = WorkspaceErrorCodes.TransactionAlreadyActive,
            [nameof(WorkspaceErrorCodes.TransactionCapacity)] = WorkspaceErrorCodes.TransactionCapacity,
            [nameof(WorkspaceErrorCodes.TransactionConflicted)] = WorkspaceErrorCodes.TransactionConflicted,
            [nameof(WorkspaceErrorCodes.TransactionHistoryUnavailable)] = WorkspaceErrorCodes.TransactionHistoryUnavailable,
            [nameof(WorkspaceErrorCodes.TransactionOwner)] = WorkspaceErrorCodes.TransactionOwner,
            [nameof(WorkspaceErrorCodes.TransactionRequired)] = WorkspaceErrorCodes.TransactionRequired,
            [nameof(WorkspaceErrorCodes.WorkspaceAlreadyOpen)] = WorkspaceErrorCodes.WorkspaceAlreadyOpen,
            [nameof(WorkspaceErrorCodes.WorkspaceAuthorityChanged)] = WorkspaceErrorCodes.WorkspaceAuthorityChanged,
            [nameof(WorkspaceErrorCodes.WorkspaceBusy)] = WorkspaceErrorCodes.WorkspaceBusy,
            [nameof(WorkspaceErrorCodes.WorkspaceCapacityReached)] = WorkspaceErrorCodes.WorkspaceCapacityReached,
            [nameof(WorkspaceErrorCodes.WorkspaceLoadFailed)] = WorkspaceErrorCodes.WorkspaceLoadFailed,
            [nameof(WorkspaceErrorCodes.WorkspaceNotOpen)] = WorkspaceErrorCodes.WorkspaceNotOpen,
            [nameof(WorkspaceErrorCodes.WorkspaceNotSupported)] = WorkspaceErrorCodes.WorkspaceNotSupported,
            [nameof(WorkspaceErrorCodes.WorkspaceOutOfDate)] = WorkspaceErrorCodes.WorkspaceOutOfDate,
        };

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CommitRecoveryCapacity"] = "CommitRecoveryCapacityReached",
            ["DocumentAmbiguous"] = "DocumentAmbiguous",
            ["DocumentNotFound"] = "DocumentNotFound",
            ["InvalidRequest"] = "InvalidRequest",
            ["LinkedDocumentConflict"] = "LinkedDocumentConflict",
            ["MutationCandidateChanged"] = "MutationCandidateChanged",
            ["SnapshotMismatch"] = "SnapshotMismatch",
            ["TransactionAlreadyActive"] = "TransactionAlreadyActive",
            ["TransactionCapacity"] = "RevisionCapacityReached",
            ["TransactionConflicted"] = "TransactionConflicted",
            ["TransactionHistoryUnavailable"] = "TransactionHistoryUnavailable",
            ["TransactionOwner"] = "TransactionOwnedByWorkspace",
            ["TransactionRequired"] = "NoActiveTransaction",
            ["WorkspaceAlreadyOpen"] = "WorkspaceAlreadyOpen",
            ["WorkspaceAuthorityChanged"] = "WorkspaceAuthorityChanged",
            ["WorkspaceBusy"] = "WorkspaceBusy",
            ["WorkspaceCapacityReached"] = "WorkspaceCapacityReached",
            ["WorkspaceLoadFailed"] = "WorkspaceLoadFailed",
            ["WorkspaceNotOpen"] = "WorkspaceNotOpen",
            ["WorkspaceNotSupported"] = "WorkspaceNotSupported",
            ["WorkspaceOutOfDate"] = "WorkspaceOutOfDate",
        };

        actual.Should().BeEquivalentTo(expected);
    }
}
