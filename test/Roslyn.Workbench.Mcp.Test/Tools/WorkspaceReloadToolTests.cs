using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceReloadToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_ReloadingWorkspace_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.ReloadAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult<WorkspaceReloadOutcome>.Succeeded(new WorkspaceReloadOutcome
            {
                Workspace = new WorkspaceIdentity
                {
                    WorkspaceId = "WorkspaceId",
                    WorkspaceEpoch = 4,
                    LoadedPath = "/workspace/Sample.csproj",
                },
                ProjectCount = 4,
                DocumentCount = 10,
            }));
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceReloadTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-reload",
            ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("workspace").GetProperty("workspaceEpoch").GetInt64().Should().Be(4);
        data.GetProperty("projectCount").GetInt32().Should().Be(4);
        service.Verify(item => item.ReloadAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            CancellationToken.None), Times.Once);
    }
}
