using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.Test.Configuration;

public sealed class StartupConfigurationReporterTests
{
    [Fact]
    public async Task GIVEN_ConfigurationWarning_WHEN_Starting_THEN_ShouldLogWarning()
    {
        var logger = new Mock<ILogger<StartupConfigurationReporter>>();
        var target = new StartupConfigurationReporter(
            new StartupConfigurationSnapshot
            {
                Options = new StartupOptions(),
                Warnings =
                [
                    new WarningInfo
                    {
                        Code = "Code",
                        Message = "Message",
                    },
                ],
            },
            logger.Object);

        await target.StartAsync(TestContext.Current.CancellationToken);

        logger.Verify(
            item => item.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString() == "Startup configuration warning Code: Message"),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_NoConfigurationWarnings_WHEN_Starting_THEN_ShouldNotLogWarning()
    {
        var logger = new Mock<ILogger<StartupConfigurationReporter>>();
        var target = new StartupConfigurationReporter(
            new StartupConfigurationSnapshot
            {
                Options = new StartupOptions(),
            },
            logger.Object);

        await target.StartAsync(TestContext.Current.CancellationToken);

        logger.Verify(
            item => item.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_CancelledStartup_WHEN_Starting_THEN_ShouldThrowOperationCanceledException()
    {
        var logger = new Mock<ILogger<StartupConfigurationReporter>>();
        var target = new StartupConfigurationReporter(
            new StartupConfigurationSnapshot
            {
                Options = new StartupOptions(),
            },
            logger.Object);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await target.StartAsync(cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_HostShutdown_WHEN_Stopping_THEN_ShouldComplete()
    {
        var logger = new Mock<ILogger<StartupConfigurationReporter>>();
        var target = new StartupConfigurationReporter(
            new StartupConfigurationSnapshot
            {
                Options = new StartupOptions(),
            },
            logger.Object);

        var action = async () => await target.StopAsync(TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync();
    }
}
