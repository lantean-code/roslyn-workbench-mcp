namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedHostExecutableIntegrationTests
{
    [Fact]
    public void GIVEN_HostPathIsAbsent_WHEN_ResolvingPublishedHost_THEN_ShouldReturnActionableFailure()
    {
        var action = () => PublishedHostExecutable.Resolve(null);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"*{PublishedHostExecutable.EnvironmentVariableName}*Debug and Release*");
    }

    [Fact]
    public void GIVEN_HostPathDoesNotExist_WHEN_ResolvingPublishedHost_THEN_ShouldReturnActionableFailure()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Roslyn.Workbench.Mcp");

        var action = () => PublishedHostExecutable.Resolve(missingPath);

        action.Should()
            .Throw<FileNotFoundException>()
            .WithMessage($"*{PublishedHostExecutable.EnvironmentVariableName}*{missingPath}*");
    }
}
