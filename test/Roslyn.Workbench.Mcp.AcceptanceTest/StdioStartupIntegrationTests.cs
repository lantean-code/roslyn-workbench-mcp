namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class StdioStartupIntegrationTests
{
    [Fact]
    public async Task GIVEN_BrokenDotnetStartup_WHEN_InitialisingClient_THEN_ShouldCaptureStandardError()
    {
        var missingAssemblyName = $"missing-{Guid.NewGuid():N}.dll";

        var action = async () => await AcceptanceProcessFixture.StartCommandAsync(
            "dotnet",
            [missingAssemblyName],
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("MCP initialization failed");
        exception.Which.Message.Should().Contain($"Command: dotnet {missingAssemblyName}");
        exception.Which.Message.Should().NotContain("Exit code: unavailable");
        exception.Which.Message.Should().Contain("Standard error:");
        exception.Which.Message.Should().NotContain("Standard error:\r\n<none>");
    }
}
