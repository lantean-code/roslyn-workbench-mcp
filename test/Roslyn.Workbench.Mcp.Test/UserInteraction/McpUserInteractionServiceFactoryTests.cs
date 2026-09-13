using Roslyn.Workbench.Mcp.Test.Tools;

namespace Roslyn.Workbench.Mcp.Test.UserInteraction;

public sealed class McpUserInteractionServiceFactoryTests
{
    [Fact]
    public async Task GIVEN_McpServer_WHEN_CreatingService_THEN_ShouldReturnSdkBackedAdapter()
    {
        await using var server = ServerOwnedToolTestSupport.CreateServer();
        var target = new McpUserInteractionServiceFactory();

        var result = target.Create(server);

        result.Should().BeOfType<McpUserInteractionService>();
    }
}
