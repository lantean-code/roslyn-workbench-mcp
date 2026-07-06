namespace Roslyn.Workbench.Mcp.Test;

public sealed class MsBuildRegistrationHostedServiceTests
{
    [Fact]
    public void GIVEN_NullRegistrationService_WHEN_ConstructingHostedService_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => new MsBuildRegistrationHostedService(null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("msBuildRegistrationService");
    }

    [Fact]
    public async Task GIVEN_RegistrationService_WHEN_StartingHostedService_THEN_ShouldEnsureMsBuildIsRegistered()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        msBuildRegistrationService
            .Setup(static service => service.EnsureRegistered())
            .Returns(new ComponentStatus
            {
                IsAvailable = true,
            });
        var target = new MsBuildRegistrationHostedService(msBuildRegistrationService.Object);

        await target.StartAsync(CancellationToken.None);

        msBuildRegistrationService.Verify(static service => service.EnsureRegistered(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_StartingHostedService_THEN_ShouldThrowOperationCanceledException()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        var target = new MsBuildRegistrationHostedService(msBuildRegistrationService.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.StartAsync(cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        msBuildRegistrationService.Verify(static service => service.EnsureRegistered(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_StoppingHostedService_THEN_ShouldThrowOperationCanceledException()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        var target = new MsBuildRegistrationHostedService(msBuildRegistrationService.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.StopAsync(cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_RegistrationService_WHEN_StoppingHostedService_THEN_ShouldCompleteWithoutCallingRegistration()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        var target = new MsBuildRegistrationHostedService(msBuildRegistrationService.Object);

        await target.StopAsync(CancellationToken.None);

        msBuildRegistrationService.Verify(static service => service.EnsureRegistered(), Times.Never);
    }
}
