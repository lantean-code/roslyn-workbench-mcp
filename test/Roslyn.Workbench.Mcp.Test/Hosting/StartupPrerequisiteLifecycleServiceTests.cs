using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class StartupPrerequisiteLifecycleServiceTests
{
    [Fact]
    public async Task GIVEN_StartupPrerequisites_WHEN_StartingLifecycle_THEN_ShouldRegisterMsBuildAndCompleteRecovery()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        msBuildRegistrationService
            .Setup(static service => service.EnsureRegistered())
            .Returns(new ComponentStatus());
        var workspaceCommitRecoveryService = new Mock<IWorkspaceCommitRecoveryService>();
        var target = new StartupPrerequisiteLifecycleService(
            msBuildRegistrationService.Object,
            workspaceCommitRecoveryService.Object);

        await target.StartingAsync(TestContext.Current.CancellationToken);

        msBuildRegistrationService.Verify(static service => service.EnsureRegistered(), Times.Once);
        workspaceCommitRecoveryService.Verify(
            service => service.RecoverAsync(TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_CancelledStartup_WHEN_StartingLifecycle_THEN_ShouldNotRunPrerequisites()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        var workspaceCommitRecoveryService = new Mock<IWorkspaceCommitRecoveryService>();
        var target = new StartupPrerequisiteLifecycleService(
            msBuildRegistrationService.Object,
            workspaceCommitRecoveryService.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = async () => await target.StartingAsync(cancellationTokenSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        msBuildRegistrationService.Verify(static service => service.EnsureRegistered(), Times.Never);
        workspaceCommitRecoveryService.Verify(
            static service => service.RecoverAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoLifecycleWorkOutsideStartingPhase_WHEN_RunningOtherPhases_THEN_ShouldCompleteWithoutPrerequisites()
    {
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        var workspaceCommitRecoveryService = new Mock<IWorkspaceCommitRecoveryService>();
        var target = new StartupPrerequisiteLifecycleService(
            msBuildRegistrationService.Object,
            workspaceCommitRecoveryService.Object);

        await target.StartAsync(TestContext.Current.CancellationToken);
        await target.StartedAsync(TestContext.Current.CancellationToken);
        await target.StoppingAsync(TestContext.Current.CancellationToken);
        await target.StopAsync(TestContext.Current.CancellationToken);
        await target.StoppedAsync(TestContext.Current.CancellationToken);

        msBuildRegistrationService.Verify(static service => service.EnsureRegistered(), Times.Never);
        workspaceCommitRecoveryService.Verify(
            static service => service.RecoverAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_HostedTransport_WHEN_StartingHost_THEN_ShouldCompletePrerequisitesBeforeTransportStarts()
    {
        var sequence = new MockSequence();
        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>(MockBehavior.Strict);
        msBuildRegistrationService
            .InSequence(sequence)
            .Setup(static service => service.EnsureRegistered())
            .Returns(new ComponentStatus());
        var workspaceCommitRecoveryService = new Mock<IWorkspaceCommitRecoveryService>(MockBehavior.Strict);
        workspaceCommitRecoveryService
            .InSequence(sequence)
            .Setup(service => service.RecoverAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var transport = new Mock<IHostedService>(MockBehavior.Strict);
        transport
            .InSequence(sequence)
            .Setup(service => service.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(service => service.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(msBuildRegistrationService.Object);
        builder.Services.AddSingleton(workspaceCommitRecoveryService.Object);
        builder.Services.AddHostedService<StartupPrerequisiteLifecycleService>();
        builder.Services.AddSingleton<IHostedService>(transport.Object);
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        transport.Verify(
            service => service.StartAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
