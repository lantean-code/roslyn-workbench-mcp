using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class StartupPrerequisiteLifecycleServiceIntegrationTests
{
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
