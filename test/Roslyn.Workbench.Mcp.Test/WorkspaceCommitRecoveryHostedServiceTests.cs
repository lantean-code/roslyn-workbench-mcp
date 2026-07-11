namespace Roslyn.Workbench.Mcp.Test;

public sealed class WorkspaceCommitRecoveryHostedServiceTests
{
    [Fact]
    public async Task GIVEN_HostStartup_WHEN_Starting_THEN_ShouldCompleteRecoveryBeforeReturning()
    {
        var recoveryService = new Mock<IWorkspaceCommitRecoveryService>();
        var target = new WorkspaceCommitRecoveryHostedService(recoveryService.Object);

        await target.StartAsync(TestContext.Current.CancellationToken);

        recoveryService.Verify(item => item.RecoverAsync(TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task GIVEN_HostShutdown_WHEN_Stopping_THEN_ShouldCompleteWithoutRecoveryWork()
    {
        var recoveryService = new Mock<IWorkspaceCommitRecoveryService>();
        var target = new WorkspaceCommitRecoveryHostedService(recoveryService.Object);

        var action = async () => await target.StopAsync(TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync();
        recoveryService.Verify(item => item.RecoverAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
