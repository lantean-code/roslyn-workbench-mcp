using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorReportingConsentServiceTests
{
    [Theory]
    [InlineData((int)ErrorReportingConsentMode.Never, (int)ErrorReportingConsentState.Disabled)]
    [InlineData((int)ErrorReportingConsentMode.Prompt, (int)ErrorReportingConsentState.PromptRequired)]
    [InlineData((int)ErrorReportingConsentMode.Always, (int)ErrorReportingConsentState.AlwaysApproved)]
    [InlineData(int.MaxValue, (int)ErrorReportingConsentState.Disabled)]
    public void GIVEN_ConfiguredConsentMode_WHEN_GettingState_THEN_ShouldReturnConfigurationState(
        int consentModeValue,
        int expectedStateValue)
    {
        var target = new ErrorReportingConsentService(
            Options.Create(new ErrorReportingOptions
            {
                ConsentMode = (ErrorReportingConsentMode)consentModeValue,
            }));

        var result = target.GetState();

        result.Should().Be((ErrorReportingConsentState)expectedStateValue);
    }
}
