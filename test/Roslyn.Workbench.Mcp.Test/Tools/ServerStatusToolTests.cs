using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Tools;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class ServerStatusToolTests
{
    [Fact]
    public async Task GIVEN_StatusRequest_WHEN_CallingExecuteAsync_THEN_ShouldReturnServiceResult()
    {
        var service = new Mock<IServerStatusService>();
        service
            .Setup(item => item.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None))
            .ReturnsAsync(ToolResult<ServerStatusData>.Succeeded(new ServerStatusData
            {
                ToolCount = 5,
            }));
        var target = new ServerStatusTool(Options.Create(new StartupOptions()), service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "server-status",
            new Dictionary<string, JsonElement>
            {
                ["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("toolCount").GetInt32().Should().Be(5);
        service.Verify(item => item.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None), Times.Once);
    }

    [Fact]
    public void GIVEN_DefaultOutputSchemaMode_WHEN_CreatingServerStatusTool_THEN_ShouldOmitOutputSchemaAndAppendResultHint()
    {
        var service = new Mock<IServerStatusService>();
        var target = new ServerStatusTool(Options.Create(new StartupOptions()), service.Object);

        target.ProtocolTool.OutputSchema.Should().BeNull();
        target.ProtocolTool.Description.Should().Be("Returns server diagnostics without requiring a loaded workspace. Result: server diagnostics, effective configuration, plugin status, and unfinished recovery state.");
    }

    [Fact]
    public void GIVEN_FullOutputSchemaMode_WHEN_CreatingServerStatusTool_THEN_ShouldPublishOutputSchema()
    {
        var options = new StartupOptions
        {
            ToolOutputSchemaMode = ToolOutputSchemaMode.Full,
        };
        var service = new Mock<IServerStatusService>();
        var target = new ServerStatusTool(Options.Create(options), service.Object);

        target.ProtocolTool.OutputSchema.Should().NotBeNull();
        target.ProtocolTool.OutputSchema!.Value.GetProperty("oneOf").ValueKind.Should().Be(JsonValueKind.Array);
    }
}
