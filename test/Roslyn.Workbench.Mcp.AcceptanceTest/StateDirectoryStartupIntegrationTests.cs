namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class StateDirectoryStartupIntegrationTests
{
    private const UnixFileMode _privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    [Fact]
    public async Task GIVEN_AbsentStateDirectory_WHEN_StartingPublishedHost_THEN_ShouldCreatePrivateStateDirectories()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            stateDirectoryPreparation: AcceptanceStateDirectoryPreparation.Absent);

        var recoveryDirectory = Path.Combine(target.StateRoot, "recovery");

        Directory.Exists(target.StateRoot).Should().BeTrue();
        Directory.Exists(recoveryDirectory).Should().BeTrue();

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(target.StateRoot).Should().Be(_privateDirectoryMode);
            File.GetUnixFileMode(recoveryDirectory).Should().Be(_privateDirectoryMode);
        }
    }

    [Fact]
    public async Task GIVEN_BroadUnixStateDirectory_WHEN_StartingPublishedHost_THEN_ShouldRejectStartup()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var action = async () => await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            stateDirectoryPreparation: AcceptanceStateDirectoryPreparation.BroadUnix);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("MCP initialization failed");
        exception.Which.Message.Should().Contain("Unix permissions '700'");
    }
}
