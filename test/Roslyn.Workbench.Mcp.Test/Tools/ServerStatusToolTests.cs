using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class ServerStatusToolTests
{
    [Fact]
    public async Task GIVEN_StatusRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnServiceResult()
    {
        var service = new Mock<IServerStatusService>();
        service
            .Setup(item => item.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None))
            .ReturnsAsync(ToolResult.Succeeded(new ServerStatusData
            {
                ToolCount = 5,
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new ServerStatusTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "server-status",
            new Dictionary<string, JsonElement>
            {
                ["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("toolCount").GetInt32().Should().Be(5);
        service.Verify(item => item.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None), Times.Once);
    }

}
