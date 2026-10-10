namespace Roslyn.Workbench.Mcp.Test.Protocol.Results;

public sealed class HostToolErrorCodesTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_HostToolErrorCodes_WHEN_ComparingCompatibilityContract_THEN_ShouldRetainExactValues()
    {
        var actual = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(HostToolErrorCodes.ApprovalUnavailable)] = HostToolErrorCodes.ApprovalUnavailable,
            [nameof(HostToolErrorCodes.ErrorDetailsUnavailable)] = HostToolErrorCodes.ErrorDetailsUnavailable,
            [nameof(HostToolErrorCodes.ErrorReportCapacityReached)] = HostToolErrorCodes.ErrorReportCapacityReached,
            [nameof(HostToolErrorCodes.ErrorReportNotApproved)] = HostToolErrorCodes.ErrorReportNotApproved,
            [nameof(HostToolErrorCodes.ErrorReportPayloadTooLarge)] = HostToolErrorCodes.ErrorReportPayloadTooLarge,
            [nameof(HostToolErrorCodes.ErrorReportSubmissionInProgress)] = HostToolErrorCodes.ErrorReportSubmissionInProgress,
            [nameof(HostToolErrorCodes.ErrorReportingUnavailable)] = HostToolErrorCodes.ErrorReportingUnavailable,
            [nameof(HostToolErrorCodes.InvalidApprovalResponse)] = HostToolErrorCodes.InvalidApprovalResponse,
            [nameof(HostToolErrorCodes.InvalidExceptionMessageHandling)] = HostToolErrorCodes.InvalidExceptionMessageHandling,
            [nameof(HostToolErrorCodes.InvalidPreparedErrorReport)] = HostToolErrorCodes.InvalidPreparedErrorReport,
            [nameof(HostToolErrorCodes.InvalidRequest)] = HostToolErrorCodes.InvalidRequest,
            [nameof(HostToolErrorCodes.PreparedReportUnavailable)] = HostToolErrorCodes.PreparedReportUnavailable,
            [nameof(HostToolErrorCodes.RecoveryOutsideWorkspaceAuthority)] = HostToolErrorCodes.RecoveryOutsideWorkspaceAuthority,
            [nameof(HostToolErrorCodes.SentryCaptureRejected)] = HostToolErrorCodes.SentryCaptureRejected,
            [nameof(HostToolErrorCodes.TransactionCommitNotApproved)] = HostToolErrorCodes.TransactionCommitNotApproved,
            [nameof(HostToolErrorCodes.TransactionReceiptUnavailable)] = HostToolErrorCodes.TransactionReceiptUnavailable,
            [nameof(HostToolErrorCodes.UnhandledException)] = HostToolErrorCodes.UnhandledException,
        };

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApprovalUnavailable"] = "ApprovalUnavailable",
            ["ErrorDetailsUnavailable"] = "ErrorDetailsUnavailable",
            ["ErrorReportCapacityReached"] = "ErrorReportCapacityReached",
            ["ErrorReportNotApproved"] = "ErrorReportNotApproved",
            ["ErrorReportPayloadTooLarge"] = "ErrorReportPayloadTooLarge",
            ["ErrorReportSubmissionInProgress"] = "ErrorReportSubmissionInProgress",
            ["ErrorReportingUnavailable"] = "ErrorReportingUnavailable",
            ["InvalidApprovalResponse"] = "InvalidApprovalResponse",
            ["InvalidExceptionMessageHandling"] = "InvalidExceptionMessageHandling",
            ["InvalidPreparedErrorReport"] = "InvalidPreparedErrorReport",
            ["InvalidRequest"] = "InvalidRequest",
            ["PreparedReportUnavailable"] = "PreparedReportUnavailable",
            ["RecoveryOutsideWorkspaceAuthority"] = "RecoveryOutsideWorkspaceAuthority",
            ["SentryCaptureRejected"] = "SentryCaptureRejected",
            ["TransactionCommitNotApproved"] = "TransactionCommitNotApproved",
            ["TransactionReceiptUnavailable"] = "TransactionReceiptUnavailable",
            ["UnhandledException"] = "UnhandledException",
        };

        actual.Should().BeEquivalentTo(expected);
    }
}
