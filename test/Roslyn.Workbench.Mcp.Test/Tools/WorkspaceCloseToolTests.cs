using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceCloseToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_ClosingWorkspace_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.CloseAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new WorkspaceCloseOutcome
            {
                ClosedPath = "/workspace/Sample.csproj",
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var boundRequest = new WorkspaceCloseRequest
        {
            Workspace = includeWorkspace ? ServerOwnedToolTestData.CreateWorkspaceSelector() : null,
        };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);
        var target = new WorkspaceCloseTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-close",
            ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("closedPath").GetString().Should().Be("/workspace/Sample.csproj");
        service.Verify(item => item.CloseAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            CancellationToken.None), Times.Once);
    }
}
