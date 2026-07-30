namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class MsBuildRegistrationServiceIntegrationTests
{
    [Fact]
    public void GIVEN_UninitialisedService_WHEN_GettingStatus_THEN_ShouldReportUnavailable()
    {
        var target = new MsBuildRegistrationService();

        var status = target.CurrentStatus;

        status.IsAvailable.Should().BeFalse();
        status.Message.Should().Be("MSBuild has not been registered.");
    }

    [Fact]
    public void GIVEN_MsBuildIsRegistered_WHEN_EnsuringRegistration_THEN_ShouldReturnAndCacheAvailableStatus()
    {
        var registrationService = new MsBuildRegistrationService();
        registrationService.EnsureRegistered();
        var target = new MsBuildRegistrationService();

        var status = target.EnsureRegistered();
        var cachedStatus = target.EnsureRegistered();

        status.IsAvailable.Should().BeTrue();
        status.Message.Should().Be("MSBuild was registered before the server started.");
        cachedStatus.Should().BeSameAs(status);
        target.CurrentStatus.Should().BeSameAs(status);
    }
}
