using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorReportingAvailabilityServiceTests
{
    [Fact]
    public void GIVEN_NeverConsent_WHEN_GettingAvailability_THEN_ShouldReturnDisabledWithoutConsultingRuntimeConsent()
    {
        var consentService = new Mock<IErrorReportingConsentService>();
        var options = new ErrorReportingOptions
        {
            ConsentMode = ErrorReportingConsentMode.Never,
        };
        var target = new ErrorReportingAvailabilityService(Options.Create(options), consentService.Object);

        var result = target.GetAvailability(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, supportsElicitation: true);

        result.State.Should().Be(ErrorReportingState.DisabledByConfiguration);
        result.CanPrepare.Should().BeFalse();
        result.PrepareTool.Should().BeNull();
        consentService.Verify(
            item => item.GetState(It.IsAny<Guid?>(), It.IsAny<long?>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_SessionSuppression_WHEN_GettingAvailability_THEN_ShouldReturnSuppressed()
    {
        var target = CreateTarget(ErrorReportingConsentState.SuppressedForSession);

        var result = target.GetAvailability(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, supportsElicitation: true);

        result.State.Should().Be(ErrorReportingState.SuppressedForSession);
        result.CanPrepare.Should().BeFalse();
        result.PrepareTool.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PromptRequiredWithoutElicitation_WHEN_GettingAvailability_THEN_ShouldReturnApprovalUnavailable()
    {
        var target = CreateTarget(ErrorReportingConsentState.PromptRequired);

        var result = target.GetAvailability(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, supportsElicitation: false);

        result.State.Should().Be(ErrorReportingState.ApprovalUnavailable);
        result.CanPrepare.Should().BeFalse();
        result.PrepareTool.Should().BeNull();
    }

    [Theory]
    [InlineData((int)ErrorReportingConsentState.PromptRequired, (int)ErrorReportingState.Available)]
    [InlineData((int)ErrorReportingConsentState.AlwaysApproved, (int)ErrorReportingState.AlwaysApproved)]
    [InlineData((int)ErrorReportingConsentState.AllowedForWorkspace, (int)ErrorReportingState.AllowedForWorkspace)]
    [InlineData((int)ErrorReportingConsentState.AllowedForSession, (int)ErrorReportingState.AllowedForSession)]
    public void GIVEN_PreparableConsentState_WHEN_GettingAvailability_THEN_ShouldPublishPreparationTool(
        int consentStateValue,
        int expectedStateValue)
    {
        var consentState = (ErrorReportingConsentState)consentStateValue;
        var expectedState = (ErrorReportingState)expectedStateValue;
        var target = CreateTarget(consentState);

        var result = target.GetAvailability(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, supportsElicitation: true);

        result.State.Should().Be(expectedState);
        result.CanPrepare.Should().BeTrue();
        result.PrepareTool.Should().Be(ServerOwnedToolRegistration.PrepareErrorReportName);
    }

    private static ErrorReportingAvailabilityService CreateTarget(ErrorReportingConsentState consentState)
    {
        var consentService = new Mock<IErrorReportingConsentService>();
        consentService
            .Setup(item => item.GetState(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1))
            .Returns(consentState);

        return new ErrorReportingAvailabilityService(
            Options.Create(new ErrorReportingOptions()),
            consentService.Object);
    }
}
