using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorReportingConsentServiceTests
{
    [Fact]
    public void GIVEN_WorkspaceGrant_WHEN_WorkspaceEpochIsInvalidated_THEN_ShouldRequirePromptAgain()
    {
        var target = CreateTarget(ErrorReportingConsentMode.Prompt);
        target.AllowWorkspace("WorkspaceId", 5);

        target.GetState("WorkspaceId", 5).Should().Be(ErrorReportingConsentState.AllowedForWorkspace);

        target.InvalidateWorkspace("WorkspaceId", 5);

        target.GetState("WorkspaceId", 5).Should().Be(ErrorReportingConsentState.PromptRequired);
    }

    [Fact]
    public void GIVEN_SessionGrant_WHEN_QueryingAnyWorkspace_THEN_ShouldAllowForSession()
    {
        var target = CreateTarget(ErrorReportingConsentMode.Prompt);

        target.AllowSession();

        target.GetState("WorkspaceId", 5).Should().Be(ErrorReportingConsentState.AllowedForSession);
        target.GetState(null, null).Should().Be(ErrorReportingConsentState.AllowedForSession);
    }

    [Fact]
    public void GIVEN_SessionSuppression_WHEN_PreviousGrantsExist_THEN_ShouldSuppressEveryScope()
    {
        var target = CreateTarget(ErrorReportingConsentMode.Prompt);
        target.AllowWorkspace("WorkspaceId", 5);
        target.AllowSession();

        target.SuppressSession();

        target.GetState("WorkspaceId", 5).Should().Be(ErrorReportingConsentState.SuppressedForSession);
    }

    [Fact]
    public void GIVEN_AlwaysStartupPolicy_WHEN_NoTemporaryGrantExists_THEN_ShouldBeAlwaysApproved()
    {
        var target = CreateTarget(ErrorReportingConsentMode.Always);

        target.GetState(null, null).Should().Be(ErrorReportingConsentState.AlwaysApproved);
    }

    private static ErrorReportingConsentService CreateTarget(ErrorReportingConsentMode mode)
    {
        var options = new ErrorReportingOptions
        {
            ConsentMode = mode,
        };

        return new ErrorReportingConsentService(Options.Create(options));
    }
}
