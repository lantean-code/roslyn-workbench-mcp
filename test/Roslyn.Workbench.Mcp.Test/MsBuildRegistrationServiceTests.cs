namespace Roslyn.Workbench.Mcp.Test;

public sealed class MsBuildRegistrationServiceTests
{
    [Fact]
    public void GIVEN_Service_WHEN_EnsuringMsBuildRegistration_THEN_ShouldExposeTheCurrentStatus()
    {
        var target = new MsBuildRegistrationService();

        var status = target.EnsureRegistered();

        target.CurrentStatus.Should().BeEquivalentTo(status);
    }
}
