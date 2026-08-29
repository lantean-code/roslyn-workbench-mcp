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

        var result = target.GetAvailability(Guid.NewGuid(), 1, supportsElicitation: true);

        result.State.Should().Be(ErrorReportingState.DisabledByConfiguration);
        result.CanPrepare.Should().BeFalse();
        result.PrepareTool.Should().BeNull();
        consentService.Verify(item => item.GetState(), Times.Never);
    }

    [Fact]
    public void GIVEN_PromptRequiredWithoutElicitation_WHEN_GettingAvailability_THEN_ShouldReturnApprovalUnavailable()
    {
        var target = CreateTarget(ErrorReportingConsentState.PromptRequired);

        var result = target.GetAvailability(Guid.NewGuid(), 1, supportsElicitation: false);

        result.State.Should().Be(ErrorReportingState.ApprovalUnavailable);
        result.CanPrepare.Should().BeFalse();
        result.PrepareTool.Should().BeNull();
    }

    [Fact]
    public void GIVEN_DisabledConsentState_WHEN_GettingAvailability_THEN_ShouldFailClosed()
    {
        var target = CreateTarget(ErrorReportingConsentState.Disabled);

        var result = target.GetAvailability(Guid.NewGuid(), 1, supportsElicitation: true);

        result.State.Should().Be(ErrorReportingState.DisabledByConfiguration);
        result.CanPrepare.Should().BeFalse();
        result.PrepareTool.Should().BeNull();
    }

    [Theory]
    [InlineData((int)ErrorReportingConsentState.PromptRequired, (int)ErrorReportingState.Available)]
    [InlineData((int)ErrorReportingConsentState.AlwaysApproved, (int)ErrorReportingState.AlwaysApproved)]
    public void GIVEN_PreparableConsentState_WHEN_GettingAvailability_THEN_ShouldPublishPreparationTool(
        int consentStateValue,
        int expectedStateValue)
    {
        var target = CreateTarget((ErrorReportingConsentState)consentStateValue);

        var result = target.GetAvailability(Guid.NewGuid(), 1, supportsElicitation: true);

        result.State.Should().Be((ErrorReportingState)expectedStateValue);
        result.CanPrepare.Should().BeTrue();
        result.PrepareTool.Should().Be(ServerOwnedToolRegistration.PrepareErrorReportName);
    }

    private static ErrorReportingAvailabilityService CreateTarget(ErrorReportingConsentState consentState)
    {
        var consentService = new Mock<IErrorReportingConsentService>();
        consentService
            .Setup(item => item.GetState())
            .Returns(consentState);

        return new ErrorReportingAvailabilityService(
            Options.Create(new ErrorReportingOptions()),
            consentService.Object);
    }
}
