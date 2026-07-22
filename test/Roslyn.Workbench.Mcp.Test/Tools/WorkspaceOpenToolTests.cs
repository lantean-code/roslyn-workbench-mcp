using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceOpenToolTests
{
    [Fact]
    public async Task GIVEN_OpenRequest_WHEN_OpeningWorkspace_THEN_ShouldRouteAndMapResult()
    {
        var service = new Mock<IWorkspaceLifecycleService>();
        service
            .Setup(item => item.OpenAsync("/workspace/Sample.csproj", "Alias", "/workspace", CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult<WorkspaceOpenOutcome>.Succeeded(new WorkspaceOpenOutcome
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
            }));
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
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("workspace").GetProperty("workspaceId").GetString().Should().Be("WorkspaceId");
        data.GetProperty("projectCount").GetInt32().Should().Be(2);
        data.GetProperty("documentCount").GetInt32().Should().Be(5);
        service.Verify(item => item.OpenAsync("/workspace/Sample.csproj", "Alias", "/workspace", CancellationToken.None), Times.Once);
        protocolFactory.Verify(item => item.CreateServerOwnedTool<WorkspaceOpenRequest, WorkspaceOpenData>(
            "workspace-open",
            "Workspace Open",
            "Loads an additional writable workspace. If instance status reports that it is or may be in use elsewhere, use it only for necessary queries, expect results to become stale, and coordinate mutation ownership before starting a transaction.",
            false,
            false,
            null,
            ToolOutputSchemaMode.Omit), Times.Once);
    }
}
