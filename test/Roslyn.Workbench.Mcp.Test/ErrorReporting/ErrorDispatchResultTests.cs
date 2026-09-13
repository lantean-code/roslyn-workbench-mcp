using Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorDispatchResultTests
{
    [Fact]
    public void GIVEN_AcceptedDispatch_WHEN_CreatingResult_THEN_ShouldExposeAcceptedInvariant()
    {
        var result = ErrorDispatchResult.Accepted("ReportReference", "PayloadDigest");

        result.IsAccepted.Should().BeTrue();
        result.Outcome.Should().Be(ErrorDispatchOutcome.Accepted);
        result.ReportReference.Should().Be("ReportReference");
        result.PayloadDigest.Should().Be("PayloadDigest");
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RejectedDispatch_WHEN_CreatingResult_THEN_ShouldExposeRejectedInvariant()
    {
        var result = ErrorDispatchResult.Rejected("ErrorCode", "ErrorMessage");

        result.IsAccepted.Should().BeFalse();
        result.Outcome.Should().Be(ErrorDispatchOutcome.Rejected);
        result.ReportReference.Should().BeNull();
        result.PayloadDigest.Should().BeNull();
        result.ErrorCode.Should().Be("ErrorCode");
        result.ErrorMessage.Should().Be("ErrorMessage");
    }
}
