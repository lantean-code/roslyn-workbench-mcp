using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceOpenToolTests
{
    [Fact]
    public async Task GIVEN_OpenRequest_WHEN_OpeningWorkspace_THEN_ShouldRouteAndMapResult()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.OpenAsync("/workspace/Sample.csproj", "Alias", "/workspace", CancellationToken.None))
            .ReturnsAsync(new WorkspaceOperationResult<WorkspaceOpenOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new WorkspaceOpenOutcome
                {
                    Workspace = new WorkspaceIdentity
                    {
                        WorkspaceId = "WorkspaceId",
                        Alias = "Alias",
                        WorkspaceEpoch = 3,
                        LoadedPath = "/workspace/Sample.csproj",
                        WorkspaceRoot = "/workspace",
                    },
                    ProjectCount = 2,
                    DocumentCount = 5,
                },
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new WorkspaceOpenTool(Options.Create(new StartupOptions()), protocolFactory.Object, service.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "workspace-open",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement("/workspace/Sample.csproj"),
                ["alias"] = JsonSerializer.SerializeToElement("Alias"),
                ["workspaceRoot"] = JsonSerializer.SerializeToElement("/workspace"),
            },
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("workspace").GetProperty("workspaceId").GetString().Should().Be("WorkspaceId");
        result.StructuredContent.Value.GetProperty("projectCount").GetInt32().Should().Be(2);
        result.StructuredContent.Value.GetProperty("documentCount").GetInt32().Should().Be(5);
        service.Verify(item => item.OpenAsync("/workspace/Sample.csproj", "Alias", "/workspace", CancellationToken.None), Times.Once);
    }
}
