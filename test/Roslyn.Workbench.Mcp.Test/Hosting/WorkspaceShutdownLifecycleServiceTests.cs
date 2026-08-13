using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class WorkspaceShutdownLifecycleServiceTests
{
    [Fact]
    public async Task GIVEN_ApplicationStops_WHEN_StoppingLifecycle_THEN_ShouldShutdownWorkspaces()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var logger = new Mock<ILogger<WorkspaceShutdownLifecycleService>>();
        var target = new WorkspaceShutdownLifecycleService(workspaceLifecycleService.Object, logger.Object);

        await target.StopAsync(TestContext.Current.CancellationToken);

        workspaceLifecycleService.Verify(item => item.ShutdownAsync(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_OtherLifecyclePhases_WHEN_Invoked_THEN_ShouldNotShutdownWorkspaces()
    {
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        var logger = new Mock<ILogger<WorkspaceShutdownLifecycleService>>();
        var target = new WorkspaceShutdownLifecycleService(workspaceLifecycleService.Object, logger.Object);

        await target.StartingAsync(TestContext.Current.CancellationToken);
        await target.StartAsync(TestContext.Current.CancellationToken);
        await target.StartedAsync(TestContext.Current.CancellationToken);
        await target.StoppingAsync(TestContext.Current.CancellationToken);
        await target.StoppedAsync(TestContext.Current.CancellationToken);

        workspaceLifecycleService.Verify(item => item.ShutdownAsync(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_WorkspaceShutdownFailure_WHEN_StoppingLifecycle_THEN_ShouldLogAndComplete()
    {
        var exception = new InvalidOperationException("Message");
        var workspaceLifecycleService = new Mock<IWorkspaceLifecycleService>();
        workspaceLifecycleService
            .Setup(item => item.ShutdownAsync())
            .Returns(() => ValueTask.FromException(exception));

        var logger = new Mock<ILogger<WorkspaceShutdownLifecycleService>>();
        logger.Setup(item => item.IsEnabled(LogLevel.Error)).Returns(true);
        var target = new WorkspaceShutdownLifecycleService(workspaceLifecycleService.Object, logger.Object);

        var action = async () => await target.StopAsync(TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync();
        logger.Verify(
            item => item.Log(
                LogLevel.Error,
                It.Is<EventId>(eventId => eventId.Id == 1),
                It.Is<It.IsAnyType>((value, _) => value.ToString() == "Workspace resource cleanup failed during Host shutdown."),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
